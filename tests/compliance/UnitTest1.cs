using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace PubDevMcp.Tests.Compliance;

public sealed class JsonRpcComplianceTests
{
    [Fact(DisplayName = "JSON-RPC responses MUST include the 2.0 version marker")]
    public async Task ResponsesMustReportJsonRpcVersion()
    {
        var request = new JsonRpcRequest(
            Id: "compliance-001",
            Method: "search_packages",
            Params: new { query = "http client" }
        );

        var response = await JsonRpcComplianceHarness.ExecuteAsync(request);

        response.JsonRpc.Should().Be("2.0", "JSON-RPC 2.0 responses must echo the 2.0 protocol version");
        response.Id.Should().Be(request.Id, "responses must correlate to the originating request id");
    }

    [Fact(DisplayName = "JSON-RPC invalid requests MUST map to error -32600")]
    public async Task InvalidRequestsMustMapToMinus32600Error()
    {
        var invalidPayload = new InvalidJsonRpcPayload(
            RawJson: "{ \"method\": \"search_packages\" }"
        );

        var error = await JsonRpcComplianceHarness.ExecuteInvalidRequestAsync(invalidPayload);

        error.JsonRpc.Should().Be("2.0", "error envelopes must continue to advertise JSON-RPC 2.0 compliance");
        error.Error.Code.Should().Be(-32600, "invalid requests must surface the standard -32600 JSON-RPC code");
        error.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Unknown methods MUST map to error -32601 (Method not found)")]
    public async Task UnknownMethodsMustMapToMinus32601Error()
    {
        var request = new JsonRpcRequest(
            Id: "compliance-unknown-method",
            Method: "nonexistent_method",
            Params: new { }
        );

        var error = await JsonRpcComplianceHarness.ExecuteUnknownMethodAsync(request);

        error.JsonRpc.Should().Be("2.0");
        error.Error.Code.Should().Be(-32601, "method-not-found must align with JSON-RPC guidance");
        error.Error.Message.Should().Contain("nonexistent_method");
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Harness will be instantiated during compliance execution")]
internal static class JsonRpcComplianceHarness
{
    public static Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request)
        => throw new NotImplementedException("JSON-RPC compliance harness not implemented");

    public static Task<JsonRpcErrorResponse> ExecuteInvalidRequestAsync(InvalidJsonRpcPayload payload)
        => throw new NotImplementedException("JSON-RPC compliance harness not implemented");

    public static Task<JsonRpcErrorResponse> ExecuteUnknownMethodAsync(JsonRpcRequest request)
        => throw new NotImplementedException("JSON-RPC compliance harness not implemented");
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Request envelope instantiated in future harness implementation")]
internal sealed record JsonRpcRequest(string Id, string Method, object? Params)
{
    public string JsonRpc => "2.0";
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Response envelope instantiated in future harness implementation")]
internal sealed record JsonRpcResponse(string JsonRpc, object? Result, JsonRpcError? Error, string? Id);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Error envelope instantiated in future harness implementation")]
internal sealed record JsonRpcErrorResponse(string JsonRpc, JsonRpcError Error, string? Id);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Error payload instantiated in future harness implementation")]
internal sealed record JsonRpcError(int Code, string Message, object? Data);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Invalid payload wrapper instantiated in future harness implementation")]
internal sealed record InvalidJsonRpcPayload(string RawJson);
