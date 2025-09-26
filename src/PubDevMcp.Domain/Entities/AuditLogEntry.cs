using System;

namespace PubDevMcp.Domain.Entities;

public sealed record AuditLogEntry
{
    private AuditLogEntry(DateTimeOffset timestamp, string tool, string requestDigest, string responseDigest)
    {
        Timestamp = timestamp;
        Tool = tool;
        RequestDigest = requestDigest;
        ResponseDigest = responseDigest;
    }

    public DateTimeOffset Timestamp { get; }

    public string Tool { get; }

    public string RequestDigest { get; }

    public string ResponseDigest { get; }

    public static AuditLogEntry Create(DateTimeOffset timestamp, string tool, string requestDigest, string responseDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseDigest);

        return new AuditLogEntry(
            timestamp,
            tool.Trim(),
            requestDigest.Trim(),
            responseDigest.Trim());
    }
}
