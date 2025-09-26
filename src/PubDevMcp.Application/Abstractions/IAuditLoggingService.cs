using System.Threading;
using System.Threading.Tasks;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Abstractions;

public interface IAuditLoggingService
{
    Task<AuditLogEntry> LogAsync(string tool, string requestPayload, string responsePayload, CancellationToken cancellationToken);
}
