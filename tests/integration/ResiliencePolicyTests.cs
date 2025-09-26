using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Polly.CircuitBreaker;
using PubDevMcp.Infrastructure.Options;
using PubDevMcp.Infrastructure.Policies;

namespace PubDevMcp.Tests.Integration;

public sealed class ResiliencePolicyTests
{
    private readonly ResiliencePolicyHarness _harness = new();

    [Fact(DisplayName = "Resilience policies MUST retry transient pub.dev failures three times")]
    public async Task ResiliencePoliciesMustRetryTransientFailuresAsync()
    {
        var result = await _harness.ExecuteWithTransientFailuresAsync();

        result.AttemptCount.Should().Be(3, "FR-005 requires three retry attempts before surfacing errors");
        result.FinalOutcome.Should().Be(ResilienceOutcome.FailedAfterRetries);
    }

    [Fact(DisplayName = "Resilience policies MUST open circuit after repeated failures")]
    public async Task ResiliencePoliciesMustOpenCircuitAsync()
    {
        var circuitState = await _harness.ExecuteCircuitBreakerScenarioAsync();

        circuitState.Should().Be(ResilienceCircuitState.Open, "Circuit breaker should open to protect upstreams");
    }
}

internal sealed class ResiliencePolicyHarness
{
    public async Task<ResilienceExecutionResult> ExecuteWithTransientFailuresAsync()
    {
        var options = new PubDevResilienceOptions
        {
            RetryCount = 2,
            RetryBaseDelay = TimeSpan.FromMilliseconds(20),
            Timeout = TimeSpan.FromSeconds(1),
            CircuitBreakerFailures = 4,
            CircuitBreakerDuration = TimeSpan.FromSeconds(5),
            CircuitBreakerWindow = TimeSpan.FromSeconds(2)
        };

        var pipeline = PubDevResiliencePolicies.Create(options);

        var attempts = 0;
        HttpResponseMessage? finalResponse = null;

        await pipeline.ExecuteAsync(async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            finalResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            await Task.CompletedTask;
            return finalResponse;
        }, CancellationToken.None).ConfigureAwait(false);

        var outcome = finalResponse is { IsSuccessStatusCode: true }
            ? ResilienceOutcome.Succeeded
            : ResilienceOutcome.FailedAfterRetries;

        finalResponse?.Dispose();

        return new ResilienceExecutionResult(attempts, outcome);
    }

    public async Task<ResilienceCircuitState> ExecuteCircuitBreakerScenarioAsync()
    {
        var options = new PubDevResilienceOptions
        {
            RetryCount = 1,
            RetryBaseDelay = TimeSpan.FromMilliseconds(10),
            Timeout = TimeSpan.FromSeconds(1),
            CircuitBreakerFailures = 2,
            CircuitBreakerDuration = TimeSpan.FromSeconds(5),
            CircuitBreakerWindow = TimeSpan.FromSeconds(2)
        };

        var pipeline = PubDevResiliencePolicies.Create(options);

        async ValueTask<HttpResponseMessage> ExecuteFailureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        try
        {
            for (var attempt = 0; attempt < options.CircuitBreakerFailures; attempt++)
            {
                using var response = await pipeline.ExecuteAsync(ExecuteFailureAsync, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            return ResilienceCircuitState.Open;
        }

        try
        {
            using var response = await pipeline.ExecuteAsync(ExecuteFailureAsync, CancellationToken.None).ConfigureAwait(false);
            return ResilienceCircuitState.Closed;
        }
        catch (Exception)
        {
            return ResilienceCircuitState.Open;
        }
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Resilience execution result will be produced once policies are implemented")]
internal sealed record ResilienceExecutionResult(int AttemptCount, ResilienceOutcome FinalOutcome);

internal enum ResilienceOutcome
{
    FailedAfterRetries,
    Succeeded,
}

internal enum ResilienceCircuitState
{
    Closed,
    Open,
    HalfOpen,
}
