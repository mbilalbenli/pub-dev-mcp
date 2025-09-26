using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PubDevMcp.Server.JsonRpc;

namespace PubDevMcp.Server.Transports;

internal static class HttpTransport
{
    public static void MapEndpoints(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/rpc", HandleRpcAsync)
            .WithName("Rpc")
            .WithDisplayName("JSON-RPC endpoint");

        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");
    }

    private static async Task HandleRpcAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cancellationToken = context.RequestAborted;
        var pipeline = context.RequestServices.GetRequiredService<JsonRpcPipeline>();
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("HttpTransport.Rpc");

        string payload;
        context.Request.EnableBuffering();

        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(payload))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("{\"error\":\"Request body cannot be empty.\"}", cancellationToken).ConfigureAwait(false);
            return;
        }

        string? response;
        try
        {
            response = await pipeline.ExecuteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception during JSON-RPC processing.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("{\"error\":\"Internal server error\"}", cancellationToken).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = "application/json";

        if (response is null)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await context.Response.WriteAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
