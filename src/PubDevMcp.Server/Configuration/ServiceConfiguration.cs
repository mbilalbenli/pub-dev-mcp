using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentValidation;
using MediatR;
using Polly;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Application.Features.SearchPackages;
using PubDevMcp.Application.Validators;
using PubDevMcp.Infrastructure.Options;
using PubDevMcp.Infrastructure.Policies;
using PubDevMcp.Infrastructure.Services;
using PubDevMcp.Server.JsonRpc;
using PubDevMcp.Server.HealthChecks;
using PubDevMcp.Server.Tools;
using PubDevMcp.Server.Transports;

namespace PubDevMcp.Server.Configuration;

internal static class ServiceConfiguration
{
    private const string PubDevApiSection = "PubDev:Api";
    private const string PubDevResilienceSection = "PubDev:Resilience";
    private static readonly string[] ReadyTags = { "ready" };

    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

    services.AddSingleton(McpTools.GetSerializerOptions());
        services.AddSingleton<IReadOnlyDictionary<string, McpToolDescriptor>>(_ => McpTools.GetRegistry());

        services.AddSingleton<JsonRpcPipeline>();
            services.AddSingleton<StdioTransport>();

        services.AddHealthChecks()
            .AddCheck<PubDevHealthCheck>("pub.dev", tags: ReadyTags);

        services.AddOptions<PubDevApiOptions>()
            .Configure(options => ApplyPubDevApiOptions(configuration, options))
            .Validate(static options => options.BaseAddress is { IsAbsoluteUri: true }, "PubDev.Api.BaseAddress must be an absolute URI.")
            .Validate(static options => options.SearchResultLimit > 0, "PubDev.Api.SearchResultLimit must be greater than zero.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.UserAgent), "PubDev.Api.UserAgent must be specified.")
            .ValidateOnStart();

        services.AddOptions<PubDevResilienceOptions>()
            .Configure(options => ApplyPubDevResilienceOptions(configuration, options))
            .Validate(static options => options.RetryCount > 0, "RetryCount must be greater than zero.")
            .Validate(static options => options.RetryBaseDelay > TimeSpan.Zero, "RetryBaseDelay must be positive.")
            .Validate(static options => options.Timeout > TimeSpan.Zero, "Timeout must be positive.")
            .Validate(static options => options.CircuitBreakerFailures > 0, "CircuitBreakerFailures must be greater than zero.")
            .Validate(static options => options.CircuitBreakerWindow > TimeSpan.Zero, "CircuitBreakerWindow must be positive.")
            .Validate(static options => options.CircuitBreakerDuration > TimeSpan.Zero, "CircuitBreakerDuration must be positive.")
            .ValidateOnStart();

        services.AddMemoryCache();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SearchPackagesQuery>());
        services.AddValidatorsFromAssemblyContaining<SearchPackagesQueryValidator>();

        services.AddSingleton<IAuditLoggingService, AuditLoggingService>();
        services.AddSingleton<ICacheService, CacheService>();

        services.AddSingleton<ResiliencePipeline<HttpResponseMessage>>(sp =>
        {
            var resilienceOptions = sp.GetRequiredService<IOptions<PubDevResilienceOptions>>().Value;
            return PubDevResiliencePolicies.Create(resilienceOptions);
        });

        services.AddHttpClient("PubDevHealthCheck", (sp, client) =>
        {
            var apiOptions = sp.GetRequiredService<IOptions<PubDevApiOptions>>().Value;
            client.BaseAddress = apiOptions.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(apiOptions.UserAgent);
        });

        services.AddHttpClient<IPubDevApiClient, PubDevApiClient>((sp, client) =>
            {
                var apiOptions = sp.GetRequiredService<IOptions<PubDevApiOptions>>().Value;
                client.BaseAddress = apiOptions.BaseAddress;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(apiOptions.UserAgent);
            });
    }

    private static void ApplyPubDevApiOptions(IConfiguration configuration, PubDevApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var section = configuration.GetSection(PubDevApiSection);

        var baseAddress = section["BaseAddress"];
        if (!string.IsNullOrWhiteSpace(baseAddress) && Uri.TryCreate(baseAddress, UriKind.Absolute, out var parsedBaseAddress))
        {
            options.BaseAddress = parsedBaseAddress;
        }

        if (int.TryParse(section["SearchResultLimit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit > 0)
        {
            options.SearchResultLimit = limit;
        }

        var userAgent = section["UserAgent"];
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            options.UserAgent = userAgent;
        }
    }

    private static void ApplyPubDevResilienceOptions(IConfiguration configuration, PubDevResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var section = configuration.GetSection(PubDevResilienceSection);

        if (int.TryParse(section["RetryCount"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var retryCount) && retryCount > 0)
        {
            options.RetryCount = retryCount;
        }

        if (TimeSpan.TryParse(section["RetryBaseDelay"], CultureInfo.InvariantCulture, out var retryBaseDelay) && retryBaseDelay > TimeSpan.Zero)
        {
            options.RetryBaseDelay = retryBaseDelay;
        }

        if (TimeSpan.TryParse(section["Timeout"], CultureInfo.InvariantCulture, out var timeout) && timeout > TimeSpan.Zero)
        {
            options.Timeout = timeout;
        }

        if (int.TryParse(section["CircuitBreakerFailures"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var breakerFailures) && breakerFailures > 0)
        {
            options.CircuitBreakerFailures = breakerFailures;
        }

        if (TimeSpan.TryParse(section["CircuitBreakerWindow"], CultureInfo.InvariantCulture, out var breakerWindow) && breakerWindow > TimeSpan.Zero)
        {
            options.CircuitBreakerWindow = breakerWindow;
        }

        if (TimeSpan.TryParse(section["CircuitBreakerDuration"], CultureInfo.InvariantCulture, out var breakerDuration) && breakerDuration > TimeSpan.Zero)
        {
            options.CircuitBreakerDuration = breakerDuration;
        }
    }
}
