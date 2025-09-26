using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PubDevMcp.Infrastructure.ErrorHandling;

/// <summary>
/// Centralized exception handler that normalizes unhandled errors into JSON-RPC compliant responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var descriptor = JsonRpcErrorMapper.CreateDescriptor(httpContext, exception);

        LogException(descriptor, exception);

        httpContext.Response.StatusCode = descriptor.StatusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        var problem = descriptor.ToProblemDetail();
        var response = new JsonRpcErrorResponse(
            JsonRpcConstants.Version,
            new JsonRpcError(descriptor.JsonRpcCode, descriptor.Message, problem),
            descriptor.RequestId);

        await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                response,
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private void LogException(JsonRpcErrorDescriptor descriptor, Exception exception)
    {
        if (descriptor.LogLevel >= LogLevel.Error)
        {
            _logger.Log(
                descriptor.LogLevel,
                exception,
                "Unhandled exception for JSON-RPC request {TraceId}",
                descriptor.TraceId);
            return;
        }

        _logger.Log(
            descriptor.LogLevel,
            "JSON-RPC error {Code} for request {TraceId}: {Detail}",
            descriptor.JsonRpcCode,
            descriptor.TraceId,
            descriptor.Detail);
    }
}

/// <summary>
/// Provides helper functionality for translating exceptions into JSON-RPC errors.
/// </summary>
internal static class JsonRpcErrorMapper
{
    public static JsonRpcErrorDescriptor CreateDescriptor(HttpContext httpContext, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var instance = BuildInstance(httpContext);
        var requestId = JsonRpcContextItemAccessor.TryGetRequestId(httpContext);

        return exception switch
        {
            JsonRpcException rpcException => JsonRpcErrorDescriptor.FromJsonRpcException(rpcException, requestId, instance, traceId),
            ValidationException validationException => JsonRpcErrorDescriptor.FromValidationException(validationException, requestId, instance, traceId),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => JsonRpcErrorDescriptor.ForCancellation(requestId, instance, traceId),
            TimeoutException timeoutException => JsonRpcErrorDescriptor.ForTimeout(timeoutException, requestId, instance, traceId),
            HttpRequestException httpRequestException => JsonRpcErrorDescriptor.ForUpstreamFailure(httpRequestException, requestId, instance, traceId),
            _ => JsonRpcErrorDescriptor.ForInternalError(exception, requestId, instance, traceId)
        };
    }

    private static string BuildInstance(HttpContext context)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        return context.Request.QueryString.HasValue
            ? string.Concat(path, context.Request.QueryString.Value)
            : path;
    }

    internal static IReadOnlyDictionary<string, string[]> CreateValidationErrors(ValidationException exception)
    {
        var grouped = exception.Errors
            .Where(static failure => !string.IsNullOrWhiteSpace(failure.ErrorMessage))
            .GroupBy(
                failure => string.IsNullOrWhiteSpace(failure.PropertyName)
                    ? JsonRpcProblemDetail.RootErrorKey
                    : failure.PropertyName!,
                StringComparer.Ordinal);

        var dictionary = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var messages = group
                .Select(static failure => failure.ErrorMessage)
                .Where(static message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (messages.Length == 0)
            {
                continue;
            }

            dictionary[group.Key] = messages;
        }

        return dictionary;
    }
}

