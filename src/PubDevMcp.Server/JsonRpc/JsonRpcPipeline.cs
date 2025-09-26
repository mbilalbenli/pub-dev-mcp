using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using PubDevMcp.Server.Tools;

namespace PubDevMcp.Server.JsonRpc;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via dependency injection.")]
internal sealed class JsonRpcPipeline
{
    private static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly IServiceProvider _services;
    private readonly IReadOnlyDictionary<string, McpToolDescriptor> _tools;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<JsonRpcPipeline> _logger;

    public JsonRpcPipeline(
        IServiceProvider services,
        IReadOnlyDictionary<string, McpToolDescriptor> tools,
        JsonSerializerOptions serializerOptions,
        ActivitySource activitySource,
        ILogger<JsonRpcPipeline> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> ExecuteAsync(string payload, CancellationToken cancellationToken)
    {
        JsonNode root;
        try
        {
            root = JsonNode.Parse(payload, NodeOptions)
                ?? throw new JsonRpcParseException("Request payload did not contain a JSON value.");
        }
        catch (JsonRpcException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON-RPC payload.");
            return JsonRpcResponse.Error(null, ex.ToError(), _serializerOptions).ToJsonString(_serializerOptions);
        }
        catch (JsonException ex)
        {
            var parseError = new JsonRpcParseException("Malformed JSON payload.", ex);
            _logger.LogWarning(ex, "Malformed JSON payload received.");
            return JsonRpcResponse.Error(null, parseError.ToError(), _serializerOptions).ToJsonString(_serializerOptions);
        }

        var responseNode = await ExecuteAsync(root, cancellationToken).ConfigureAwait(false);
        return responseNode?.ToJsonString(_serializerOptions);
    }

    public async Task<JsonNode?> ExecuteAsync(JsonNode node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node is JsonArray batch)
        {
            if (batch.Count == 0)
            {
                var error = new JsonRpcInvalidRequestException("Batch must not be empty.");
                return JsonRpcResponse.Error(null, error.ToError(), _serializerOptions);
            }

            var responses = new JsonArray();

            foreach (var entry in batch)
            {
                var response = await ExecuteSingleAsync(entry, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                {
                    responses.Add(response);
                }
            }

            return responses.Count == 0 ? null : responses;
        }

        return await ExecuteSingleAsync(node, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonNode?> ExecuteSingleAsync(JsonNode? node, CancellationToken cancellationToken)
    {
        if (node is null)
        {
            var error = new JsonRpcInvalidRequestException("Request value must be a JSON object.");
            return JsonRpcResponse.Error(null, error.ToError(), _serializerOptions);
        }

        JsonRpcRequest request;
        try
        {
            request = JsonRpcRequest.Parse(node);
        }
        catch (JsonRpcException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON-RPC request structure.");
            return JsonRpcResponse.Error(null, ex.ToError(), _serializerOptions);
        }

        if (!_tools.TryGetValue(request.Method, out var descriptor))
        {
            var error = new JsonRpcMethodNotFoundException(request.Method);
            _logger.LogWarning("JSON-RPC method {Method} not found.", request.Method);
            return request.IsNotification
                ? null
                : JsonRpcResponse.Error(request.Id, error.ToError(), _serializerOptions);
        }

        using var activity = _activitySource.StartActivity($"mcp.{descriptor.Name}", ActivityKind.Server);
        if (activity is not null)
        {
            activity.SetTag("rpc.system", "jsonrpc");
            activity.SetTag("rpc.service", "PubDevMcp");
            activity.SetTag("rpc.method", descriptor.Name);
            activity.SetTag("rpc.request.id", request.Id?.ToString());
        }

        try
        {
            var result = await descriptor.ExecuteAsync(request.Params, _services, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("rpc.status_code", "OK");

            if (request.IsNotification)
            {
                return null;
            }

            return JsonRpcResponse.Success(request.Id, result, _serializerOptions);
        }
        catch (JsonRpcException ex)
        {
            activity?.SetTag("rpc.status_code", "ERROR");
            activity?.SetTag("rpc.error_code", ex.Code);

            if (request.IsNotification && ex.SuppressResponseForNotifications)
            {
                _logger.LogWarning(ex, "JSON-RPC notification for method {Method} failed.", descriptor.Name);
                return null;
            }

            _logger.LogWarning(ex, "JSON-RPC method {Method} returned error {Code}.", descriptor.Name, ex.Code);
            return JsonRpcResponse.Error(request.Id, ex.ToError(), _serializerOptions);
        }
        catch (ValidationException ex)
        {
            activity?.SetTag("rpc.status_code", "INVALID_ARGUMENT");
            var error = JsonRpcInvalidParamsException.FromValidation(ex, _serializerOptions);
            _logger.LogWarning(ex, "Validation failed for JSON-RPC method {Method}.", descriptor.Name);
            return JsonRpcResponse.Error(request.Id, error.ToError(), _serializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("rpc.status_code", "CANCELLED");
            var error = new JsonRpcServerErrorException(-32001, "Request was cancelled.");
            return request.IsNotification ? null : JsonRpcResponse.Error(request.Id, error.ToError(), _serializerOptions);
        }
        catch (Exception ex)
        {
            activity?.SetTag("rpc.status_code", "EXCEPTION");
            _logger.LogError(ex, "Unhandled exception while processing JSON-RPC method {Method}.", descriptor.Name);
            var data = JsonValue.Create(ex.Message);
            var error = new JsonRpcInternalErrorException("Internal server error.", data);
            return request.IsNotification ? null : JsonRpcResponse.Error(request.Id, error.ToError(), _serializerOptions);
        }
    }
}

[SuppressMessage("Performance", "CA1812", Justification = "Constructed within JSON-RPC pipeline.")]
internal sealed class JsonRpcRequest
{
    private JsonRpcRequest(JsonNode? id, string method, JsonNode? parameters, bool isNotification)
    {
        Id = id;
        Method = method;
        Params = parameters;
        IsNotification = isNotification;
    }

    public JsonNode? Id { get; }

    public string Method { get; }

    public JsonNode? Params { get; }

    public bool IsNotification { get; }

    public static JsonRpcRequest Parse(JsonNode node)
    {
        if (node is not JsonObject obj)
        {
            throw new JsonRpcInvalidRequestException("JSON-RPC request must be an object.");
        }

        if (!obj.TryGetPropertyValue("jsonrpc", out var versionNode) || versionNode is not JsonValue versionValue || !versionValue.TryGetValue(out string? version) || version != "2.0")
        {
            throw new JsonRpcInvalidRequestException("jsonrpc member must equal '2.0'.");
        }

        if (!obj.TryGetPropertyValue("method", out var methodNode) || methodNode is not JsonValue methodValue || !methodValue.TryGetValue(out string? method) || string.IsNullOrWhiteSpace(method))
        {
            throw new JsonRpcInvalidRequestException("method member must be a non-empty string.");
        }

        obj.TryGetPropertyValue("params", out var paramsNode);
        var hasId = obj.TryGetPropertyValue("id", out var idNode);
        var idClone = hasId ? idNode?.DeepClone() : null;
        var paramsClone = paramsNode?.DeepClone();

        return new JsonRpcRequest(idClone, method, paramsClone, !hasId);
    }
}

internal static class JsonRpcResponse
{
    public static JsonNode Success(JsonNode? id, JsonNode? result, JsonSerializerOptions options)
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["result"] = result?.DeepClone() ?? JsonValue.Create((object?)null)
        };

        payload["id"] = id?.DeepClone() ?? JsonValue.Create((object?)null);
        return payload;
    }

    public static JsonNode Error(JsonNode? id, JsonRpcError error, JsonSerializerOptions options)
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["error"] = error.ToJsonNode(options)
        };

        payload["id"] = id?.DeepClone() ?? JsonValue.Create((object?)null);
        return payload;
    }
}

internal sealed record JsonRpcError(int Code, string Message, JsonNode? Data)
{
    public JsonNode ToJsonNode(JsonSerializerOptions options)
    {
        var error = new JsonObject
        {
            ["code"] = Code,
            ["message"] = Message
        };

        if (Data is not null)
        {
            error["data"] = Data.DeepClone();
        }

        return error;
    }
}

internal abstract class JsonRpcException : Exception
{
    protected JsonRpcException()
        : this(-32603, "JSON-RPC exception.")
    {
    }

