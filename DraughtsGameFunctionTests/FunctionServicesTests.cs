using System;
using System.Collections.Generic;
using DraughtsGameFunction;
using DraughtsGameFunction.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Both functions get their services through dependency injection. The worker creates a function class for each
/// call from the registered services, as <see cref="ActivatorUtilities"/> does here, with the same registrations as
/// Program.cs.
/// </summary>
[TestClass]
public class FunctionServicesTests
{
    [TestMethod]
    [DataRow(typeof(AutomatedPlayer))]
    [DataRow(typeof(PlayerTips))]
    public void CreateInstance_ProgramRegistrations_CreatesFunction(Type functionClass)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddComputerPlayer(configuration)
            .AddPlayerTips();
        using var provider = services.BuildServiceProvider();

        // Act
        var function = ActivatorUtilities.CreateInstance(provider, functionClass);

        // Assert
        Assert.IsInstanceOfType(function, functionClass);
    }
}
