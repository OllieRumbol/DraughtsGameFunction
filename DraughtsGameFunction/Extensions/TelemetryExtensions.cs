using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DraughtsGameFunction.Extensions;

/// <summary>
/// In Azure, the worker sends its logs to Application Insights directly through OpenTelemetry, so named
/// placeholders reach customDimensions (plan section 5). Locally, with no connection string, the worker
/// relays its logs through the host, which prints them in the func start console.
/// </summary>
public static class TelemetryExtensions
{
    public const string ApplicationInsightsConnectionStringSettingName = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static IServiceCollection AddWorkerTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // UseFunctionsWorkerDefaults tells the host to stop relaying the worker's logs.
        // Without the exporter, the logs would go nowhere, so the two are only used together.
        if (!string.IsNullOrWhiteSpace(configuration[ApplicationInsightsConnectionStringSettingName]))
        {
            services.AddOpenTelemetry()
                .UseFunctionsWorkerDefaults()
                .UseAzureMonitorExporter(KeepEveryLog);
        }

        return services;
    }

    /// <summary>
    /// By default, the exporter keeps at most 5 traces per second, and drops a log whose trace was not kept.
    /// The response-time check reads every request's log line (plan section 5), so both are turned off.
    /// </summary>
    private static void KeepEveryLog(AzureMonitorExporterOptions options)
    {
        options.SamplingRatio = 1.0f;
        options.TracesPerSecond = null;
        options.EnableTraceBasedLogsSampler = false;
    }
}
