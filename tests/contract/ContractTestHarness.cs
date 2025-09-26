using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Application.Features.CheckCompatibility;
using PubDevMcp.Application.Features.DependencyInspector;
using PubDevMcp.Application.Features.LatestVersion;
using PubDevMcp.Application.Features.ListVersions;
using PubDevMcp.Application.Features.PackageDetails;
using PubDevMcp.Application.Features.PublisherPackages;
using PubDevMcp.Application.Features.ScoreInsights;
using PubDevMcp.Application.Features.SearchPackages;
#nullable enable
using PubDevMcp.Domain.Entities;
using PubDevMcp.Infrastructure.Telemetry;
using PubDevMcp.Server.Configuration;
using PubDevMcp.Server.JsonRpc;
using PubDevMcp.Server.Tools;

namespace PubDevMcp.Tests.Contract;

internal static class ContractTestHarness
{
    private static readonly JsonSerializerOptions SerializerOptions = McpTools.GetSerializerOptions();
    private static readonly ActivitySource TestActivitySource = new("PubDevMcp.Tests.Contracts");

    public static Task<SearchPackagesContractResult> ExecuteSearchPackagesContractAsync(string query)
        => ExecuteAsync<SearchPackagesQuery, SearchPackagesContractResult>(
            McpToolNames.SearchPackages,
            JsonSerializer.SerializeToNode(new SearchPackagesQuery(query), SerializerOptions)!,
            MapSearchPackagesResult);

    public static Task<LatestVersionContractResult> ExecuteLatestVersionContractAsync(string package)
        => ExecuteAsync<LatestVersionQuery, LatestVersionContractResult>(
            McpToolNames.LatestVersion,
            JsonSerializer.SerializeToNode(new LatestVersionQuery(package), SerializerOptions)!,
            result => MapLatestVersionResult(package, result));

    public static Task<CompatibilityContractResult> ExecuteCompatibilityContractAsync(string package, string flutterSdk)
        => ExecuteAsync<CheckCompatibilityQuery, CompatibilityContractResult>(
            McpToolNames.CheckCompatibility,
            JsonSerializer.SerializeToNode(new CheckCompatibilityQuery(package, flutterSdk, null), SerializerOptions)!,
            MapCompatibilityResult);

    public static Task<ListVersionsContractResult> ExecuteListVersionsContractAsync(string package, bool includePrerelease)
        => ExecuteAsync<ListVersionsQuery, ListVersionsContractResult>(
            McpToolNames.ListVersions,
            JsonSerializer.SerializeToNode(new ListVersionsQuery(package, includePrerelease, 50), SerializerOptions)!,
            result => MapListVersionsResult(package, includePrerelease, result));

    public static Task<PackageDetailsContractResult> ExecutePackageDetailsContractAsync(string package)
        => ExecuteAsync<PackageDetailsQuery, PackageDetailsContractResult>(
            McpToolNames.PackageDetails,
            JsonSerializer.SerializeToNode(new PackageDetailsQuery(package), SerializerOptions)!,
            MapPackageDetailsResult);

    public static Task<PublisherPackagesContractResult> ExecutePublisherPackagesContractAsync(string publisher)
        => ExecuteAsync<PublisherPackagesQuery, PublisherPackagesContractResult>(
            McpToolNames.PublisherPackages,
            JsonSerializer.SerializeToNode(new PublisherPackagesQuery(publisher), SerializerOptions)!,
            result => MapPublisherPackagesResult(publisher, result));

    public static Task<ScoreInsightsContractResult> ExecuteScoreInsightsContractAsync(string package)
        => ExecuteAsync<ScoreInsightsQuery, ScoreInsightsContractResult>(
            McpToolNames.ScoreInsights,
            JsonSerializer.SerializeToNode(new ScoreInsightsQuery(package), SerializerOptions)!,
            MapScoreInsightsResult);

    public static Task<DependencyInspectorContractResult> ExecuteDependencyInspectorContractAsync(string package, string? version)
        => ExecuteAsync<DependencyInspectorQuery, DependencyInspectorContractResult>(
            McpToolNames.DependencyInspector,
            JsonSerializer.SerializeToNode(new DependencyInspectorQuery(package, version, true), SerializerOptions)!,
            MapDependencyInspectorResult);

