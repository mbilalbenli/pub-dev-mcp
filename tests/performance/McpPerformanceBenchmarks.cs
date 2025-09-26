using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;
using PubDevMcp.Server.Configuration;
using PubDevMcp.Server.JsonRpc;

namespace PubDevMcp.Tests.Performance;

[Config(typeof(ThroughputConfig))]
[MemoryDiagnoser]
public sealed class McpPerformanceBenchmarks : IDisposable
{
    private ServiceProvider? _provider;
    private JsonRpcPipeline _pipeline = default!;
    private JsonSerializerOptions _serializerOptions = default!;

    private string _searchPayload = string.Empty;
    private string _latestVersionPayload = string.Empty;
    private string _compatibilityPayload = string.Empty;
    private string _listVersionsPayload = string.Empty;
    private string _packageDetailsPayload = string.Empty;
    private string _publisherPackagesPayload = string.Empty;
    private string _scoreInsightsPayload = string.Empty;
    private string _dependencyInspectorPayload = string.Empty;
    private string _batchPayload = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PubDev:Api:BaseAddress"] = "https://performance.local/",
                ["PubDev:Api:UserAgent"] = "PubDevMcp.Tests.Performance/1.0",
                ["PubDev:Resilience:RetryCount"] = "0"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddSingleton(new ActivitySource("PubDevMcp.Server"));

        ServiceConfiguration.Configure(services, configuration);

        services.AddSingleton<IPubDevApiClient, FakePubDevApiClient>();

        _provider = services.BuildServiceProvider(validateScopes: true);
        _pipeline = _provider.GetRequiredService<JsonRpcPipeline>();
        _serializerOptions = _provider.GetRequiredService<JsonSerializerOptions>();

        _searchPayload = SerializeRequest("search_packages", CreateSearchParameters(), "bench-search");
        _latestVersionPayload = SerializeRequest("latest_version", CreatePackageParameters(), "bench-latest");
        _compatibilityPayload = SerializeRequest("check_compatibility", CreateCompatibilityParameters(), "bench-compat");
        _listVersionsPayload = SerializeRequest("list_versions", CreateListVersionsParameters(), "bench-list");
        _packageDetailsPayload = SerializeRequest("package_details", CreatePackageParameters(), "bench-details");
        _publisherPackagesPayload = SerializeRequest("publisher_packages", CreatePublisherParameters(), "bench-publisher");
        _scoreInsightsPayload = SerializeRequest("score_insights", CreatePackageParameters(), "bench-score");
        _dependencyInspectorPayload = SerializeRequest("dependency_inspector", CreateDependencyInspectorParameters(), "bench-deps");

