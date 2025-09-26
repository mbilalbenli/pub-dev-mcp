using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.ListVersions;

public sealed record ListVersionsQuery(string Package, bool IncludePrerelease = false, int Take = 50) : IRequest<IReadOnlyList<VersionDetail>>;

public sealed class ListVersionsHandler : IRequestHandler<ListVersionsQuery, IReadOnlyList<VersionDetail>>
{
    private readonly IPubDevApiClient _apiClient;

    public ListVersionsHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<VersionDetail>> Handle(ListVersionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Package);

        var package = request.Package.Trim();
        var take = Math.Clamp(request.Take, 1, 200);

        var versions = await _apiClient
            .GetVersionHistoryAsync(package, cancellationToken)
            .ConfigureAwait(false);

        var filtered = request.IncludePrerelease
            ? versions
            : versions.Where(static version => !version.IsPrerelease);

        var materialized = filtered
            .Take(take)
            .ToArray();

        if (materialized.Length == 0)
        {
            throw new InvalidOperationException($"No versions available for package '{package}'.");
        }

        return Array.AsReadOnly(materialized);
    }
}
