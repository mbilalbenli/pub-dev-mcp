using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;
using PubDevMcp.Infrastructure.Options;
using Polly;

namespace PubDevMcp.Infrastructure.Services;

public sealed partial class PubDevApiClient : IPubDevApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PubDevApiOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ICacheService _cacheService;
    private static readonly PubDevApiClientJsonSerializerContext SerializerContext = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    });

    public PubDevApiClient(HttpClient httpClient, IOptions<PubDevApiOptions> options, ICacheService cacheService, ResiliencePipeline<HttpResponseMessage>? pipeline = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _pipeline = pipeline ?? ResiliencePipeline<HttpResponseMessage>.Empty;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = _options.BaseAddress;
        }

        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }
    }

    public async Task<SearchResultSet> SearchPackagesAsync(string query, bool includePrerelease, string? sdkConstraint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var searchUri = $"/api/search?q={Uri.EscapeDataString(query)}";
        if (includePrerelease)
        {
            searchUri += "&include-prerelease=true";
        }

    var response = await GetAsync(searchUri, SerializerContext.SearchResponse, cancellationToken).ConfigureAwait(false);
        var packageNames = response.Packages
            .Select(result => result.Package)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_options.SearchResultLimit)
            .ToArray();

        var summaries = new List<PackageSummary>(packageNames.Length);
        foreach (var packageName in packageNames)
        {
            var summary = await BuildPackageSummaryAsync(packageName, cancellationToken).ConfigureAwait(false);
            summaries.Add(summary);
        }

        var hint = response.HasMoreResults(packageNames.Length) ? "More packages available. Refine or paginate the query to continue browsing." : null;
        return SearchResultSet.Create(query, summaries, hint);
    }

    public async Task<VersionDetail> GetLatestVersionAsync(string package, CancellationToken cancellationToken)
    {
        var metadata = await GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);
        return MapVersion(metadata.LatestStable ?? metadata.Latest);
    }

    public async Task<IReadOnlyList<VersionDetail>> GetVersionHistoryAsync(string package, CancellationToken cancellationToken)
    {
        var metadata = await GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);
        var ordered = metadata.Versions
            .OrderByDescending(version => version.Published)
            .ThenByDescending(version => version.Version, StringComparer.OrdinalIgnoreCase)
            .Select(MapVersion)
            .ToArray();

        return Array.AsReadOnly(ordered);
    }

    public async Task<PackageDetails> GetPackageDetailsAsync(string package, CancellationToken cancellationToken)
    {
        var metadata = await GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);
        var score = await GetScoreAsync(package, cancellationToken).ConfigureAwait(false);

        var homepage = TryCreateUri(metadata.Latest.Pubspec.Homepage);
        var repository = TryCreateUri(metadata.Latest.Pubspec.Repository);
        var issueTracker = TryCreateUri(metadata.Latest.Pubspec.IssueTracker);

        var topics = metadata.Latest.Pubspec.Topics ?? Array.Empty<string>();
    var details = PackageDetails.Create(
            metadata.Name,
            metadata.Latest.Pubspec.Description ?? metadata.Name,
            metadata.Publisher ?? "unknown",
            homepage,
            repository,
            issueTracker,
            MapVersion(metadata.LatestStable ?? metadata.Latest),
            topics);

        return details;
    }

    public async Task<IReadOnlyList<PackageSummary>> GetPublisherPackagesAsync(string publisher, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);

        var collected = new List<PackageSummary>();
        string? next = $"/api/publishers/{Uri.EscapeDataString(publisher)}/packages";

        while (!string.IsNullOrEmpty(next))
        {
            var page = await GetAsync(next!, SerializerContext.PublisherPackagesResponse, cancellationToken).ConfigureAwait(false);

            foreach (var item in page.Packages)
            {
                var summary = await BuildPackageSummaryAsync(item.Package, cancellationToken).ConfigureAwait(false);
                collected.Add(summary);
            }

            next = page.NextPageUrl;
        }

        if (collected.Count == 0)
        {
            throw new InvalidOperationException($"Publisher '{publisher}' has no packages.");
        }

        return collected.AsReadOnly();
    }

    public async Task<ScoreInsight> GetScoreInsightAsync(string package, CancellationToken cancellationToken)
    {
        return await _cacheService.GetScoreInsightAsync(
            package,
            async ct =>
            {
                var score = await GetScoreAsync(package, ct).ConfigureAwait(false);
                var popularity = score.PopularityScore.HasValue ? Math.Clamp(score.PopularityScore.Value / 100d, 0d, 1d) : 0d;
                var pubPoints = score.PubPoints != 0 ? score.PubPoints : score.GrantedPoints;

                var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["popularity"] = "Popularity score sourced from pub.dev quality metrics.",
                    ["likes"] = "Community like count from pub.dev analytics.",
                    ["pubPoints"] = "Granted pub points reflecting code quality and documentation."
                };

                return ScoreInsight.Create(
                    package,
                    score.GrantedPoints,
                    popularity,
                    score.LikeCount,
                    pubPoints,
                    notes,
                    score.LastUpdated);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DependencyGraph> InspectDependenciesAsync(string package, string? version, bool includeDevDependencies, CancellationToken cancellationToken)
    {
        var cacheVersionKey = string.IsNullOrWhiteSpace(version) ? "LATEST" : version;

        return await _cacheService.GetDependencyGraphAsync(
            package,
            cacheVersionKey,
            includeDevDependencies,
            async ct =>
            {
                var metadata = await GetPackageMetadataAsync(package, ct).ConfigureAwait(false);
                var rootVersion = await ResolveVersionAsync(metadata, version, ct).ConfigureAwait(false);
                var requestedConstraint = string.IsNullOrWhiteSpace(version) ? rootVersion.Version : version;

                var graphBuilder = new DependencyGraphBuilder(this, includeDevDependencies);
                var nodes = await graphBuilder.BuildAsync(package, requestedConstraint, ct).ConfigureAwait(false);

                return DependencyGraph.Create(package, rootVersion.Version, nodes.Nodes, nodes.Issues);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<PackageSummary> BuildPackageSummaryAsync(string package, CancellationToken cancellationToken)
    {
        var metadata = await GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);
        var score = await GetScoreAsync(package, cancellationToken).ConfigureAwait(false);

        var latestStable = MapVersion(metadata.LatestStable ?? metadata.Latest);
        var popularity = score.PopularityScore.HasValue ? Math.Clamp(score.PopularityScore.Value / 100d, 0d, 1d) : 0d;

        return PackageSummary.Create(
            metadata.Name,
            metadata.Latest.Pubspec.Description ?? metadata.Name,
            metadata.Publisher ?? "unknown",
            score.LikeCount,
            score.PubPoints,
            popularity,
            latestStable);
    }

    private async Task<PackageMetadata> GetPackageMetadataAsync(string package, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);

        var packageUri = $"/api/packages/{Uri.EscapeDataString(package)}";
    var response = await GetAsync(packageUri, SerializerContext.PackageResponse, cancellationToken).ConfigureAwait(false);
        return new PackageMetadata(
            response.Name,
            response.Publisher,
            response.Latest,
            response.LatestStable,
            response.Versions);
    }

    private async Task<ScoreResponse> GetScoreAsync(string package, CancellationToken cancellationToken)
    {
        var scoreUri = $"/api/packages/{Uri.EscapeDataString(package)}/score";
    return await GetAsync(scoreUri, SerializerContext.ScoreResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PackageVersionResponse> ResolveVersionAsync(PackageMetadata metadata, string? requestedVersion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return metadata.LatestStable ?? metadata.Latest;
        }

        var match = metadata.Versions.FirstOrDefault(version => string.Equals(version.Version, requestedVersion, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        // Attempt to fetch specific version details from pub.dev when not present in cached metadata
        var versionUri = $"/api/packages/{Uri.EscapeDataString(metadata.Name)}/versions/{Uri.EscapeDataString(requestedVersion)}";
    return await GetAsync(versionUri, SerializerContext.PackageVersionResponse, cancellationToken).ConfigureAwait(false);
    }

    private static VersionDetail MapVersion(PackageVersionResponse version)
    {
        var sdkConstraint = version.Pubspec.Environment?.Sdk ?? "any";
        var releaseNotes = TryCreateUri(version.Pubspec.Changelog ?? version.Pubspec.IssueTracker);
        var nugetVersion = NuGetVersion.Parse(version.Version);

        return VersionDetail.Create(
            version.Version,
            version.Published,
            sdkConstraint,
            nugetVersion.IsPrerelease,
            releaseNotes);
    }

    private async Task<T> GetAsync<T>(string requestUri, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var uri = new Uri(requestUri, UriKind.RelativeOrAbsolute);

        var response = await _pipeline.ExecuteAsync(async token =>
        {
            var httpResponse = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            return httpResponse;
        }, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);

            return result ?? throw new InvalidOperationException($"Unable to deserialize response for '{requestUri}'.");
        }
    }

    private static Uri? TryCreateUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private sealed record PackageMetadata(
        string Name,
        string? Publisher,
        PackageVersionResponse Latest,
        PackageVersionResponse? LatestStable,
        IReadOnlyList<PackageVersionResponse> Versions);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record SearchResponse(
        [property: JsonPropertyName("packages")] IReadOnlyList<SearchPackage> Packages,
        [property: JsonPropertyName("next")] string? Next,
        [property: JsonPropertyName("totalCount")] int? TotalCount)
    {
        public bool HasMoreResults(int capturedCount)
            => (TotalCount ?? capturedCount) > capturedCount || !string.IsNullOrWhiteSpace(Next);
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record SearchPackage(
        [property: JsonPropertyName("package")] string Package,
        [property: JsonPropertyName("score")] SearchScore Score);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record SearchScore(
        [property: JsonPropertyName("popularityScore")] double? PopularityScore,
        [property: JsonPropertyName("grantedPoints")] int GrantedPoints,
        [property: JsonPropertyName("likeCount")] int LikeCount,
        [property: JsonPropertyName("pubPoints")] int PubPoints);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PackageResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("publisher")] string? Publisher,
        [property: JsonPropertyName("latest")] PackageVersionResponse Latest,
        [property: JsonPropertyName("latestStable")] PackageVersionResponse? LatestStable,
        [property: JsonPropertyName("versions")] IReadOnlyList<PackageVersionResponse> Versions);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PackageVersionResponse(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("published")] DateTimeOffset Published,
        [property: JsonPropertyName("pubspec")] PackagePubspec Pubspec);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PackagePubspec(
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("homepage")] string? Homepage,
        [property: JsonPropertyName("repository")] string? Repository,
        [property: JsonPropertyName("issue_tracker")] string? IssueTracker,
        [property: JsonPropertyName("changelog")] string? Changelog,
        [property: JsonPropertyName("documentation")] string? Documentation,
        [property: JsonPropertyName("environment")] PackageEnvironment? Environment,
        [property: JsonPropertyName("topics")] IReadOnlyList<string>? Topics,
        [property: JsonPropertyName("dependencies")] IReadOnlyDictionary<string, JsonElement>? Dependencies,
        [property: JsonPropertyName("dev_dependencies")] IReadOnlyDictionary<string, JsonElement>? DevDependencies);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PackageEnvironment(
        [property: JsonPropertyName("sdk")] string? Sdk);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record ScoreResponse(
        [property: JsonPropertyName("grantedPoints")] int GrantedPoints,
        [property: JsonPropertyName("pubPoints")] int PubPoints,
        [property: JsonPropertyName("likeCount")] int LikeCount,
        [property: JsonPropertyName("popularityScore")] double? PopularityScore,
        [property: JsonPropertyName("lastUpdated")] DateTimeOffset LastUpdated);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PublisherPackagesResponse(
        [property: JsonPropertyName("packages")] IReadOnlyList<PublisherPackageItem> Packages,
        [property: JsonPropertyName("next")] string? NextPageUrl);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via System.Text.Json deserialization")]
    private sealed record PublisherPackageItem(
        [property: JsonPropertyName("package")] string Package);

    private sealed record DependencyBuildResult(IReadOnlyList<DependencyNode> Nodes, IReadOnlyList<string> Issues);

    private sealed class DependencyGraphBuilder
    {
        private const int MaxDepth = 10;
        private readonly PubDevApiClient _client;
        private readonly bool _includeDevDependencies;
    private readonly HashSet<string> _activePath = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _issues = new();

        public DependencyGraphBuilder(PubDevApiClient client, bool includeDevDependencies)
        {
            _client = client;
            _includeDevDependencies = includeDevDependencies;
        }

        public async Task<DependencyBuildResult> BuildAsync(string package, string requestedVersion, CancellationToken cancellationToken)
        {
            var rootNode = await BuildNodeAsync(package, requestedVersion, true, 0, cancellationToken).ConfigureAwait(false);
            return new DependencyBuildResult(new[] { rootNode }, _issues);
        }

        private async Task<DependencyNode> BuildNodeAsync(string package, string requestedConstraint, bool isDirect, int depth, CancellationToken cancellationToken)
        {
            if (depth > MaxDepth)
            {
                _issues.Add($"Dependency depth exceeded limit for {package} ({requestedConstraint}).");
                return DependencyNode.Create(package, requestedConstraint, requestedConstraint, isDirect, Array.Empty<DependencyNode>());
            }

            var metadata = await _client.GetPackageMetadataAsync(package, cancellationToken).ConfigureAwait(false);
            var resolved = ResolveVersion(metadata, requestedConstraint);

            var key = $"{package}@{resolved.Version}";
            if (!_activePath.Add(key))
            {
                _issues.Add($"Detected circular dependency at {key}.");
                return DependencyNode.Create(package, requestedConstraint, resolved.Version, isDirect, Array.Empty<DependencyNode>());
            }

            var children = new List<DependencyNode>();
            try
            {
                await AddChildrenAsync(children, resolved.Pubspec.Dependencies, false, depth, cancellationToken).ConfigureAwait(false);

                if (_includeDevDependencies)
                {
                    await AddChildrenAsync(children, resolved.Pubspec.DevDependencies, true, depth, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _activePath.Remove(key);
            }

            return DependencyNode.Create(package, requestedConstraint, resolved.Version, isDirect, children);
        }

        private async Task AddChildrenAsync(List<DependencyNode> sink, IReadOnlyDictionary<string, JsonElement>? dependencies, bool isDevDependency, int depth, CancellationToken cancellationToken)
        {
            if (dependencies is null || dependencies.Count == 0)
            {
                return;
            }

            foreach (var (dependencyName, specification) in dependencies)
            {
                var constraint = ExtractConstraint(specification);
                try
                {
                    var isDirectChild = depth == 0 && !isDevDependency;
                    var child = await BuildNodeAsync(dependencyName, constraint, isDirectChild, depth + 1, cancellationToken).ConfigureAwait(false);
                    sink.Add(child);
                }
                catch (Exception ex)
                {
                    _issues.Add($"Failed to resolve dependency '{dependencyName}' ({constraint}): {ex.Message}");
                }
            }
        }

        private static string ExtractConstraint(JsonElement specification)
            => specification.ValueKind switch
            {
                JsonValueKind.String => specification.GetString() ?? "any",
                JsonValueKind.Object when specification.TryGetProperty("version", out var versionElement) => versionElement.GetString() ?? "any",
                _ => "any"
            };

        private static PackageVersionResponse ResolveVersion(PackageMetadata metadata, string constraint)
        {
            if (string.IsNullOrWhiteSpace(constraint) || constraint.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                return metadata.LatestStable ?? metadata.Latest;
            }

            if (!VersionRange.TryParse(constraint, out var range))
            {
                return metadata.LatestStable ?? metadata.Latest;
            }

            var candidate = metadata.Versions
                .Select(version => (version, parsed: NuGetVersion.Parse(version.Version)))
                .Where(pair => range.Satisfies(pair.parsed))
                .OrderByDescending(pair => pair.parsed)
                .Select(pair => pair.version)
                .FirstOrDefault();

            return candidate ?? metadata.LatestStable ?? metadata.Latest;
        }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SearchResponse))]
    [JsonSerializable(typeof(PublisherPackagesResponse))]
    [JsonSerializable(typeof(PackageResponse))]
    [JsonSerializable(typeof(PackageVersionResponse))]
    [JsonSerializable(typeof(ScoreResponse))]
    private sealed partial class PubDevApiClientJsonSerializerContext : JsonSerializerContext;
}
