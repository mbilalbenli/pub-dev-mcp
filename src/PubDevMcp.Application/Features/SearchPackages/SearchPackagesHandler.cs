using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.SearchPackages;

public sealed record SearchPackagesQuery(string Query, bool IncludePrerelease = false, string? SdkConstraint = null) : IRequest<SearchResultSet>;

public sealed class SearchPackagesHandler : IRequestHandler<SearchPackagesQuery, SearchResultSet>
{
    private readonly IPubDevApiClient _apiClient;

    public SearchPackagesHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<SearchResultSet> Handle(SearchPackagesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = request.Query?.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var sdkConstraint = string.IsNullOrWhiteSpace(request.SdkConstraint)
            ? null
            : request.SdkConstraint.Trim();

        return await _apiClient
            .SearchPackagesAsync(query, request.IncludePrerelease, sdkConstraint, cancellationToken)
            .ConfigureAwait(false);
    }
}