        _batchPayload = SerializeBatch(
            CreateRequest("batch-1", "search_packages", CreateSearchParameters()),
            CreateRequest("batch-2", "latest_version", CreatePackageParameters()),
            CreateRequest("batch-3", "check_compatibility", CreateCompatibilityParameters()),
            CreateRequest("batch-4", "list_versions", CreateListVersionsParameters()),
            CreateRequest("batch-5", "package_details", CreatePackageParameters()),
            CreateRequest("batch-6", "publisher_packages", CreatePublisherParameters()),
            CreateRequest("batch-7", "score_insights", CreatePackageParameters()),
            CreateRequest("batch-8", "dependency_inspector", CreateDependencyInspectorParameters()));
    }

    [GlobalCleanup]
    public void Cleanup()
        => Dispose();

    [Benchmark]
    public Task<string?> SearchPackages()
        => _pipeline.ExecuteAsync(_searchPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> LatestVersion()
        => _pipeline.ExecuteAsync(_latestVersionPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> CheckCompatibility()
        => _pipeline.ExecuteAsync(_compatibilityPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> ListVersions()
        => _pipeline.ExecuteAsync(_listVersionsPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> PackageDetails()
        => _pipeline.ExecuteAsync(_packageDetailsPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> PublisherPackages()
        => _pipeline.ExecuteAsync(_publisherPackagesPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> ScoreInsights()
        => _pipeline.ExecuteAsync(_scoreInsightsPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> DependencyInspector()
        => _pipeline.ExecuteAsync(_dependencyInspectorPayload, CancellationToken.None);

    [Benchmark]
    public Task<string?> BatchAcrossTools()
        => _pipeline.ExecuteAsync(_batchPayload, CancellationToken.None);

    public void Dispose()
    {
        _provider?.Dispose();
    }

    private string SerializeRequest(string method, JsonObject parameters, string id)
    {
        var node = CreateRequest(id, method, parameters);
        return node.ToJsonString(_serializerOptions);
    }

    private string SerializeBatch(params JsonObject[] requests)
    {
        var batch = new JsonArray();
        foreach (var request in requests)
        {
            batch.Add(request);
        }

        return batch.ToJsonString(_serializerOptions);
    }

    private static JsonObject CreateRequest(string id, string method, JsonObject parameters)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        };

    private static JsonObject CreateSearchParameters()
        => new()
        {
            ["query"] = "flutter http client",
            ["includePrerelease"] = false
        };

    private static JsonObject CreatePackageParameters()
        => new()
        {
            ["package"] = "riverpod"
        };

    private static JsonObject CreateCompatibilityParameters()
        => new()
        {
            ["package"] = "riverpod",
            ["flutterSdk"] = "3.24.0",
            ["projectConstraint"] = "^3.1.0"
        };

    private static JsonObject CreateListVersionsParameters()
        => new()
        {
            ["package"] = "riverpod",
            ["includePrerelease"] = true,
            ["take"] = 8
        };

    private static JsonObject CreatePublisherParameters()
        => new()
        {
            ["publisher"] = "dash.labs"
        };

    private static JsonObject CreateDependencyInspectorParameters()
        => new()
        {
            ["package"] = "riverpod",
            ["version"] = "3.1.0",
            ["includeDevDependencies"] = true
        };

    private sealed class ThroughputConfig : ManualConfig
    {
        public ThroughputConfig()
        {
            AddJob(Job.ShortRun
                .WithRuntime(CoreRuntime.Core90)
                .WithWarmupCount(2)
                .WithIterationCount(6)
                .WithMaxIterationCount(6)
                .WithId("short"));

            AddDiagnoser(MemoryDiagnoser.Default);
            AddColumn(StatisticColumn.P95);
        }
    }

    private sealed class FakePubDevApiClient : IPubDevApiClient
    {
        private static readonly VersionDetail StableRelease = VersionDetail.Create(
            "3.1.0",
            new DateTimeOffset(2024, 11, 15, 0, 0, 0, TimeSpan.Zero),
            ">=3.13.0 <4.0.0",
            false,
            new Uri("https://pub.dev/packages/riverpod/changelog"));

        private static readonly VersionDetail MaintenanceRelease = VersionDetail.Create(
            "3.0.4",
            new DateTimeOffset(2024, 9, 5, 0, 0, 0, TimeSpan.Zero),
            ">=3.10.0 <4.0.0",
            false,
            new Uri("https://pub.dev/packages/riverpod/changelog"));

        private static readonly VersionDetail BetaRelease = VersionDetail.Create(
            "3.2.0-beta.1",
            new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero),
            ">=3.15.0 <4.0.0",
            true,
            new Uri("https://pub.dev/packages/riverpod/changelog"));

        private static readonly IReadOnlyList<VersionDetail> VersionHistory = Array.AsReadOnly(new[]
        {
            BetaRelease,
            StableRelease,
            MaintenanceRelease
        });

        private static readonly PackageSummary RiverpodSummary = PackageSummary.Create(
            "riverpod",
            "A simple, compile-safe state-management library for Flutter",
            "dash.labs",
            likes: 3200,
            pubPoints: 140,
            popularity: 0.94,
            latestStable: StableRelease);

        private static readonly PackageSummary HooksSummary = PackageSummary.Create(
            "flutter_hooks",
            "Composable hooks for Flutter widgets",
            "dash.labs",
            likes: 2100,
            pubPoints: 130,
            popularity: 0.89,
            latestStable: MaintenanceRelease);

        private static readonly PackageSummary DioSummary = PackageSummary.Create(
            "dio",
            "Powerful HTTP client for Dart/Flutter",
            "flutterchina.dev",
            likes: 2600,
            pubPoints: 135,
            popularity: 0.91,
            latestStable: MaintenanceRelease);

        private static readonly PackageDetails RiverpodDetails = PackageDetails.Create(
            "riverpod",
            "A compile-safe, testable state-management solution for Flutter and Dart.",
            "dash.labs",
            new Uri("https://riverpod.dev"),
            new Uri("https://github.com/rrousselGit/riverpod"),
            new Uri("https://github.com/rrousselGit/riverpod/issues"),
            StableRelease,
            new[] { "flutter", "state-management", "riverpod" });

        private static readonly ScoreInsight RiverpodScore = ScoreInsight.Create(
            "riverpod",
            overallScore: 0.93,
            popularity: 0.91,
            likes: 3200,
            pubPoints: 140,
            componentNotes: new Dictionary<string, string>
            {
                ["popularity"] = "Extensive adoption across Flutter projects.",
                ["likes"] = "Strong community feedback and endorsement.",
                ["pubPoints"] = "Passes static analysis and documentation checks."
            },
            fetchedAt: DateTimeOffset.UtcNow);

        private static readonly DependencyGraph Dependencies = DependencyGraph.Create(
            rootPackage: "riverpod",
            rootVersion: StableRelease.Version,
            nodes: new[]
            {
                DependencyNode.Create(
                    package: "riverpod",
                    requested: StableRelease.Version,
                    resolved: StableRelease.Version,
                    isDirect: true,
                    children: new[]
                    {
                        DependencyNode.Create(
                            package: "flutter",
                            requested: ">=3.13.0",
                            resolved: "3.24.0",
                            isDirect: true,
                            children: Array.Empty<DependencyNode>()),
                        DependencyNode.Create(
                            package: "meta",
                            requested: ">=1.11.0",
                            resolved: "1.12.0",
                            isDirect: false,
                            children: Array.Empty<DependencyNode>())
                    })
            },
            issues: Array.Empty<string>());

        public Task<SearchResultSet> SearchPackagesAsync(string query, bool includePrerelease, string? sdkConstraint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = new List<PackageSummary> { RiverpodSummary, HooksSummary, DioSummary };
            return Task.FromResult(SearchResultSet.Create(query, results, "Refine your query for more packages."));
        }

        public Task<VersionDetail> GetLatestVersionAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureKnownPackage(package);
            var latestStable = VersionHistory.First(detail => !detail.IsPrerelease);
            return Task.FromResult(latestStable);
        }

        public Task<IReadOnlyList<VersionDetail>> GetVersionHistoryAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureKnownPackage(package);
            return Task.FromResult(VersionHistory);
        }

        public Task<PackageDetails> GetPackageDetailsAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureKnownPackage(package);
            return Task.FromResult(RiverpodDetails);
        }

        public Task<IReadOnlyList<PackageSummary>> GetPublisherPackagesAsync(string publisher, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(publisher, "dash.labs", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Publisher '{publisher}' not recognized in performance fixture.");
            }

            IReadOnlyList<PackageSummary> packages = Array.AsReadOnly(new[] { RiverpodSummary, HooksSummary });
            return Task.FromResult(packages);
        }

        public Task<ScoreInsight> GetScoreInsightAsync(string package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureKnownPackage(package);
            return Task.FromResult(RiverpodScore);
        }

        public Task<DependencyGraph> InspectDependenciesAsync(string package, string? version, bool includeDevDependencies, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureKnownPackage(package);

            if (!includeDevDependencies)
            {
                return Task.FromResult(Dependencies);
            }

            var devDependency = DependencyNode.Create(
                package: "lints",
                requested: ">=3.0.0",
                resolved: "3.0.1",
                isDirect: true,
                children: Array.Empty<DependencyNode>());

            var augmentedRoot = DependencyNode.Create(
                package: Dependencies.RootPackage,
                requested: Dependencies.RootVersion,
                resolved: Dependencies.RootVersion,
                isDirect: true,
                children: Dependencies.Nodes[0].Children.Append(devDependency));

            var augmentedGraph = DependencyGraph.Create(
                Dependencies.RootPackage,
                Dependencies.RootVersion,
                new[] { augmentedRoot },
                Dependencies.Issues);

            return Task.FromResult(augmentedGraph);
        }

        private static void EnsureKnownPackage(string package)
        {
            if (!string.Equals(package, "riverpod", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Package '{package}' not recognized in performance fixture.");
            }
        }
    }
}

internal static class PerformanceBudget
{
    public static void Assert(Summary summary, TimeSpan p95Budget)
    {
        ArgumentNullException.ThrowIfNull(summary);

        foreach (var report in summary.Reports)
        {
            var statistics = report.ResultStatistics;
            if (statistics is null)
            {
                continue;
            }

            var p95 = TimeSpan.FromNanoseconds(statistics.Percentiles.P95);
            if (p95 > p95Budget)
            {
                throw new InvalidOperationException($"Benchmark '{report.BenchmarkCase.Descriptor.WorkloadMethod.Name}' exceeded the p95 budget of {p95Budget.TotalSeconds:F2}s with {p95.TotalSeconds:F2}s.");
            }
        }
    }
}
