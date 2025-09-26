using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PubDevMcp.Infrastructure.Logging;
using PubDevMcp.Infrastructure.Telemetry;
using PubDevMcp.Server.Configuration;
using PubDevMcp.Server.Transports;
using Serilog;

var bootstrapLogger = LoggingConfiguration.CreateBootstrapLogger();
Log.Logger = bootstrapLogger;

try
{
	var transport = ResolveTransport(args, Environment.GetEnvironmentVariable("MCP_TRANSPORT"));

	using var cancellationSource = new CancellationTokenSource();
	Console.CancelKeyPress += (_, eventArgs) =>
	{
		eventArgs.Cancel = true;
		cancellationSource.Cancel();
	};

	switch (transport)
	{
		case TransportMode.Http:
			await RunHttpAsync(args, cancellationSource.Token).ConfigureAwait(false);
			break;
		case TransportMode.Stdio:
		default:
			await RunStdioAsync(args, cancellationSource.Token).ConfigureAwait(false);
			break;
	}
}
catch (Exception ex)
{
	Log.Fatal(ex, "Host terminated unexpectedly");
	throw;
}
finally
{
	await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

static TransportMode ResolveTransport(string[] args, string? transportVariable)
{
	if (args.Any(arg => string.Equals(arg, "--http", StringComparison.OrdinalIgnoreCase)))
	{
		return TransportMode.Http;
	}

	if (args.Any(arg => string.Equals(arg, "--stdio", StringComparison.OrdinalIgnoreCase)))
	{
		return TransportMode.Stdio;
	}

	if (!string.IsNullOrWhiteSpace(transportVariable))
	{
		return transportVariable.Trim().ToUpperInvariant() switch
		{
			"HTTP" => TransportMode.Http,
			_ => TransportMode.Stdio
		};
	}

	return TransportMode.Stdio;
}

static async Task RunStdioAsync(string[] args, CancellationToken cancellationToken)
{
	var host = Host.CreateDefaultBuilder(args)
		.UseInfrastructureLogging()
		.UseInfrastructureTelemetry()
		.ConfigureServices((context, services) => ServiceConfiguration.Configure(services, context.Configuration))
		.Build();

	await host.StartAsync(cancellationToken).ConfigureAwait(false);

	try
	{
		var transport = host.Services.GetRequiredService<StdioTransport>();
		await transport.RunAsync(cancellationToken).ConfigureAwait(false);
	}
	finally
	{
		await host.StopAsync(cancellationToken).ConfigureAwait(false);
		await host.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
	}
}

static async Task RunHttpAsync(string[] args, CancellationToken cancellationToken)
{
	var builder = WebApplication.CreateBuilder(args);
	builder.Host.UseInfrastructureLogging();
	builder.Host.UseInfrastructureTelemetry();

	ServiceConfiguration.Configure(builder.Services, builder.Configuration);

	var app = builder.Build();

	HttpTransport.MapEndpoints(app);

	await app.RunAsync(cancellationToken).ConfigureAwait(false);
}

internal enum TransportMode
{
	Stdio,
	Http
}
