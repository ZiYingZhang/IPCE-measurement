using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class PlotControllerTests
{
    [TestMethod]
    public void NearestSample_ReturnsOriginalXValue()
    {
        var controller = new PlotController(
            [0, 1, 2, 4],
            [10, 20, 30, 40],
            "Time (s)",
            "Current (A)");

        double nearest = controller.FindNearestX(1.6);

        Assert.AreEqual(2d, nearest);
    }

    [TestMethod]
    public void InvalidLogarithmicLimits_AreRejected()
    {
        var controller = new PlotController(
            [0, 1, 2],
            [10, 20, 30],
            "Time (s)",
            "Current (A)");

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => controller.SetAxis(
                new PlotAxisSettings(0, 2, 10, 30, true, false)));

        Assert.AreEqual("IPCE:InvalidLogAxis", exception.Code);
    }

    [TestMethod]
    public void ResetAxis_RestoresExactDataLimits()
    {
        var controller = new PlotController(
            [1, 2, 4],
            [-3, 5, 2],
            "x",
            "y");
        controller.SetAxis(
            new PlotAxisSettings(1.5, 3, -1, 4, false, false));

        PlotAxisSettings reset = controller.ResetAxis();

        Assert.AreEqual(
            new PlotAxisSettings(1, 4, -3, 5, false, false),
            reset);
        Assert.AreEqual(reset, controller.Axis);
    }

    [TestMethod]
    public void NonIncreasingAxisLimits_AreRejected()
    {
        var controller = new PlotController(
            [1, 2],
            [3, 4],
            "x",
            "y");

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => controller.SetAxis(
                new PlotAxisSettings(2, 2, 3, 4, false, false)));

        Assert.AreEqual("IPCE:InvalidAxisLimits", exception.Code);
    }
}
