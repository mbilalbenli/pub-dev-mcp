using System;

namespace PubDevMcp.Infrastructure.Options;

public sealed class PubDevResilienceOptions
{
    public int RetryCount { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    public int CircuitBreakerFailures { get; set; } = 5;

    public TimeSpan CircuitBreakerWindow { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(15);
}
