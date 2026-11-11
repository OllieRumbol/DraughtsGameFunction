using DraughtsGameFunction.Extensions;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionService.Engine;
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

    // The allowed limits: 10 seconds at most, and 1 at least.
    [TestMethod]
    [DataRow("NormalMaxDepth", 1)]
    [DataRow("NormalTimeLimitMs", 10000)]
    [DataRow("HardTimeLimitMs", 10000)]
    [DataRow("HardTimeLimitMs", 1)]
    public async Task AddComputerPlayer_SettingAtItsLimit_StartsWithThatValue(string name, int value)
    {
        // Arrange
        using var host = BuildHost(new() { [$"Search:{name}"] = value.ToString() });

        // Act
        await host.StartAsync();
        var settings = host.Services.GetRequiredService<IOptions<SearchOptions>>().Value;

        // Assert
        Assert.AreEqual(value, ValueOf(settings, name));
    }

    // The deepest Normal setting is the search's own ply cap, so a setting that passes start-up never makes the
    // search throw during a game.
    [TestMethod]
    public async Task AddComputerPlayer_NormalMaxDepthAtSearchCap_StartsAndOneMoreStops()
    {
        // Arrange
        using var atCap = BuildHost(new() { ["Search:NormalMaxDepth"] = $"{GameTreeSearch.MaxPly}" });
        using var pastCap = BuildHost(new() { ["Search:NormalMaxDepth"] = $"{GameTreeSearch.MaxPly + 1}" });

        // Act
        await atCap.StartAsync();

        // Assert
        Assert.AreEqual(GameTreeSearch.MaxPly, atCap.Services.GetRequiredService<IOptions<SearchOptions>>().Value.NormalMaxDepth);
        await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => pastCap.StartAsync());
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

    private static int ValueOf(SearchOptions settings, string name) => name switch
    {
        nameof(SearchOptions.NormalMaxDepth) => settings.NormalMaxDepth,
        nameof(SearchOptions.NormalTimeLimitMs) => settings.NormalTimeLimitMs,
        nameof(SearchOptions.HardTimeLimitMs) => settings.HardTimeLimitMs,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown setting."),
    };

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
