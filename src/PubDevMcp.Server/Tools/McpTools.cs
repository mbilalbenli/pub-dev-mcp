using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
    private static readonly McpToolSerializerContext SerializerContext = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    private static JsonSerializerOptions SerializerOptions => SerializerContext.Options;

    private static readonly Lazy<IReadOnlyDictionary<string, McpToolDescriptor>> Registry = new(() => BuildRegistry(SerializerContext));

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("search_packages", "Search pub.dev packages by keyword and return the top 10 results.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> SearchPackagesFactory =
        (context, metadata) => CreateDescriptor<SearchPackagesQuery, SearchResultSet>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("latest_version", "Retrieve the newest stable version for a package.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> LatestVersionFactory =
        (context, metadata) => CreateDescriptor<LatestVersionQuery, VersionDetail>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("check_compatibility", "Evaluate package compatibility for a Flutter SDK version.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> CheckCompatibilityFactory =
        (context, metadata) => CreateDescriptor<CheckCompatibilityQuery, CompatibilityResult>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("list_versions", "List package versions in descending order, optionally including prereleases.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> ListVersionsFactory =
        (context, metadata) => CreateDescriptor<ListVersionsQuery, IReadOnlyList<VersionDetail>>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("package_details", "Return package metadata, links, and latest stable release information.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> PackageDetailsFactory =
        (context, metadata) => CreateDescriptor<PackageDetailsQuery, PackageDetails>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("publisher_packages", "Retrieve packages owned by a specific publisher.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> PublisherPackagesFactory =
        (context, metadata) => CreateDescriptor<PublisherPackagesQuery, IReadOnlyList<PackageSummary>>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("score_insights", "Obtain overall and component score insights for a package.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> ScoreInsightsFactory =
        (context, metadata) => CreateDescriptor<ScoreInsightsQuery, ScoreInsight>(context, metadata);

    [SuppressMessage("Performance", "CA1823", Justification = "Factory discovered via reflection.")]
    [McpTool("dependency_inspector", "Inspect the dependency graph for a package version.")]
    private static readonly Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> DependencyInspectorFactory =
        (context, metadata) => CreateDescriptor<DependencyInspectorQuery, DependencyGraph>(context, metadata);

    public static IReadOnlyDictionary<string, McpToolDescriptor> GetRegistry()
        => Registry.Value;

    public static JsonSerializerOptions GetSerializerOptions()
        => SerializerOptions;

    private static Dictionary<string, McpToolDescriptor> BuildRegistry(McpToolSerializerContext context)
    {
        var descriptors = new Dictionary<string, McpToolDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in typeof(McpTools).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.GetValue(null) is not Func<McpToolSerializerContext, McpToolAttribute, McpToolDescriptor> factory)
            {
                continue;
            }

            var attribute = field.GetCustomAttribute<McpToolAttribute>();
            if (attribute is null)
            {
                continue;
            }

            var descriptor = factory(context, attribute);
            descriptors[descriptor.Name] = descriptor;
        }

        return descriptors;
    }

    private static McpToolDescriptor CreateDescriptor<TRequest, TResponse>(
        McpToolSerializerContext context,
        McpToolAttribute metadata,
        Func<JsonNode?, JsonTypeInfo<TRequest>, TRequest>? binder = null)
        where TRequest : IRequest<TResponse>
    {
        var requestInfo = context.GetTypeInfo(typeof(TRequest)) as JsonTypeInfo<TRequest>
            ?? throw new InvalidOperationException($"Serializer context is missing type info for {typeof(TRequest).FullName}.");
        var responseInfo = context.GetTypeInfo(typeof(TResponse)) as JsonTypeInfo<TResponse>
            ?? throw new InvalidOperationException($"Serializer context is missing type info for {typeof(TResponse).FullName}.");
        return McpToolDescriptor.Create(metadata, requestInfo, responseInfo, binder);
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
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        Func<JsonNode?, JsonTypeInfo<TRequest>, TRequest>? binder = null)
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

                var request = binder(parameters, requestTypeInfo);

                if (validator is not null)
                {
                    await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
                }

                var response = await mediator.Send(request, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(response, responseTypeInfo);
            });
    }

    private static TRequest DefaultBinder<TRequest>(JsonNode? parameters, JsonTypeInfo<TRequest> typeInfo)
    {
        if (parameters is null)
        {
            throw new JsonRpcInvalidParamsException("Tool parameters are required but were not provided.");
        }

        var json = parameters.ToJsonString();
        var request = JsonSerializer.Deserialize(json, typeInfo);
        return request ?? throw new JsonRpcInvalidParamsException("Unable to deserialize tool parameters.");
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SearchPackagesQuery))]
[JsonSerializable(typeof(SearchResultSet))]
[JsonSerializable(typeof(LatestVersionQuery))]
[JsonSerializable(typeof(VersionDetail))]
[JsonSerializable(typeof(CheckCompatibilityQuery))]
[JsonSerializable(typeof(CompatibilityResult))]
[JsonSerializable(typeof(ListVersionsQuery))]
[JsonSerializable(typeof(IReadOnlyList<VersionDetail>))]
[JsonSerializable(typeof(PackageDetailsQuery))]
[JsonSerializable(typeof(PackageDetails))]
[JsonSerializable(typeof(PublisherPackagesQuery))]
[JsonSerializable(typeof(IReadOnlyList<PackageSummary>))]
[JsonSerializable(typeof(ScoreInsightsQuery))]
[JsonSerializable(typeof(ScoreInsight))]
[JsonSerializable(typeof(DependencyInspectorQuery))]
[JsonSerializable(typeof(DependencyGraph))]
internal partial class McpToolSerializerContext : JsonSerializerContext;
