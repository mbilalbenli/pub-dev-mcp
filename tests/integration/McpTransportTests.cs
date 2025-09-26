using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace PubDevMcp.Tests.Integration;

public sealed class McpTransportTests
{
    private readonly McpTransportHarness _harness = new();

    [Fact(DisplayName = "STDIO transport MUST complete capability negotiation")]
    public async Task StdioTransportNegotiatesCapabilitiesAsync()
    {
        var negotiation = await _harness.StartStdioAsync();

        negotiation.ProtocolVersion.Should().Be("2.0", "MCP STDIO transport must negotiate JSON-RPC 2.0");
        negotiation.Capabilities.Should().Contain("search_packages", "All registered tools must flow through capability negotiation");
    }

    [Fact(DisplayName = "HTTP transport MUST expose tool discovery endpoint")]
    public async Task HttpTransportPublishesToolDiscoveryAsync()
    {
        var discovery = await _harness.QueryHttpToolsEndpointAsync();

        discovery.StatusCode.Should().Be(200, "tool discovery endpoint should return HTTP 200 when transport is active");
        discovery.Tools.Should().Contain("latest_version", "HTTP transport must expose all MCP tools");
    }
}

internal sealed class McpTransportHarness
{
    public Task<McpNegotiationResult> StartStdioAsync() => throw new NotImplementedException("STDIO transport harness not implemented");

    public Task<McpToolDiscoveryResult> QueryHttpToolsEndpointAsync() => throw new NotImplementedException("HTTP transport harness not implemented");
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Negotiation result will be populated by transport harness during implementation")]
internal sealed record McpNegotiationResult(string ProtocolVersion, IReadOnlyCollection<string> Capabilities);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Tool discovery result will be populated by transport harness during implementation")]
internal sealed record McpToolDiscoveryResult(int StatusCode, IReadOnlyCollection<string> Tools);
