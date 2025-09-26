using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using PubDevMcp.Infrastructure.Options;

namespace PubDevMcp.Infrastructure.Policies;

public static class PubDevResiliencePolicies
{
    public static ResiliencePipeline<HttpResponseMessage> Create(PubDevResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = options.Timeout
        });

        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = options.RetryCount,
            DelayGenerator = args =>
            {
                var attempt = Math.Max(1, args.AttemptNumber);
                var exponential = Math.Pow(2, attempt - 1);
                var baseDelay = options.RetryBaseDelay.TotalMilliseconds * exponential;
                var jitterUpper = Math.Max(1, (int)options.RetryBaseDelay.TotalMilliseconds);
                var jitter = RandomNumberGenerator.GetInt32(0, jitterUpper);
                var maxDelay = options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, options.RetryCount);
                var delay = TimeSpan.FromMilliseconds(Math.Min(baseDelay + jitter, maxDelay));
                return new ValueTask<TimeSpan?>(delay);
            },
            ShouldHandle = args =>
            {
                if (args.Outcome.Exception is HttpRequestException or TimeoutRejectedException)
                {
                    return ValueTask.FromResult(true);
                }

                if (args.Outcome.Result is HttpResponseMessage response && IsTransient(response))
                {
                    return ValueTask.FromResult(true);
                }

                return ValueTask.FromResult(false);
            }
        });

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            BreakDuration = options.CircuitBreakerDuration,
            FailureRatio = 0.5,
            MinimumThroughput = options.CircuitBreakerFailures,
            SamplingDuration = options.CircuitBreakerWindow,
            ShouldHandle = args =>
            {
                if (args.Outcome.Exception is HttpRequestException or TimeoutRejectedException)
                {
                    return ValueTask.FromResult(true);
                }

                if (args.Outcome.Result is HttpResponseMessage response && IsTransient(response))
                {
                    return ValueTask.FromResult(true);
                }

                return ValueTask.FromResult(false);
            }
        });

        return builder.Build();
    }

    private static bool IsTransient(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        return statusCode >= 500 || response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout;
    }
}
