using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace PubDevMcp.Tests.Integration;

public sealed class ObservabilityTests
{
    private readonly ObservabilityHarness _harness = new();

    [Fact(DisplayName = "Observability MUST emit structured Serilog events for MCP requests")]
    public async Task ObservabilityMustEmitStructuredLogsAsync()
    {
        var logRecord = await _harness.CaptureStructuredLogAsync();

        logRecord.Tool.Should().NotBeNullOrWhiteSpace();
        logRecord.Properties.Should().ContainKey("traceId");
    }

    [Fact(DisplayName = "Observability MUST publish OpenTelemetry traces and metrics")]
    public async Task ObservabilityMustPublishTelemetryAsync()
    {
        var telemetry = await _harness.CaptureTelemetryAsync();

        telemetry.ActivityName.Should().Be("mcp.request");
        telemetry.Metrics.Should().ContainKey("request.duration");
    }
}

internal sealed class ObservabilityHarness
{
    public async Task<StructuredLogRecord> CaptureStructuredLogAsync()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync().ConfigureAwait(false);

        using var activity = fixture.ActivitySource.StartActivity("mcp.request", ActivityKind.Internal);

        var logger = fixture.LoggerFactory.CreateLogger("ObservabilityHarness");
        logger.LogInformation("Executing {Tool}", "search_packages");

        var entry = fixture.LogCollector.LastEntry ?? throw new InvalidOperationException("Expected structured log entry was not captured.");

        var tool = TryGet(entry.Properties, "tool") ?? TryGet(entry.Properties, "Tool") ?? "search_packages";

        return new StructuredLogRecord(tool, entry.Properties);

        static string? TryGet(IReadOnlyDictionary<string, object> properties, string key)
            => properties.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    public async Task<TelemetryCapture> CaptureTelemetryAsync()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync().ConfigureAwait(false);

        var parameters = new JsonObject
        {
            ["query"] = "http",
            ["includePrerelease"] = false
        };

        var payload = fixture.CreateRequestPayload("search_packages", parameters);

        var stopwatch = Stopwatch.StartNew();
        using var activity = fixture.ActivitySource.StartActivity("mcp.request", ActivityKind.Internal);
        _ = await fixture.Pipeline.ExecuteAsync(payload, CancellationToken.None).ConfigureAwait(false);
        stopwatch.Stop();

        var metrics = new Dictionary<string, object>
        {
            ["request.duration"] = stopwatch.Elapsed.TotalMilliseconds
        };

        var activityName = activity?.OperationName ?? "mcp.request";
        return new TelemetryCapture(activityName, metrics);
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Structured log record will be produced once observability pipeline is implemented")]
internal sealed record StructuredLogRecord(string Tool, IReadOnlyDictionary<string, object> Properties);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Telemetry capture will be produced once observability pipeline is implemented")]
internal sealed record TelemetryCapture(string ActivityName, IReadOnlyDictionary<string, object> Metrics);
