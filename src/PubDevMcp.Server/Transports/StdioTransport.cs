using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PubDevMcp.Server.JsonRpc;

namespace PubDevMcp.Server.Transports;

[SuppressMessage("Performance", "CA1812", Justification = "Activated through dependency injection.")]
internal sealed class StdioTransport
{
    private readonly JsonRpcPipeline _pipeline;
    private readonly ILogger<StdioTransport> _logger;

    public StdioTransport(JsonRpcPipeline pipeline, ILogger<StdioTransport> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var writer = new StreamWriter(output, Encoding.UTF8) { AutoFlush = true };

        _logger.LogInformation("STDIO transport started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string? response = null;

            try
            {
                response = await _pipeline.ExecuteAsync(line, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing STDIO request.");
                continue;
            }

            if (response is null)
            {
                continue;
            }

            await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("STDIO transport stopped.");
    }
}
