using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;
using PubDevMcp.Server.Configuration;
using PubDevMcp.Server.JsonRpc;
using PubDevMcp.Server.Tools;

namespace PubDevMcp.Tests.Integration;

internal sealed class IntegrationTestFixture : IAsyncDisposable
{
    private readonly ActivityListener _listener;
    private readonly ServiceProvider _provider;

    private IntegrationTestFixture(
        ServiceProvider provider,
        JsonRpcPipeline pipeline,
        JsonSerializerOptions serializerOptions,
        ActivitySource activitySource,
        TestLogCollector logCollector,
        ILoggerFactory loggerFactory,
        ActivityListener listener)
    {
        _provider = provider;
        Pipeline = pipeline;
        SerializerOptions = serializerOptions;
        ActivitySource = activitySource;
        LogCollector = logCollector;
        LoggerFactory = loggerFactory;
        _listener = listener;
    }

    public JsonRpcPipeline Pipeline { get; }

    public JsonSerializerOptions SerializerOptions { get; }

    public ActivitySource ActivitySource { get; }

    public TestLogCollector LogCollector { get; }

    public ILoggerFactory LoggerFactory { get; }

    public static Task<IntegrationTestFixture> CreateAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PubDev:Api:BaseAddress"] = "https://integration.local/",
                ["PubDev:Api:UserAgent"] = "PubDevMcp.Tests.Integration/1.0",
                ["PubDev:Resilience:RetryCount"] = "0",
                ["MCP_TELEMETRY_EXPORTER"] = "NONE"
            })
            .Build();

        var services = new ServiceCollection();
        var logCollector = new TestLogCollector();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(logCollector);
        });

        var activitySource = new ActivitySource("PubDevMcp.Tests.Integration");
        services.AddSingleton(activitySource);

    ServiceConfiguration.Configure(services, configuration);

    services.AddSingleton<JsonSerializerOptions>(JsonSerializerOptionsProvider);
        services.AddSingleton<IPubDevApiClient, StubPubDevApiClient>();

        var provider = services.BuildServiceProvider(validateScopes: false);
        var pipeline = provider.GetRequiredService<JsonRpcPipeline>();
        var serializerOptions = provider.GetRequiredService<JsonSerializerOptions>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        var listener = new ActivityListener
        {
            ShouldListenTo = source => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> options) => ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        var fixture = new IntegrationTestFixture(provider, pipeline, serializerOptions, activitySource, logCollector, loggerFactory, listener);
        return Task.FromResult(fixture);
    }

    public static IReadOnlyCollection<string> GetToolNames()
        => McpTools.GetRegistry().Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    public string CreateRequestPayload(string method, JsonObject parameters, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(parameters);

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? Guid.NewGuid().ToString("N"),
            ["method"] = method,
            ["params"] = parameters.DeepClone()
        };

        return request.ToJsonString(SerializerOptions);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }

    private static JsonSerializerOptions JsonSerializerOptionsProvider(IServiceProvider provider)
        => McpTools.GetSerializerOptions();
}

internal sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public LogEntry? LastEntry => _entries.LastOrDefault();

    public ILogger CreateLogger(string categoryName) => new CollectorLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Record(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Enqueue(entry);
    }

    internal sealed record LogEntry(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object> Properties);

    private sealed class CollectorLogger : ILogger
    {
        private readonly TestLogCollector _collector;
        private readonly string _category;

        public CollectorLogger(TestLogCollector collector, string category)
        {
            _collector = collector;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
            {
                return;
            }

            var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (state is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                foreach (var (key, value) in kvps)
                {
                    properties[key] = value;
                }
            }

            if (Activity.Current is { } activity)
            {
                properties["traceId"] = activity.TraceId.ToString();
            }

            var message = formatter(state, exception);
            _collector.Record(new LogEntry(_category, logLevel, message, new Dictionary<string, object>(properties)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered through dependency injection for integration tests")]
internal sealed class StubPubDevApiClient : IPubDevApiClient
{
    private static readonly VersionDetail StableHttpVersion = VersionDetail.Create(
        "1.2.3",
        new DateTimeOffset(2024, 4, 18, 0, 0, 0, TimeSpan.Zero),
        ">=3.10.0 <4.0.0",
        isPrerelease: false,
        releaseNotesUrl: new Uri("https://example.com/http/1.2.3"));

    private static readonly VersionDetail PreviewHttpVersion = VersionDetail.Create(
        "1.3.0-beta.1",
        new DateTimeOffset(2024, 12, 5, 0, 0, 0, TimeSpan.Zero),
        ">=3.15.0 <4.0.0",
        isPrerelease: true,
        releaseNotesUrl: new Uri("https://example.com/http/1.3.0-beta.1"));

    private static readonly IReadOnlyList<VersionDetail> HttpVersionHistory = Array.AsReadOnly(new[]
    {
        PreviewHttpVersion,
        StableHttpVersion
    });

    private static readonly PackageSummary[] SearchPackages =
    {
        PackageSummary.Create("http", "Composable HTTP client for Dart", "dart-lang", 2500, 140, 0.92, StableHttpVersion),
        PackageSummary.Create("http_mock", "HTTP mocking utilities", "dash-labs", 420, 100, 0.78, StableHttpVersion)
    };

    private static readonly PackageDetails HttpPackageDetails = PackageDetails.Create(
        "http",
        "HTTP client library for Dart and Flutter applications.",
        "dart-lang",
        new Uri("https://example.com/http"),
        new Uri("https://example.com/http/repository"),
        new Uri("https://example.com/http/issues"),
        StableHttpVersion,
        new[] { "http", "network", "client" });

    private static readonly ScoreInsight HttpScoreInsight = ScoreInsight.Create(
        "http",
        overallScore: 0.91,
        popularity: 0.89,
        likes: 2500,
        pubPoints: 140,
        componentNotes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["popularity"] = "Widely adopted within the Flutter ecosystem.",
            ["pubPoints"] = "Passes all automated pub.dev quality checks.",
            ["likes"] = "Thousands of developers starred the package."
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
        issues: new[] { "No dependency conflicts detected." });

    public Task<SearchResultSet> SearchPackagesAsync(string query, bool includePrerelease, string? sdkConstraint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SearchResultSet.Create(query, SearchPackages, "Refine your query to see more results."));
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

        return Task.FromResult<IReadOnlyList<PackageSummary>>(Array.AsReadOnly(SearchPackages));
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
            throw new InvalidOperationException($"Package '{package}' is not part of the integration test fixture.");
        }
    }
}
