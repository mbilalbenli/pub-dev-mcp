using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using DomainPackageDetails = PubDevMcp.Domain.Entities.PackageDetails;

namespace PubDevMcp.Application.Features.PackageDetails;

public sealed record PackageDetailsQuery(string Package) : IRequest<DomainPackageDetails>;

public sealed class PackageDetailsHandler : IRequestHandler<PackageDetailsQuery, DomainPackageDetails>
{
    private readonly IPubDevApiClient _apiClient;

    public PackageDetailsHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<DomainPackageDetails> Handle(PackageDetailsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Package);

        var package = request.Package.Trim();

        return await _apiClient
            .GetPackageDetailsAsync(package, cancellationToken)
            .ConfigureAwait(false);
    }
}
