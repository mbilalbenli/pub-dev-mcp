using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.DependencyInspector;

public sealed record DependencyInspectorQuery(string Package, string? Version, bool IncludeDevDependencies = false) : IRequest<DependencyGraph>;

public sealed class DependencyInspectorHandler : IRequestHandler<DependencyInspectorQuery, DependencyGraph>
{
    private readonly IPubDevApiClient _apiClient;

    public DependencyInspectorHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<DependencyGraph> Handle(DependencyInspectorQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Package);

        var package = request.Package.Trim();
        var version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim();

        return await _apiClient
            .InspectDependenciesAsync(package, version, request.IncludeDevDependencies, cancellationToken)
            .ConfigureAwait(false);
    }
}
