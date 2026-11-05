using System.Collections.Generic;
using System.Linq;
using Azure.Monitor.OpenTelemetry.Exporter;
using DraughtsGameFunction.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Test 34: in Azure the worker exports its own logs and keeps every one. Locally it relays them
/// through the host. The tests read the options. They build no exporter and send nothing.
/// </summary>
[TestClass]
public class TelemetryExtensionsTests
{
    // Invented, in the connection string's shape. It is never used to send anything.
    private const string FakeConnectionString = "InstrumentationKey=fake-key;IngestionEndpoint=https://telemetry.invalid/";

    // When the worker sends the host this capability, the host stops relaying the worker's logs.
    private const string WorkerOpenTelemetryCapability = "WorkerOpenTelemetryEnabled";

    [TestMethod]
    public void AddWorkerTelemetry_ConnectionStringSet_KeepsEveryLog()
    {
        using var provider = BuildProvider(FakeConnectionString);

        var options = provider.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>().CurrentValue;

        Assert.AreEqual(1.0f, options.SamplingRatio);
        Assert.IsNull(options.TracesPerSecond);
        Assert.IsFalse(options.EnableTraceBasedLogsSampler);
    }

    // The worker exports its own logs, so the host must not relay them as well, or each log arrives twice.
    [TestMethod]
    public void AddWorkerTelemetry_ConnectionStringSet_TellsTheHostToStopRelayingLogs()
    {
        using var provider = BuildProvider(FakeConnectionString);

        var capabilities = provider.GetRequiredService<IOptions<WorkerOptions>>().Value.Capabilities;

        Assert.IsTrue(capabilities.TryGetValue(WorkerOpenTelemetryCapability, out var enabled));
        Assert.AreEqual(bool.TrueString, enabled);
    }

    // With the capability set and no exporter, no log would reach the local console.
    [TestMethod]
    public void AddWorkerTelemetry_NoConnectionString_LeavesTheHostRelayingLogsAndSetsUpNoExporter()
    {
        using var provider = BuildProvider(connectionString: null);

        var capabilities = provider.GetRequiredService<IOptions<WorkerOptions>>().Value.Capabilities;

        Assert.IsFalse(capabilities.ContainsKey(WorkerOpenTelemetryCapability));
        Assert.IsFalse(provider.GetServices<IConfigureOptions<AzureMonitorExporterOptions>>().Any());
    }

    private static ServiceProvider BuildProvider(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TelemetryExtensions.ApplicationInsightsConnectionStringSettingName] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddWorkerTelemetry(configuration);

        return services.BuildServiceProvider();
    }
}