    protected JsonRpcException(string message)
        : this(-32603, message)
    {
    }

    protected JsonRpcException(string message, Exception innerException)
        : this(-32603, message, innerException: innerException)
    {
    }

    protected JsonRpcException(int code, string message, JsonNode? data = null, bool suppressResponseForNotifications = false, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        ErrorData = data;
        SuppressResponseForNotifications = suppressResponseForNotifications;
    }

    public int Code { get; }

    public JsonNode? ErrorData { get; }

    public bool SuppressResponseForNotifications { get; }

    public JsonRpcError ToError()
        => new(Code, Message, ErrorData?.DeepClone());
}

internal sealed class JsonRpcParseException : JsonRpcException
{
    public JsonRpcParseException()
        : base(-32700, "Failed to parse JSON payload.")
    {
    }

    public JsonRpcParseException(string message)
        : base(-32700, message)
    {
    }

    public JsonRpcParseException(string message, Exception innerException)
        : base(-32700, message, innerException: innerException)
    {
    }
}

internal sealed class JsonRpcInvalidRequestException : JsonRpcException
{
    public JsonRpcInvalidRequestException()
        : base(-32600, "Invalid JSON-RPC request.")
    {
    }

    public JsonRpcInvalidRequestException(string message)
        : base(-32600, message)
    {
    }

