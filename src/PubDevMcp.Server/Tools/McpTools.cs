using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PubDevMcp.Application.Features.CheckCompatibility;
using PubDevMcp.Application.Features.DependencyInspector;
using PubDevMcp.Application.Features.LatestVersion;
using PubDevMcp.Application.Features.ListVersions;
using PubDevMcp.Application.Features.PackageDetails;
using PubDevMcp.Application.Features.PublisherPackages;
using PubDevMcp.Application.Features.ScoreInsights;
using PubDevMcp.Application.Features.SearchPackages;
using PubDevMcp.Domain.Entities;
using PubDevMcp.Server.JsonRpc;

namespace PubDevMcp.Server.Tools;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
internal sealed class McpToolAttribute : Attribute
{
    public McpToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string Description { get; }
}

internal static class McpTools
{
    private static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Lazy<IReadOnlyDictionary<string, McpToolDescriptor>> Registry = new(() => BuildRegistry(SerializerOptions));

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("search_packages", "Search pub.dev packages by keyword and return the top 10 results.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> SearchPackagesFactory =
        (options, metadata) => McpToolDescriptor.Create<SearchPackagesQuery, SearchResultSet>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("latest_version", "Retrieve the newest stable version for a package.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> LatestVersionFactory =
        (options, metadata) => McpToolDescriptor.Create<LatestVersionQuery, VersionDetail>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("check_compatibility", "Evaluate package compatibility for a Flutter SDK version.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> CheckCompatibilityFactory =
        (options, metadata) => McpToolDescriptor.Create<CheckCompatibilityQuery, CompatibilityResult>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("list_versions", "List package versions in descending order, optionally including prereleases.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> ListVersionsFactory =
        (options, metadata) => McpToolDescriptor.Create<ListVersionsQuery, IReadOnlyList<VersionDetail>>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("package_details", "Return package metadata, links, and latest stable release information.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> PackageDetailsFactory =
        (options, metadata) => McpToolDescriptor.Create<PackageDetailsQuery, PackageDetails>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("publisher_packages", "Retrieve packages owned by a specific publisher.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> PublisherPackagesFactory =
        (options, metadata) => McpToolDescriptor.Create<PublisherPackagesQuery, IReadOnlyList<PackageSummary>>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("score_insights", "Obtain overall and component score insights for a package.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> ScoreInsightsFactory =
        (options, metadata) => McpToolDescriptor.Create<ScoreInsightsQuery, ScoreInsight>(metadata, options);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("dependency_inspector", "Inspect the dependency graph for a package version.")]
    private static readonly Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> DependencyInspectorFactory =
        (options, metadata) => McpToolDescriptor.Create<DependencyInspectorQuery, DependencyGraph>(metadata, options);

    public static IReadOnlyDictionary<string, McpToolDescriptor> GetRegistry()
        => Registry.Value;

    public static JsonSerializerOptions GetSerializerOptions()
        => SerializerOptions;

    private static Dictionary<string, McpToolDescriptor> BuildRegistry(JsonSerializerOptions options)
    {
        var descriptors = new Dictionary<string, McpToolDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in typeof(McpTools).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.GetValue(null) is not Func<JsonSerializerOptions, McpToolAttribute, McpToolDescriptor> factory)
            {
                continue;
            }

            var attribute = field.GetCustomAttribute<McpToolAttribute>();
            if (attribute is null)
            {
                continue;
            }

            var descriptor = factory(options, attribute);
            descriptors[descriptor.Name] = descriptor;
        }

        return descriptors;
    }
}

internal sealed record McpToolDescriptor(
    string Name,
    string Description,
    Type RequestType,
    Type ResponseType,
    Func<JsonNode?, IServiceProvider, CancellationToken, Task<JsonNode?>> Executor)
{
    public Task<JsonNode?> ExecuteAsync(JsonNode? parameters, IServiceProvider services, CancellationToken cancellationToken)
        => Executor(parameters, services, cancellationToken);

    public static McpToolDescriptor Create<TRequest, TResponse>(
        McpToolAttribute metadata,
        JsonSerializerOptions serializerOptions,
        Func<JsonNode?, JsonSerializerOptions, TRequest>? binder = null)
        where TRequest : IRequest<TResponse>
    {
        binder ??= DefaultBinder<TRequest>;

        return new McpToolDescriptor(
            metadata.Name,
            metadata.Description,
            typeof(TRequest),
            typeof(TResponse),
            async (parameters, services, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(services);

                var mediator = services.GetRequiredService<IMediator>();
                var validator = services.GetService<IValidator<TRequest>>();

                var request = binder(parameters, serializerOptions);

                if (validator is not null)
                {
                    await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
                }

                var response = await mediator.Send(request, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToNode<TResponse>(response, serializerOptions);
            });
    }

    private static TRequest DefaultBinder<TRequest>(JsonNode? parameters, JsonSerializerOptions options)
    {
        if (parameters is null)
        {
            throw new JsonRpcInvalidParamsException("Tool parameters are required but were not provided.");
        }

        var request = parameters.Deserialize<TRequest>(options);
        return request ?? throw new JsonRpcInvalidParamsException("Unable to deserialize tool parameters.");
    }
}
