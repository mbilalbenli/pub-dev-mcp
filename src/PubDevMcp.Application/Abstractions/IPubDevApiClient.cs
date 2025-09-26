using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Abstractions;

public interface IPubDevApiClient
{
    Task<SearchResultSet> SearchPackagesAsync(string query, bool includePrerelease, string? sdkConstraint, CancellationToken cancellationToken);

    Task<VersionDetail> GetLatestVersionAsync(string package, CancellationToken cancellationToken);

    Task<IReadOnlyList<VersionDetail>> GetVersionHistoryAsync(string package, CancellationToken cancellationToken);

    Task<PackageDetails> GetPackageDetailsAsync(string package, CancellationToken cancellationToken);

    Task<IReadOnlyList<PackageSummary>> GetPublisherPackagesAsync(string publisher, CancellationToken cancellationToken);

    Task<ScoreInsight> GetScoreInsightAsync(string package, CancellationToken cancellationToken);

    Task<DependencyGraph> InspectDependenciesAsync(string package, string? version, bool includeDevDependencies, CancellationToken cancellationToken);
}
