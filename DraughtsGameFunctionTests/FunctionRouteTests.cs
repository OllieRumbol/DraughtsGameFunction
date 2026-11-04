using System;
using System.Reflection;
using DraughtsGameFunction;
using Microsoft.Azure.Functions.Worker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// The other tests call the functions directly, so they would still pass if a route changed.
/// The web app depends on these routes (spec section 6.1.1, item 1).
/// </summary>
[TestClass]
public class FunctionRouteTests
{
    [TestMethod]
    [DataRow(typeof(AutomatedPlayer), "GetNextMove")]
    [DataRow(typeof(PlayerTips), "GetPotentialMoves")]
    public void Run_IsPublishedOnTheAgreedRoute(Type functionClass, string expectedName)
    {
        var run = functionClass.GetMethod("Run");
        Assert.IsNotNull(run);

        var function = run.GetCustomAttribute<FunctionAttribute>();
        var trigger = run.GetParameters()[0].GetCustomAttribute<HttpTriggerAttribute>();
        Assert.IsNotNull(function);
        Assert.IsNotNull(trigger);

        Assert.AreEqual(expectedName, function.Name);
        Assert.AreEqual(AuthorizationLevel.Anonymous, trigger.AuthLevel);
        CollectionAssert.AreEqual(new[] { "post" }, trigger.Methods);
        Assert.IsNull(trigger.Route);
    }
}
