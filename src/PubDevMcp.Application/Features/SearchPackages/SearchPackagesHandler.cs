using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.SearchPackages;

public sealed record SearchPackagesQuery(string Query) : IRequest<SearchResultSet>;

public sealed class SearchPackagesHandler : IRequestHandler<SearchPackagesQuery, SearchResultSet>
{
    private readonly IPubDevApiClient _apiClient;

    public SearchPackagesHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<SearchResultSet> Handle(SearchPackagesQuery request, CancellationToken cancellationToken)
        => _apiClient.SearchPackagesAsync(request.Query, cancellationToken);
}