    public static string GenerateOpenApiContract()
    {
        var descriptors = McpTools.GetRegistry().Values
            .OrderBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ToArray();

        var document = new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = "Pub.dev Package Intelligence MCP API",
                ["version"] = "0.1.0"
            }
        };

        var paths = new JsonObject();
        foreach (var descriptor in descriptors)
        {
            var pathName = $"/tools/{descriptor.Name.Replace('_', '-')}";
            paths[pathName] = BuildPostOperation(descriptor);
        }

        document["paths"] = paths;
        return document.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static JsonObject BuildPostOperation(McpToolDescriptor descriptor)
    {
        return new JsonObject
        {
            ["post"] = new JsonObject
            {
                ["summary"] = descriptor.Description,
                ["operationId"] = descriptor.Name,
                ["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = new JsonObject
                            {
                                ["type"] = "object"
                            }
                        }
                    }
                },
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject
                    {
                        ["description"] = "Successful response"
                    }
                }
            }
        };
    }

    private static async Task<TContract> ExecuteAsync<TRequest, TContract>(
        string method,
        JsonNode parameters,
        Func<JsonNode, TContract> projector)
    {
        await using var harness = await Harness.CreateAsync().ConfigureAwait(false);

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString("N"),
            ["method"] = method,
            ["params"] = parameters.DeepClone()
        };

        var payload = request.ToJsonString(SerializerOptions);
        var responsePayload = await harness.Pipeline.ExecuteAsync(payload, CancellationToken.None).ConfigureAwait(false);
        responsePayload.Should().NotBeNull("JSON-RPC methods must return payloads for contract execution");

        var responseJson = JsonNode.Parse(responsePayload!, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        ((object?)responseJson).Should().NotBeNull();
        var response = responseJson!.AsObject();

        if (response.TryGetPropertyValue("error", out var errorNode))
        {
            var errorMessage = errorNode?["message"]?.GetValue<string?>();
            var errorData = errorNode?["data"]?.ToJsonString(SerializerOptions);
            throw new InvalidOperationException($"JSON-RPC method '{method}' returned error: {errorMessage ?? "Unknown error"}. Details: {errorData ?? "<none>"}.");
        }

        var resultNode = response["result"];
        ((object?)resultNode).Should().NotBeNull();

        return projector(resultNode!);
    }

    private static SearchPackagesContractResult MapSearchPackagesResult(JsonNode node)
    {
        var result = RequireObject(node, "search_packages result");
        var packages = RequireArrayProperty(result, "packages")
            .Select(item => MapPackageSummary(RequireObject(item, "packages[]")))
            .ToArray();

        return new SearchPackagesContractResult(
            RequireValue<string>(result, "query"),
            packages,
            GetOptionalString(result, "moreResultsHint"));
    }

    private static LatestVersionContractResult MapLatestVersionResult(string package, JsonNode node)
    {
        var version = MapVersionDetail(RequireObject(node, "latest_version result"));
        return new LatestVersionContractResult(package, version);
    }

    private static CompatibilityContractResult MapCompatibilityResult(JsonNode node)
    {
        var result = RequireObject(node, "check_compatibility result");
        var request = RequireObjectProperty(result, "request");

        var recommendedNode = result.TryGetPropertyValue("recommendedVersion", out var recNode) && recNode is JsonObject recObj
            ? MapVersionDetail(recObj)
            : null;

        var evaluated = RequireArrayProperty(result, "evaluatedVersions")
            .Select(item => MapVersionDetail(RequireObject(item, "evaluatedVersions[]")))
            .ToArray();

        return new CompatibilityContractResult(
            RequireValue<string>(request, "package"),
            RequireValue<string>(request, "flutterSdk"),
            recommendedNode,
            RequireValue<bool>(result, "satisfies"),
            RequireValue<string>(result, "explanation"),
            evaluated);
    }

    private static ListVersionsContractResult MapListVersionsResult(string package, bool includePrerelease, JsonNode node)
    {
        var versions = RequireArray(node, "list_versions result")
            .Select(item => MapVersionDetail(RequireObject(item, "versions[]")))
            .ToArray();

        return new ListVersionsContractResult(package, includePrerelease, versions);
    }

    private static PackageDetailsContractResult MapPackageDetailsResult(JsonNode node)
    {
        var result = RequireObject(node, "package_details result");
        var topics = RequireArrayProperty(result, "topics")
            .Select(item => GetString(item!, "topics[]"))
            .ToArray();

        return new PackageDetailsContractResult(
            RequireValue<string>(result, "package"),
            RequireValue<string>(result, "description"),
            RequireValue<string>(result, "publisher"),
            GetOptionalString(result, "homepageUrl") ?? string.Empty,
            GetOptionalString(result, "repositoryUrl") ?? string.Empty,
            GetOptionalString(result, "issueTrackerUrl") ?? string.Empty,
            MapVersionDetail(RequireObjectProperty(result, "latestStable")),
            topics);
    }

    private static PublisherPackagesContractResult MapPublisherPackagesResult(string publisher, JsonNode node)
    {
        var packages = RequireArray(node, "publisher_packages result")
            .Select(item => MapPackageSummary(RequireObject(item, "packages[]")))
            .ToArray();

        return new PublisherPackagesContractResult(publisher, packages);
    }

    private static ScoreInsightsContractResult MapScoreInsightsResult(JsonNode node)
    {
        var result = RequireObject(node, "score_insights result");
        var componentNotes = result.TryGetPropertyValue("componentNotes", out var notesNode) && notesNode is JsonObject notesObj
            ? MapComponentNotes(notesObj)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new ScoreInsightsContractResult(
            RequireValue<string>(result, "package"),
            RequireValue<double>(result, "overallScore"),
            RequireValue<double>(result, "popularity"),
            RequireValue<int>(result, "likes"),
            RequireValue<int>(result, "pubPoints"),
            componentNotes);
    }

    private static DependencyInspectorContractResult MapDependencyInspectorResult(JsonNode node)
    {
        var result = RequireObject(node, "dependency_inspector result");
        var nodes = RequireArrayProperty(result, "nodes")
            .Select(item => MapDependencyNode(RequireObject(item, "nodes[]")))
            .ToArray();

        var issues = GetStringArray(result, "issues");

        return new DependencyInspectorContractResult(
            RequireValue<string>(result, "rootPackage"),
            RequireValue<string>(result, "rootVersion"),
            nodes,
            issues);
    }

    private static PackageSummaryContractModel MapPackageSummary(JsonObject summary)
    {
        var latestStableNode = summary.TryGetPropertyValue("latestStable", out var latestNode) && latestNode is JsonObject latestObj
            ? MapVersionDetail(latestObj)
            : new VersionDetailContractModel(string.Empty, DateTimeOffset.UnixEpoch, string.Empty, false, null);

        return new PackageSummaryContractModel(
            RequireValue<string>(summary, "name"),
            RequireValue<string>(summary, "description"),
            RequireValue<string>(summary, "publisher"),
            RequireValue<int>(summary, "likes"),
            RequireValue<int>(summary, "pubPoints"),
            RequireValue<double>(summary, "popularity"),
            latestStableNode);
    }

    private static VersionDetailContractModel MapVersionDetail(JsonObject version)
    {
        var releaseNotes = GetOptionalString(version, "releaseNotesUrl");

        return new VersionDetailContractModel(
            RequireValue<string>(version, "version"),
            RequireValue<DateTimeOffset>(version, "released"),
            RequireValue<string>(version, "sdkConstraint"),
            RequireValue<bool>(version, "isPrerelease"),
            string.IsNullOrWhiteSpace(releaseNotes) ? null : new Uri(releaseNotes, UriKind.Absolute));
    }

    private static DependencyNodeContractModel MapDependencyNode(JsonObject node)
    {
        var children = RequireArrayProperty(node, "children")
            .Select(item => MapDependencyNode(RequireObject(item, "children[]")))
            .ToArray();

        return new DependencyNodeContractModel(
            RequireValue<string>(node, "package"),
            RequireValue<string>(node, "requested"),
            RequireValue<string>(node, "resolved"),
            RequireValue<bool>(node, "isDirect"),
            children);
    }

    private static Dictionary<string, string> MapComponentNotes(JsonObject componentNotes)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, valueNode) in componentNotes)
        {
            if (valueNode is JsonValue value && value.TryGetValue(out string? text) && text is not null)
            {
                dictionary[key] = text;
            }
        }

        return dictionary;
    }

    private static JsonObject RequireObject(JsonNode? node, string description)
    {
        if (node is JsonObject obj)
        {
            return obj;
        }

        throw new InvalidOperationException($"Expected JSON object for {description}.");
    }

    private static JsonArray RequireArray(JsonNode? node, string description)
    {
        if (node is JsonArray array)
        {
            return array;
        }

        throw new InvalidOperationException($"Expected JSON array for {description}.");
    }

    private static JsonNode RequireProperty(JsonObject obj, string propertyName)
        => obj.TryGetPropertyValue(propertyName, out var node) && node is not null
            ? node
            : throw new InvalidOperationException($"Property '{propertyName}' is required in JSON payload.");

    private static JsonObject RequireObjectProperty(JsonObject obj, string propertyName)
        => RequireObject(RequireProperty(obj, propertyName), $"property '{propertyName}'");

    private static JsonArray RequireArrayProperty(JsonObject obj, string propertyName)
        => RequireArray(RequireProperty(obj, propertyName), $"property '{propertyName}'");

    private static T RequireValue<T>(JsonObject obj, string propertyName)
        => GetValue<T>(RequireProperty(obj, propertyName), propertyName);

    private static T GetValue<T>(JsonNode node, string description)
    {
        if (node is JsonValue value && value.TryGetValue(out T? result) && result is not null)
        {
            return result;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name} for {description}.");
    }

    private static string GetString(JsonNode node, string description)
        => GetValue<string>(node, description);

    private static string? GetOptionalString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue(out string? result) ? result : null;
    }

    private static string[] GetStringArray(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return Array.Empty<string>();
        }

        if (node is JsonArray array)
        {
            return array.Select(item => GetString(item!, $"{propertyName}[]")).ToArray();
        }

        throw new InvalidOperationException($"Property '{propertyName}' must be an array of strings.");
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private Harness(ServiceProvider provider, JsonRpcPipeline pipeline)
        {
            _provider = provider;
            Pipeline = pipeline;
        }

        public JsonRpcPipeline Pipeline { get; }

        public static Task<Harness> CreateAsync()
        {
            var configuration = new ConfigurationBuilder()
                .Add(new DictionaryConfigurationSource(new Dictionary<string, string?>
                {
                    ["PubDev:Api:BaseAddress"] = "https://contract.local/",
                    ["PubDev:Api:UserAgent"] = "PubDevMcp.Tests.Contracts/1.0",
                    ["PubDev:Resilience:RetryCount"] = "0"
                }))
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders());
            services.AddSingleton<ActivitySource>(_ => new ActivitySource("PubDevMcp.Tests.Contracts"));

            ServiceConfiguration.Configure(services, configuration);
            services.AddSingleton<JsonSerializerOptions>(_ => SerializerOptions);
            services.AddSingleton(TestActivitySource);
            services.AddSingleton<IPubDevApiClient, FakePubDevApiClient>();

            var provider = services.BuildServiceProvider(validateScopes: false);
            var pipeline = provider.GetRequiredService<JsonRpcPipeline>();

            return Task.FromResult(new Harness(provider, pipeline));
        }

        public ValueTask DisposeAsync()
        {
            _provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static class McpToolNames
    {
        public const string SearchPackages = "search_packages";
        public const string LatestVersion = "latest_version";
        public const string CheckCompatibility = "check_compatibility";
        public const string ListVersions = "list_versions";
        public const string PackageDetails = "package_details";
        public const string PublisherPackages = "publisher_packages";
        public const string ScoreInsights = "score_insights";
        public const string DependencyInspector = "dependency_inspector";
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered via dependency injection in contract tests")]
    private sealed class FakePubDevApiClient : IPubDevApiClient
    {
        private static readonly VersionDetail StableHttpVersion = VersionDetail.Create(
            "1.2.1",
            new DateTimeOffset(2024, 11, 12, 0, 0, 0, TimeSpan.Zero),
            ">=3.13.0 <4.0.0",
            isPrerelease: false,
            releaseNotesUrl: new Uri("https://pub.dev/packages/http/changelog"));

        private static readonly VersionDetail MaintenanceHttpVersion = VersionDetail.Create(
            "1.2.0",
            new DateTimeOffset(2024, 8, 5, 0, 0, 0, TimeSpan.Zero),
            ">=3.10.0 <4.0.0",
            isPrerelease: false,
            releaseNotesUrl: new Uri("https://pub.dev/packages/http/changelog"));

        private static readonly VersionDetail PrereleaseHttpVersion = VersionDetail.Create(
            "1.3.0-beta.1",
            new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero),
            ">=3.15.0 <4.0.0",
            isPrerelease: true,
            releaseNotesUrl: new Uri("https://pub.dev/packages/http/changelog"));

        private static readonly IReadOnlyList<VersionDetail> HttpVersionHistory = Array.AsReadOnly(new[]
        {
            PrereleaseHttpVersion,
            StableHttpVersion,
            MaintenanceHttpVersion
        });

        private static readonly IReadOnlyList<PackageSummary> SearchPackages = Array.AsReadOnly(new[]
        {
            PackageSummary.Create("http", "Composable HTTP client for Dart", "dart-lang", 2500, 140, 0.93, StableHttpVersion),
            PackageSummary.Create("http_client_helper", "Utilities for HTTP clients", "dart-lang", 1200, 120, 0.87, MaintenanceHttpVersion),
            PackageSummary.Create("http_parser", "Robust HTTP parser", "dart-lang", 950, 115, 0.85, MaintenanceHttpVersion),
            PackageSummary.Create("http_multi_server", "HTTP server that handles multiple requests", "dart-lang", 640, 110, 0.82, MaintenanceHttpVersion),
            PackageSummary.Create("http_retry", "Retry logic for HTTP requests", "dart-lang", 580, 108, 0.81, MaintenanceHttpVersion),
            PackageSummary.Create("http_interceptor", "Hook HTTP requests and responses", "app-co", 430, 99, 0.79, MaintenanceHttpVersion),
            PackageSummary.Create("http_mock_adapter", "Mocking adapter for HTTP", "dash-labs", 410, 101, 0.78, MaintenanceHttpVersion),
            PackageSummary.Create("http_signing", "Sign HTTP requests", "dart-lang", 380, 100, 0.77, MaintenanceHttpVersion),
            PackageSummary.Create("http_metrics", "Capture HTTP client metrics", "dash-labs", 360, 98, 0.76, MaintenanceHttpVersion),
            PackageSummary.Create("http_cache", "Caching layer for HTTP", "dash-labs", 350, 97, 0.75, MaintenanceHttpVersion)
        });

        private static readonly PackageDetails HttpPackageDetails = PackageDetails.Create(
            "http",
            "A composable, Future-based library for making HTTP requests.",
            "dart-lang",
            new Uri("https://github.com/dart-lang/http"),
            new Uri("https://github.com/dart-lang/http"),
            new Uri("https://github.com/dart-lang/http/issues"),
            StableHttpVersion,
            new[] { "network", "http", "client" });

        private static readonly ScoreInsight HttpScoreInsight = ScoreInsight.Create(
            "http",
            overallScore: 0.92,
            popularity: 0.90,
            likes: 2500,
            pubPoints: 140,
            componentNotes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["popularity"] = "Widely adopted across Dart and Flutter projects.",
                ["likes"] = "Strong community support with thousands of likes.",
                ["pubPoints"] = "Passes all automated pub.dev quality checks."
            },
            fetchedAt: DateTimeOffset.UtcNow);

        private static readonly DependencyGraph HttpDependencyGraph = DependencyGraph.Create(
            rootPackage: "http",
            rootVersion: StableHttpVersion.Version,
            nodes: new[]
            {
                DependencyNode.Create(
                    package: "http",
                    requested: StableHttpVersion.Version,
                    resolved: StableHttpVersion.Version,
                    isDirect: true,
                    children: new[]
                    {
                        DependencyNode.Create(
                            package: "async",
                            requested: ">=2.11.0 <4.0.0",
                            resolved: "2.12.0",
                            isDirect: false,
                            children: Array.Empty<DependencyNode>()),
                        DependencyNode.Create(
                            package: "meta",
                            requested: ">=1.11.0",
                            resolved: "1.12.0",
                            isDirect: false,
                            children: Array.Empty<DependencyNode>())
                    })
            },
            issues: Array.AsReadOnly(new[] { "No dependency conflicts detected." }));

        private static readonly IReadOnlyList<PackageSummary> PublisherPackages = Array.AsReadOnly(new[]
        {
            PackageSummary.Create("http", "Composable HTTP client for Dart", "dart-lang", 2500, 140, 0.93, StableHttpVersion),
            PackageSummary.Create("http_parser", "Robust HTTP parser", "dart-lang", 950, 115, 0.85, MaintenanceHttpVersion),
            PackageSummary.Create("http_multi_server", "HTTP server that handles multiple requests", "dart-lang", 640, 110, 0.82, MaintenanceHttpVersion)
        });

        public Task<SearchResultSet> SearchPackagesAsync(string query, bool includePrerelease, string? sdkConstraint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SearchResultSet.Create(query, SearchPackages, "Refine your query for more packages."));
        }

        public Task<VersionDetail> GetLatestVersionAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHttpPackage(package);
            return Task.FromResult(StableHttpVersion);
        }

        public Task<IReadOnlyList<VersionDetail>> GetVersionHistoryAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHttpPackage(package);
            return Task.FromResult(HttpVersionHistory);
        }

        public Task<PackageDetails> GetPackageDetailsAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHttpPackage(package);
            return Task.FromResult(HttpPackageDetails);
        }

        public Task<IReadOnlyList<PackageSummary>> GetPublisherPackagesAsync(string publisher, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(publisher, "dart-lang", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<PackageSummary>>(Array.AsReadOnly(Array.Empty<PackageSummary>()));
            }

            return Task.FromResult(PublisherPackages);
        }

        public Task<ScoreInsight> GetScoreInsightAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHttpPackage(package);
            return Task.FromResult(HttpScoreInsight);
        }

        public Task<DependencyGraph> InspectDependenciesAsync(string package, string? version, bool includeDevDependencies, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHttpPackage(package);
            return Task.FromResult(HttpDependencyGraph);
        }

        private static void EnsureHttpPackage(string package)
        {
            if (!string.Equals(package, "http", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Package '{package}' is not part of the contract test fixture.");
            }
        }
    }

    private sealed class DictionaryConfigurationSource : IConfigurationSource
    {
        private readonly IDictionary<string, string?> _initialData;

        public DictionaryConfigurationSource(IDictionary<string, string?> initialData)
        {
            _initialData = new Dictionary<string, string?>(initialData, StringComparer.OrdinalIgnoreCase);
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new DictionaryConfigurationProvider(_initialData);
    }

    private sealed class DictionaryConfigurationProvider : ConfigurationProvider
    {
        public DictionaryConfigurationProvider(IDictionary<string, string?> initialData)
        {
            foreach (var (key, value) in initialData)
            {
                Data[key] = value;
            }
        }
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will populate search result model during implementation")]
internal sealed record SearchPackagesContractResult(string Query, IReadOnlyList<PackageSummaryContractModel> Packages, string? MoreResultsHint);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will materialize package summary model during implementation")]
internal sealed record PackageSummaryContractModel(
    string Name,
    string Description,
    string Publisher,
    int Likes,
    int PubPoints,
    double Popularity,
    VersionDetailContractModel LatestStable
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will materialize version detail model during implementation")]
internal sealed record VersionDetailContractModel(
    string Version,
    DateTimeOffset Released,
    string SdkConstraint,
    bool IsPrerelease,
    Uri? ReleaseNotesUrl
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide latest version contract model during implementation")]
internal sealed record LatestVersionContractResult(string Package, VersionDetailContractModel Latest);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide compatibility contract model during implementation")]
internal sealed record CompatibilityContractResult(
    string Package,
    string RequestedFlutterSdk,
    VersionDetailContractModel? RecommendedVersion,
    bool Satisfies,
    string Explanation,
    IReadOnlyList<VersionDetailContractModel> EvaluatedVersions
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide list versions contract model during implementation")]
internal sealed record ListVersionsContractResult(
    string Package,
    bool IncludePrerelease,
    IReadOnlyList<VersionDetailContractModel> Versions
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide package details contract model during implementation")]
internal sealed record PackageDetailsContractResult(
    string Package,
    string Description,
    string Publisher,
    string HomepageUrl,
    string RepositoryUrl,
    string IssueTrackerUrl,
    VersionDetailContractModel LatestStable,
    IReadOnlyList<string> Topics
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide publisher packages contract model during implementation")]
internal sealed record PublisherPackagesContractResult(
    string Publisher,
    IReadOnlyList<PackageSummaryContractModel> Packages
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide score insights contract model during implementation")]
internal sealed record ScoreInsightsContractResult(
    string Package,
    double OverallScore,
    double Popularity,
    int Likes,
    int PubPoints,
    IReadOnlyDictionary<string, string> ComponentNotes
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide dependency inspector contract model during implementation")]
internal sealed record DependencyInspectorContractResult(
    string Package,
    string RootVersion,
    IReadOnlyList<DependencyNodeContractModel> Nodes,
    IReadOnlyList<string> Issues
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide dependency node contract model during implementation")]
internal sealed record DependencyNodeContractModel(
    string Package,
    string Requested,
    string Resolved,
    bool IsDirect,
    IReadOnlyList<DependencyNodeContractModel> Children
);