/// <summary>
/// Captures the normalized JSON-RPC error representation for downstream serialization.
/// </summary>
internal readonly record struct JsonRpcErrorDescriptor(
    int JsonRpcCode,
    int StatusCode,
    string Title,
    string Detail,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors,
    string? RequestId,
    string Instance,
    string TraceId,
    LogLevel LogLevel)
{
    public JsonRpcProblemDetail ToProblemDetail()
    {
        var instance = string.IsNullOrWhiteSpace(Instance) ? null : Instance;
        return new JsonRpcProblemDetail(Title, Detail, StatusCode, instance, Errors, TraceId);
    }

    public static JsonRpcErrorDescriptor FromJsonRpcException(JsonRpcException exception, string? requestId, string instance, string traceId)
        => new(
            exception.JsonRpcCode,
            exception.StatusCode,
            exception.Title,
            exception.Detail,
            exception.Message,
            exception.Errors,
            requestId,
            instance,
            traceId,
            exception.LogLevel);

    public static JsonRpcErrorDescriptor FromValidationException(ValidationException exception, string? requestId, string instance, string traceId)
    {
        var errors = JsonRpcErrorMapper.CreateValidationErrors(exception);

        return new JsonRpcErrorDescriptor(
            JsonRpcErrorCodes.InvalidParams,
            StatusCodes.Status400BadRequest,
            "Validation failed",
            "One or more validation errors occurred.",
            "Invalid parameters supplied.",
            errors,
            requestId,
            instance,
            traceId,
            LogLevel.Warning);
    }

    public static JsonRpcErrorDescriptor ForCancellation(string? requestId, string instance, string traceId)
        => new(
            JsonRpcErrorCodes.RequestCancelled,
            StatusCodes.Status408RequestTimeout,
            "Request cancelled",
            "The request was cancelled before completion.",
            "Request cancelled",
            null,
            requestId,
            instance,
            traceId,
            LogLevel.Warning);

    public static JsonRpcErrorDescriptor ForTimeout(Exception exception, string? requestId, string instance, string traceId)
        => new(
            JsonRpcErrorCodes.UpstreamFailure,
            StatusCodes.Status504GatewayTimeout,
            "Timeout waiting for upstream",
            exception.Message,
            "Timeout waiting for upstream dependency.",
            null,
            requestId,
            instance,
            traceId,
            LogLevel.Error);

    public static JsonRpcErrorDescriptor ForUpstreamFailure(HttpRequestException exception, string? requestId, string instance, string traceId)
        => new(
            JsonRpcErrorCodes.UpstreamFailure,
            StatusCodes.Status503ServiceUnavailable,
            "Upstream dependency failed",
            exception.Message,
            "Failed to reach upstream dependency.",
            null,
            requestId,
            instance,
            traceId,
            LogLevel.Error);

    public static JsonRpcErrorDescriptor ForInternalError(Exception exception, string? requestId, string instance, string traceId)
        => new(
            JsonRpcErrorCodes.InternalError,
            StatusCodes.Status500InternalServerError,
            "Internal server error",
            exception.Message,
            "An internal error occurred while processing the request.",
            null,
            requestId,
            instance,
            traceId,
            LogLevel.Error);
}

/// <summary>
/// Constants describing the standard JSON-RPC error codes used within the service.
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    public const int UpstreamFailure = -32002;
    public const int RequestCancelled = -32800;
}

/// <summary>
/// Base exception type for domain-specific JSON-RPC failures.
/// </summary>
public abstract class JsonRpcException : Exception
{
    protected JsonRpcException()
        : this("A JSON-RPC error occurred.")
    {
    }

    protected JsonRpcException(string message)
        : this(
            message,
            JsonRpcErrorCodes.InternalError,
            StatusCodes.Status500InternalServerError,
            "JSON-RPC error",
            message)
    {
    }

    protected JsonRpcException(string message, Exception innerException)
        : this(
            message,
            JsonRpcErrorCodes.InternalError,
            StatusCodes.Status500InternalServerError,
            "JSON-RPC error",
            message,
            null,
            LogLevel.Error,
            innerException)
    {
    }

    protected JsonRpcException(
        string message,
        int jsonRpcCode,
        int statusCode,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        LogLevel logLevel = LogLevel.Error,
        Exception? innerException = null)
        : base(message, innerException)
    {
        JsonRpcCode = jsonRpcCode;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors;
        LogLevel = logLevel;
    }

    public int JsonRpcCode { get; }

    public int StatusCode { get; }

    public string Title { get; }

    public string Detail { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public LogLevel LogLevel { get; }
}

/// <summary>
/// Exception describing invalid JSON-RPC requests.
/// </summary>
public sealed class JsonRpcInvalidRequestException : JsonRpcException
{
    private const string DefaultMessage = "The JSON-RPC request is invalid.";

    public JsonRpcInvalidRequestException()
        : this(DefaultMessage)
    {
    }

    public JsonRpcInvalidRequestException(string message)
        : this(message, message, null, null)
    {
    }

    public JsonRpcInvalidRequestException(string message, Exception innerException)
        : this(message, message, null, innerException)
    {
    }

    public JsonRpcInvalidRequestException(string message, string detail, IReadOnlyDictionary<string, string[]>? errors = null, Exception? innerException = null)
        : base(
            message,
            JsonRpcErrorCodes.InvalidRequest,
            StatusCodes.Status400BadRequest,
            "Invalid JSON-RPC request",
            detail,
            errors,
            LogLevel.Warning,
            innerException)
    {
    }

    public static JsonRpcInvalidRequestException FromDetail(string detail, IReadOnlyDictionary<string, string[]>? errors = null, Exception? innerException = null)
        => new(DefaultMessage, detail, errors, innerException);
}

/// <summary>
/// Exception raised when a requested JSON-RPC method is unknown.
/// </summary>
public sealed class JsonRpcMethodNotFoundException : JsonRpcException
{
    private const string DefaultMessage = "Requested method was not found.";
    private const string DefaultDetail = "The MCP server does not expose the requested method.";