    public JsonRpcInvalidRequestException(string message, Exception innerException)
        : base(-32600, message, innerException: innerException)
    {
    }
}

internal sealed class JsonRpcMethodNotFoundException : JsonRpcException
{
    public JsonRpcMethodNotFoundException()
        : base(-32601, "Method was not found.")
    {
    }

    public JsonRpcMethodNotFoundException(string method)
        : base(-32601, $"Method '{method}' was not found.")
    {
    }

    public JsonRpcMethodNotFoundException(string message, Exception innerException)
        : base(-32601, message, innerException: innerException)
    {
    }
}

internal sealed class JsonRpcInvalidParamsException : JsonRpcException
{
    public JsonRpcInvalidParamsException()
        : base(-32602, "Invalid parameters for JSON-RPC method.")
    {
    }

    public JsonRpcInvalidParamsException(string message)
        : base(-32602, message)
    {
    }

    public JsonRpcInvalidParamsException(string message, JsonNode data)
        : base(-32602, message, data)
    {
    }

    public JsonRpcInvalidParamsException(string message, Exception innerException)
        : base(-32602, message, innerException: innerException)
    {
    }

    public static JsonRpcInvalidParamsException FromValidation(ValidationException exception, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(options);

        var errors = new JsonArray();

        foreach (var failure in exception.Errors)
        {
            var error = new JsonObject
            {
                ["field"] = failure.PropertyName,
                ["message"] = failure.ErrorMessage
            };

            if (failure.AttemptedValue is not null)
            {
                var attemptedValue = JsonSerializer.SerializeToNode(failure.AttemptedValue, failure.AttemptedValue.GetType(), options);
                error["attemptedValue"] = attemptedValue;
            }

            errors.Add(error);
        }

        var data = new JsonObject
        {
            ["errors"] = errors
        };

        return new JsonRpcInvalidParamsException("Request validation failed.", data);
    }
}

internal sealed class JsonRpcInternalErrorException : JsonRpcException
{
    public JsonRpcInternalErrorException()
        : base(-32603, "Internal server error.")
    {
    }

    public JsonRpcInternalErrorException(string message, JsonNode? data = null)
        : base(-32603, message, data)
    {
    }

    public JsonRpcInternalErrorException(string message, Exception innerException)
        : base(-32603, message, innerException: innerException)
    {
    }

    public JsonRpcInternalErrorException(string message)
        : this(message, data: null)
    {
    }
}

internal sealed class JsonRpcServerErrorException : JsonRpcException
{
    public JsonRpcServerErrorException()
        : base(-32000, "Server error.")
    {
    }

    public JsonRpcServerErrorException(int code, string message, JsonNode? data = null, bool suppressNotifications = false)
        : base(code, message, data, suppressNotifications)
    {
    }

    public JsonRpcServerErrorException(string message)
        : base(-32000, message)
    {
    }

    public JsonRpcServerErrorException(string message, Exception innerException)
        : base(-32000, message, innerException: innerException)
    {
    }
}
