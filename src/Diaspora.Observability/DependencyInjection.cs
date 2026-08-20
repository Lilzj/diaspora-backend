using Diaspora.Platform.Modules.Contributors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Diaspora.Observability;

public static class DependencyInjection
{
    private const string ServiceName = "Diaspora.Api";

    /// <summary>
    /// Wires tracing and metrics, subscribing to whatever telemetry each module declares.
    /// <para>
    /// B2 hardcoded three activity source names here and nothing created an
    /// <c>ActivitySource</c> behind any of them, so the tracer subscribed to sources that
    /// could never emit. Sources now arrive from <see cref="IObservabilityContributor"/>
    /// implementations that own the live instruments, which also means a new module's traces
    /// appear without editing this file.
    /// </para>
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<IObservabilityContributor>? contributors = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        contributors ??= [];

        var activitySources = contributors
            .SelectMany(contributor => contributor.ActivitySources)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var meters = contributors
            .SelectMany(contributor => contributor.Meters)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
        var hasOtlp = !string.IsNullOrWhiteSpace(otlpEndpoint);

        // Console exporters are a development convenience and a production hazard: B2 fell
        // back to them whenever the OTLP endpoint was unset, which is the shipped default,
        // so a deployment that forgot the variable wrote every span and every metric to
        // stdout. An unconfigured endpoint now means no exporter unless one is asked for.
        var useConsole = !hasOtlp &&
            configuration.GetValue("OpenTelemetry:UseConsoleExporter", defaultValue: false);

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: configuration["OpenTelemetry:ServiceName"] ?? ServiceName,
                    serviceVersion: typeof(DependencyInjection).Assembly.GetName().Version?.ToString() ?? "1.0.0");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Health probes run every few seconds forever and say nothing about
                        // user-facing behaviour. Tracing them buys noise and storage.
                        options.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation();

                foreach (var source in activitySources)
                {
                    tracing.AddSource(source);
                }

                if (hasOtlp)
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint!));
                }
                else if (useConsole)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                foreach (var meter in meters)
                {
                    metrics.AddMeter(meter);
                }

                if (hasOtlp)
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint!));
                }
                else if (useConsole)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }

    /// <summary>
    /// Configures Serilog from configuration.
    /// <para>
    /// B2's version took an <see cref="IConfiguration"/> and never read it, so the
    /// <c>Serilog</c> section in <c>appsettings.json</c> did nothing and log levels could
    /// only be changed by a redeploy. <c>ReadFrom.Configuration</c> runs first; everything
    /// after it is a default that configuration can override.
    /// </para>
    /// </summary>
    public static void ConfigureSerilog(IConfiguration configuration, string environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        var logConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", ServiceName)
            .WriteTo.Console(
                outputTemplate: isDevelopment
                    ? "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}"
                    // Deployed hosts get the correlation id inline. Container logs are read
                    // through an aggregator that has no other way to group a request.
                    : "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture);

        // No file sink. B2 wrote to a relative "logs/" path — a directory the container's
        // non-root user cannot create, on a filesystem that disappears with the container.
        // stdout is the contract; the platform's log aggregator owns retention.

        Log.Logger = logConfig.CreateLogger();
    }
}
