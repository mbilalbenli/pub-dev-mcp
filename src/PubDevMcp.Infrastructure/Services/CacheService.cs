using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Infrastructure.Services;

public sealed class CacheService : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _memoryCache;

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public Task<ScoreInsight> GetScoreInsightAsync(string package, Func<CancellationToken, Task<ScoreInsight>> factory, CancellationToken cancellationToken)
        => GetOrCreateAsync(BuildScoreKey(package), factory, cancellationToken);

    public Task<DependencyGraph> GetDependencyGraphAsync(string package, string version, bool includeDevDependencies, Func<CancellationToken, Task<DependencyGraph>> factory, CancellationToken cancellationToken)
        => GetOrCreateAsync(BuildDependencyKey(package, version, includeDevDependencies), factory, cancellationToken);

    private Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        return _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultTtl;

            cancellationToken.ThrowIfCancellationRequested();

            return await factory(cancellationToken).ConfigureAwait(false);
        })!;
    }

    private static string BuildScoreKey(string package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
    return $"score:{package.Trim().ToUpperInvariant()}";
    }

    private static string BuildDependencyKey(string package, string version, bool includeDevDependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
    var normalizedPackage = package.Trim().ToUpperInvariant();
    var normalizedVersion = version.Trim().ToUpperInvariant();
        var suffix = includeDevDependencies ? ":with-dev" : string.Empty;
        return $"deps:{normalizedPackage}:{normalizedVersion}{suffix}";
    }
}
