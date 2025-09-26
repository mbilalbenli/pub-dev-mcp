using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

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
    public Task<StructuredLogRecord> CaptureStructuredLogAsync() => throw new NotImplementedException("Observability harness not implemented");

    public Task<TelemetryCapture> CaptureTelemetryAsync() => throw new NotImplementedException("Observability harness not implemented");
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Structured log record will be produced once observability pipeline is implemented")]
internal sealed record StructuredLogRecord(string Tool, IReadOnlyDictionary<string, object> Properties);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Telemetry capture will be produced once observability pipeline is implemented")]
internal sealed record TelemetryCapture(string ActivityName, IReadOnlyDictionary<string, object> Metrics);
