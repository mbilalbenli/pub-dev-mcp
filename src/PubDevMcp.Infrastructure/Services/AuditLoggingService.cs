using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Infrastructure.Services;

public sealed class AuditLoggingService : IAuditLoggingService
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly ILogger<AuditLoggingService> _logger;

    public AuditLoggingService(ILogger<AuditLoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AuditLogEntry> LogAsync(string tool, string requestPayload, string responsePayload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        cancellationToken.ThrowIfCancellationRequested();

        var requestDigest = ComputeDigest(requestPayload ?? string.Empty);
        var responseDigest = ComputeDigest(responsePayload ?? string.Empty);
        var timestamp = DateTimeOffset.UtcNow;

        var entry = AuditLogEntry.Create(timestamp, tool, requestDigest, responseDigest);

        _logger.LogInformation(
            "AuditLogEntry {@Audit}",
            new
            {
                entry.Timestamp,
                entry.Tool,
                entry.RequestDigest,
                entry.ResponseDigest
            });

        return Task.FromResult(entry);
    }

    private static string ComputeDigest(string payload)
    {
        var bytes = Utf8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
