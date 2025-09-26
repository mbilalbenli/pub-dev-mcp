using System;
using System.Collections.Generic;
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
            .Bind(configuration.GetSection(PubDevApiSection))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PubDevResilienceOptions>()
            .Bind(configuration.GetSection(PubDevResilienceSection))
            .ValidateDataAnnotations()
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
}
