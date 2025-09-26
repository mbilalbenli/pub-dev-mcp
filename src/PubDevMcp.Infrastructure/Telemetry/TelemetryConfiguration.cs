using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PubDevMcp.Infrastructure.Telemetry;

public static class TelemetryConfiguration
{
    private const string ServiceName = "PubDevMcp";
    private const string ActivitySourceName = "PubDevMcp.Server";
    private const string MeterName = "PubDevMcp.Server";
    private const string TelemetryExporterKey = "MCP_TELEMETRY_EXPORTER";
    private const string TraceSampleRatioKey = "MCP_TRACE_SAMPLE_RATIO";

    public static IHostBuilder UseInfrastructureTelemetry(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices((context, services) =>
        {
            services.AddSingleton<ActivitySource>(_ => new ActivitySource(ActivitySourceName));
            services.AddSingleton<Meter>(_ => new Meter(MeterName));

            services.AddOpenTelemetry()
                .ConfigureResource(resourceBuilder => ConfigureResource(resourceBuilder, context))
                .WithTracing(tracing => ConfigureTracing(tracing, context.Configuration))
                .WithMetrics(metrics => ConfigureMetrics(metrics, context.Configuration));
        });
    }

    public static ActivitySource GetActivitySource(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<ActivitySource>();
    }

    public static Meter GetMeter(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<Meter>();
    }

    private static void ConfigureResource(ResourceBuilder builder, HostBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        builder.AddService(
            serviceName: ServiceName,
            serviceVersion: typeof(TelemetryConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName,
            serviceNamespace: "MCP");

        var environmentName = string.IsNullOrWhiteSpace(context.HostingEnvironment.EnvironmentName)
            ? "unknown"
            : context.HostingEnvironment.EnvironmentName;

        var hostName = string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "unknown"
            : Environment.MachineName;

        builder.AddAttributes(new[]
        {
            new KeyValuePair<string, object>("deployment.environment", environmentName),
            new KeyValuePair<string, object>("host.name", hostName)
        });
    }

    private static void ConfigureTracing(TracerProviderBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder
            .AddSource(ActivitySourceName)
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ResolveSampleRatio(configuration))));

        switch (ResolveExporter(configuration))
        {
            case TelemetryExporter.Console:
                builder.AddConsoleExporter();
                break;
            case TelemetryExporter.Otlp:
                builder.AddOtlpExporter(options => ConfigureOtlpOptions(options, configuration));
                break;
            case TelemetryExporter.None:
            default:
                break;
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.AddMeter(MeterName);
        builder.AddMeter("System.Runtime");

        switch (ResolveExporter(configuration))
        {
            case TelemetryExporter.Console:
                builder.AddConsoleExporter();
                break;
            case TelemetryExporter.Otlp:
                builder.AddOtlpExporter(options => ConfigureOtlpOptions(options, configuration));
                break;
            case TelemetryExporter.None:
            default:
                break;
        }
    }

    private static TelemetryExporter ResolveExporter(IConfiguration configuration)
    {
    var configured = configuration[TelemetryExporterKey] ?? Environment.GetEnvironmentVariable(TelemetryExporterKey);

        if (string.IsNullOrWhiteSpace(configured))
        {
            return TelemetryExporter.Console;
        }

        return configured.Trim().ToUpperInvariant() switch
        {
            "CONSOLE" => TelemetryExporter.Console,
            "OTLP" => TelemetryExporter.Otlp,
            "NONE" => TelemetryExporter.None,
            _ => TelemetryExporter.Console
        };
    }

    private static void ConfigureOtlpOptions(OtlpExporterOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            options.Endpoint = uri;
        }
    }

    private static double ResolveSampleRatio(IConfiguration configuration)
    {
        var configured = configuration[TraceSampleRatioKey] ?? Environment.GetEnvironmentVariable(TraceSampleRatioKey);
        if (double.TryParse(configured, out var value))
        {
            return Math.Clamp(value, 0d, 1d);
        }

        return 1d;
    }

    private enum TelemetryExporter
    {
        None,
        Console,
        Otlp
    }
}
