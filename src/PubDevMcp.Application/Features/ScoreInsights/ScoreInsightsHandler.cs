using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.ScoreInsights;

public sealed record ScoreInsightsQuery(string Package) : IRequest<ScoreInsight>;

public sealed class ScoreInsightsHandler : IRequestHandler<ScoreInsightsQuery, ScoreInsight>
{
    private readonly IPubDevApiClient _apiClient;

    public ScoreInsightsHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ScoreInsight> Handle(ScoreInsightsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Package);

        var package = request.Package.Trim();

        return await _apiClient
            .GetScoreInsightAsync(package, cancellationToken)
            .ConfigureAwait(false);
    }
}
