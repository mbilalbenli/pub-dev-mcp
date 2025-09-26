using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.PublisherPackages;

public sealed record PublisherPackagesQuery(string Publisher) : IRequest<IReadOnlyList<PackageSummary>>;

public sealed class PublisherPackagesHandler : IRequestHandler<PublisherPackagesQuery, IReadOnlyList<PackageSummary>>
{
    private readonly IPubDevApiClient _apiClient;

    public PublisherPackagesHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<PackageSummary>> Handle(PublisherPackagesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Publisher);

        var publisher = request.Publisher.Trim();

        var packages = await _apiClient
            .GetPublisherPackagesAsync(publisher, cancellationToken)
            .ConfigureAwait(false);

        if (packages.Count == 0)
        {
            throw new InvalidOperationException($"Publisher '{publisher}' has no packages.");
        }

        return packages;
    }
}