    public JsonRpcMethodNotFoundException()
        : this(DefaultMessage, DefaultDetail, string.Empty, null)
    {
    }

    public JsonRpcMethodNotFoundException(string message)
        : this(message, message, string.Empty, null)
    {
    }

    public JsonRpcMethodNotFoundException(string message, Exception innerException)
        : this(message, message, string.Empty, innerException)
    {
    }

    public JsonRpcMethodNotFoundException(string message, string detail, string method, Exception? innerException = null)
        : base(
            message,
            JsonRpcErrorCodes.MethodNotFound,
            StatusCodes.Status404NotFound,
            "Method not found",
            detail,
            null,
            LogLevel.Warning,
            innerException)
    {
        Method = method;
    }

    public string Method { get; }

    public static JsonRpcMethodNotFoundException ForMethod(string method, Exception? innerException = null)
        => new(
            $"Method '{method}' not found.",
            $"The MCP server does not expose a method named '{method}'.",
            method,
            innerException);
}

/// <summary>
/// Exception describing invalid JSON-RPC parameters.
/// </summary>
public sealed class JsonRpcInvalidParamsException : JsonRpcException
{
    private const string DefaultMessage = "Invalid parameters supplied.";

    public JsonRpcInvalidParamsException()
        : this(DefaultMessage)
    {
    }

    public JsonRpcInvalidParamsException(string message)
        : this(message, message, null, null)
    {
    }

    public JsonRpcInvalidParamsException(string message, Exception innerException)
        : this(message, message, null, innerException)
    {
    }

    public JsonRpcInvalidParamsException(string message, string detail, IReadOnlyDictionary<string, string[]>? errors = null, Exception? innerException = null)
        : base(
            message,
            JsonRpcErrorCodes.InvalidParams,
            StatusCodes.Status400BadRequest,
            "Invalid parameters",
            detail,
            errors,
            LogLevel.Warning,
            innerException)
    {
    }

    public static JsonRpcInvalidParamsException FromDetail(string detail, IReadOnlyDictionary<string, string[]>? errors = null, Exception? innerException = null)
        => new(DefaultMessage, detail, errors, innerException);
}

/// <summary>
/// Exception raised when an upstream dependency fails.
/// </summary>
public sealed class JsonRpcUpstreamException : JsonRpcException
{
    private const string DefaultMessage = "Failed to reach upstream dependency.";
    private const int DefaultStatusCode = StatusCodes.Status503ServiceUnavailable;

    public JsonRpcUpstreamException()
        : this(DefaultMessage)
    {
    }

    public JsonRpcUpstreamException(string message)
        : this(message, message, DefaultStatusCode, null)
    {
    }

    public JsonRpcUpstreamException(string message, Exception innerException)
        : this(message, message, DefaultStatusCode, innerException)
    {
    }

    public JsonRpcUpstreamException(string message, string detail, int statusCode, Exception? innerException = null)
        : base(
            message,
            JsonRpcErrorCodes.UpstreamFailure,
            statusCode,
            "Upstream dependency failed",
            detail,
            null,
            LogLevel.Error,
            innerException)
    {
    }

    public JsonRpcUpstreamException(string detail, int statusCode)
        : this(DefaultMessage, detail, statusCode, null)
    {
    }

    public static JsonRpcUpstreamException FromDetail(string detail, Exception? innerException = null)
        => new(DefaultMessage, detail, DefaultStatusCode, innerException);
}

/// <summary>
/// Provides strongly-typed JSON-RPC error payloads.
/// </summary>
internal sealed record JsonRpcErrorResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("error")] JsonRpcError Error,
    [property: JsonPropertyName("id")] string? Id);

internal sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] JsonRpcProblemDetail Data);

internal sealed record JsonRpcProblemDetail(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("instance")] string? Instance,
    [property: JsonPropertyName("errors")] IReadOnlyDictionary<string, string[]>? Errors,
    [property: JsonPropertyName("traceId")] string TraceId)
{
    public const string RootErrorKey = "__root__";
}

internal static class JsonRpcConstants
{
    public const string Version = "2.0";
}

internal static class JsonRpcContextItemKeys
{
    public const string RequestId = "PubDevMcp.JsonRpc.RequestId";
}

internal static class JsonRpcContextItemAccessor
{
    public static string? TryGetRequestId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Items.TryGetValue(JsonRpcContextItemKeys.RequestId, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
