using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DraughtsGameFunction.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 5. A wrong value in host.json causes no error, so nothing else would find it.
/// </summary>
[TestClass]
public class HostJsonTests
{
    private static JsonElement ReadHostJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "host.json");

        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    [TestMethod]
    public void HostJson_Settings_MatchThePlan()
    {
        var hostJson = ReadHostJson();

        Assert.AreEqual("OpenTelemetry", hostJson.GetProperty("telemetryMode").GetString());
        Assert.AreEqual("00:05:00", hostJson.GetProperty("functionTimeout").GetString());
        Assert.AreEqual("Warning", hostJson.GetProperty("logging").GetProperty("logLevel").GetProperty("default").GetString());
    }

    // Without its entry, a function's request line disappears from the local console.
    [TestMethod]
    public void HostJson_LogLevels_KeepTheRequestLineForEveryFunction()
    {
        var logLevels = ReadHostJson().GetProperty("logging").GetProperty("logLevel");

        var functionNames = typeof(RequestLoggingMiddleware).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()?.Name)
            .OfType<string>()
            .ToArray();

        Assert.HasCount(2, functionNames);

        foreach (var functionName in functionNames)
        {
            Assert.AreEqual("Information", logLevels.GetProperty($"Function.{functionName}.User").GetString());
        }
    }
}
