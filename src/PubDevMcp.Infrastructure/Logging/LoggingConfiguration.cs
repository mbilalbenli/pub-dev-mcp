using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using SerilogLogger = Serilog.ILogger;

namespace PubDevMcp.Infrastructure.Logging;

public static class LoggingConfiguration
{
    private const string LogLevelConfigurationKey = "MCP_LOG_LEVEL";
    private const string LogsDirectoryName = "logs";
    private const string DiagnosticsFileTemplate = "diagnostics-.json";

    public static SerilogLogger CreateBootstrapLogger()
    {
        var levelSwitch = new LoggingLevelSwitch(ResolveLogLevel(null));

        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter(renderMessage: false))
            .CreateLogger();
    }

    public static IHostBuilder UseInfrastructureLogging(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseSerilog((context, services, configuration) =>
        {
            var levelSwitch = new LoggingLevelSwitch(ResolveLogLevel(context.Configuration));
            var logsDirectory = EnsureLogsDirectory(context.HostingEnvironment.ContentRootPath);

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "PubDevMcp")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .MinimumLevel.ControlledBy(levelSwitch)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.Console(new JsonFormatter(renderMessage: false))
                .WriteTo.File(
                    formatter: new JsonFormatter(renderMessage: false),
                    path: Path.Combine(logsDirectory, DiagnosticsFileTemplate),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true);
        });
    }

    private static LogEventLevel ResolveLogLevel(IConfiguration? configuration)
    {
        var configuredValue = configuration?[LogLevelConfigurationKey];
        var value = string.IsNullOrWhiteSpace(configuredValue)
            ? Environment.GetEnvironmentVariable(LogLevelConfigurationKey)
            : configuredValue;

        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;
    }

    private static string EnsureLogsDirectory(string contentRootPath)
    {
        var directory = Path.Combine(contentRootPath ?? AppContext.BaseDirectory, LogsDirectoryName);
        Directory.CreateDirectory(directory);
        return directory;
    }
}

internal static partial class InfrastructureLogMessages
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Pub.dev HTTP {Method} {Uri} started.")]
    internal static partial void PubDevRequestStarted(Microsoft.Extensions.Logging.ILogger logger, string method, string uri);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Pub.dev HTTP {Method} {Uri} completed in {ElapsedMilliseconds} ms with status code {StatusCode}.")]
    internal static partial void PubDevRequestCompleted(Microsoft.Extensions.Logging.ILogger logger, string method, string uri, double elapsedMilliseconds, int statusCode);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Pub.dev HTTP {Method} {Uri} failed after {Attempt} attempts. Status: {StatusCode}. Reason: {Reason}.")]
    internal static partial void PubDevRequestFailed(Microsoft.Extensions.Logging.ILogger logger, Exception exception, string method, string uri, int attempt, int? statusCode, string reason);
}
