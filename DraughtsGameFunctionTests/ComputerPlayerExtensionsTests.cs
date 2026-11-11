using DraughtsGameFunction.Extensions;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionService.Instance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, test 7: the search settings bind from configuration, and a bad setting stops start-up
/// (plan section 2). Each test starts a small host with the same registration as Program.cs.
/// </summary>
[TestClass]
public class ComputerPlayerExtensionsTests
{
    [TestMethod]
    public async Task AddComputerPlayer_NoSearchSettings_UsesPlanDefaults()
    {
        // Arrange
        using var host = BuildHost([]);

        // Act
        await host.StartAsync();
        var settings = host.Services.GetRequiredService<IOptions<SearchOptions>>().Value;

        // Assert
        Assert.AreEqual(6, settings.NormalMaxDepth);
        Assert.AreEqual(500, settings.NormalTimeLimitMs);
        Assert.AreEqual(1000, settings.HardTimeLimitMs);
    }

    [TestMethod]
    public async Task AddComputerPlayer_SearchSettingsSet_BindsThem()
    {
        // Arrange
        using var host = BuildHost(new()
        {
            ["Search:NormalMaxDepth"] = "4",
            ["Search:NormalTimeLimitMs"] = "300",
            ["Search:HardTimeLimitMs"] = "800",
        });

        // Act
        await host.StartAsync();
        var settings = host.Services.GetRequiredService<IOptions<SearchOptions>>().Value;

        // Assert
        Assert.AreEqual(4, settings.NormalMaxDepth);
        Assert.AreEqual(300, settings.NormalTimeLimitMs);
        Assert.AreEqual(800, settings.HardTimeLimitMs);
    }

    // The largest allowed values: the search's ply cap, and 10 seconds.
    [TestMethod]
    [DataRow("NormalMaxDepth", "64")]
    [DataRow("NormalTimeLimitMs", "10000")]
    [DataRow("HardTimeLimitMs", "10000")]
    [DataRow("HardTimeLimitMs", "1")]
    public async Task AddComputerPlayer_SettingAtItsLimit_Starts(string name, string value)
    {
        // Arrange
        using var host = BuildHost(new() { [$"Search:{name}"] = value });

        // Act
        await host.StartAsync();

        // Assert
        Assert.IsNotNull(host.Services.GetRequiredService<IOptions<SearchOptions>>().Value);
    }

    [TestMethod]
    [DataRow("NormalMaxDepth", "0")]
    [DataRow("NormalMaxDepth", "65")]
    [DataRow("NormalTimeLimitMs", "0")]
    [DataRow("NormalTimeLimitMs", "10001")]
    [DataRow("HardTimeLimitMs", "-1")]
    [DataRow("HardTimeLimitMs", "10001")]
    public async Task AddComputerPlayer_SettingOutOfRange_StopsStartUp(string name, string value)
    {
        // Arrange
        using var host = BuildHost(new() { [$"Search:{name}"] = value });

        // Act and assert
        var exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());
        StringAssert.Contains(exception.Message, name);
    }

    [TestMethod]
    public async Task AddComputerPlayer_SettingNotANumber_StopsStartUp()
    {
        // Arrange
        using var host = BuildHost(new() { ["Search:HardTimeLimitMs"] = "one second" });

        // Act and assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.StartAsync());
    }

    // Plan section 3.3: production uses Random.Shared, because requests run at the same time.
    [TestMethod]
    public void AddComputerPlayer_Registered_UsesSharedRandomAndCreatesPlayer()
    {
        // Arrange
        using var host = BuildHost([]);

        // Act
        var random = host.Services.GetRequiredService<Random>();
        var player = host.Services.GetService<ComputerPlayer>();

        // Assert
        Assert.AreSame(Random.Shared, random);
        Assert.IsNotNull(player);
    }

    // No default sources, so environment variables on the machine cannot change a result.
    private static IHost BuildHost(Dictionary<string, string?> settings)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddComputerPlayer(builder.Configuration);

        return builder.Build();
    }
}
