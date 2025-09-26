using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace PubDevMcp.Server.HealthChecks;

[SuppressMessage("Performance", "CA1812", Justification = "Registered with health check builder.")]
internal sealed class PubDevHealthCheck : IHealthCheck
{
    private static readonly Uri ProbeUri = new("/api/packages?q=health-check", UriKind.Relative);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PubDevHealthCheck> _logger;

    public PubDevHealthCheck(IHttpClientFactory httpClientFactory, ILogger<PubDevHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PubDevHealthCheck");
            using var request = new HttpRequestMessage(HttpMethod.Get, ProbeUri);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("pub.dev responded successfully.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return HealthCheckResult.Degraded("pub.dev rate limited the request.");
            }

            return HealthCheckResult.Unhealthy($"pub.dev responded with status {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Health check cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reach pub.dev during health check.");
            return HealthCheckResult.Unhealthy("Failed to reach pub.dev.", ex);
        }
    }
}
