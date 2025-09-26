using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.LatestVersion;

public sealed record LatestVersionQuery(string Package) : IRequest<VersionDetail>;

public sealed class LatestVersionHandler : IRequestHandler<LatestVersionQuery, VersionDetail>
{
    private readonly IPubDevApiClient _apiClient;

    public LatestVersionHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<VersionDetail> Handle(LatestVersionQuery request, CancellationToken cancellationToken)
        => _apiClient.GetLatestVersionAsync(request.Package, cancellationToken);
}
