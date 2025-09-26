using System.Threading;
using System.Threading.Tasks;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Abstractions;

public interface ICacheService
{
    Task<ScoreInsight> GetScoreInsightAsync(string package, Func<CancellationToken, Task<ScoreInsight>> factory, CancellationToken cancellationToken);

    Task<DependencyGraph> GetDependencyGraphAsync(string package, string version, bool includeDevDependencies, Func<CancellationToken, Task<DependencyGraph>> factory, CancellationToken cancellationToken);
}
