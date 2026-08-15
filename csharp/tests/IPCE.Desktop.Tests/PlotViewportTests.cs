using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class PlotViewportTests
{
    [TestMethod]
    public void Calculate_RobustModeKeepsMainTraceVisibleAndCountsLeakageSteps()
    {
        double[] normal = Enumerable.Range(0, 9_980)
            .Select(index => -2e-5 + index / 9_979d * 1e-5)
            .ToArray();
        double[] leakage = Enumerable.Range(0, 20)
            .Select(index => -3e-4 - index * 1e-7)
            .ToArray();
        double[] y = normal.Concat(leakage).ToArray();
        double[] x = Enumerable.Range(0, y.Length)
            .Select(index => (double)index)
            .ToArray();
        PlotModel model = CreateModel(
            new PlotSeries(
                "i-t",
                x,
                y,
                PlotSeriesKind.Line,
                "#1565C0"));

        PlotViewport robust = PlotViewportCalculator.Calculate(
            model,
            new PlotViewportPolicy(),
            PlotViewportMode.Robust);
        PlotViewport full = PlotViewportCalculator.Calculate(
            model,
            new PlotViewportPolicy(),
            PlotViewportMode.Full);

        Assert.IsTrue(robust.MinimumY > -1e-4);
        Assert.IsGreaterThanOrEqualTo(20, robust.ClippedYPointCount);
        Assert.IsTrue(full.MinimumY <= -3e-4);
        Assert.AreEqual(0, full.ClippedYPointCount);
    }

    [TestMethod]
    public void Calculate_HandlesConstantSeriesAndIgnoresNonRangeOverlay()
    {
        double[] x = [1, 2];
        double[] y = [5, 5];
        PlotSeries primary = new(
            "data",
            x,
            y,
            PlotSeriesKind.Line,
            "#1565C0");
        PlotSeries overlay = new(
            "overlay",
            [1d, 2d],
            [-100d, 100d],
            PlotSeriesKind.Line,
            "#EF6C00",
            contributesToAutoRange: false);
        PlotModel model = CreateModel(primary, overlay);

        x[0] = 99;
        y[0] = 99;
        PlotViewport viewport = PlotViewportCalculator.Calculate(
            model,
            new PlotViewportPolicy(),
            PlotViewportMode.Robust);

        Assert.IsTrue(viewport.MinimumY < 5);
        Assert.IsTrue(viewport.MaximumY > 5);
        Assert.IsTrue(viewport.MinimumX > 0);
        Assert.IsTrue(viewport.MaximumX < 3);
        Assert.AreEqual(0, viewport.ClippedYPointCount);
        Assert.AreEqual(1d, primary.X[0]);
        Assert.AreEqual(5d, primary.Y[0]);
    }

    [TestMethod]
    public void Calculate_PreferredXRangeFocusesSpectrumInBothModes()
    {
        PlotModel model = CreateModel(
            new PlotSeries(
                "spectrum",
                [280d, 300d, 450d, 600d, 4_000d],
                [1d, 2d, 3d, 4d, 100d],
                PlotSeriesKind.Line,
                "#00897B"));
        PlotViewportPolicy policy = new(
            PreferredMinimumX: 300,
            PreferredMaximumX: 600);

        foreach (PlotViewportMode mode in Enum.GetValues<PlotViewportMode>())
        {
            PlotViewport viewport =
                PlotViewportCalculator.Calculate(model, policy, mode);

            Assert.IsTrue(viewport.MinimumX < 300);
            Assert.IsTrue(viewport.MaximumX > 600);
            Assert.IsTrue(viewport.MinimumY > 0);
            Assert.IsTrue(viewport.MaximumY < 10);
        }
    }

    [TestMethod]
    public void Calculate_RejectsInvalidPolicy()
    {
        PlotModel model = CreateModel(
            new PlotSeries(
                "data",
                [1d, 2d],
                [3d, 4d],
                PlotSeriesKind.Line,
                "#1565C0"));

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => PlotViewportCalculator.Calculate(
                model,
                new PlotViewportPolicy(
                    LowerQuantile: 0.9,
                    UpperQuantile: 0.1),
                PlotViewportMode.Robust));

        Assert.AreEqual("IPCE:InvalidViewportPolicy", exception.Code);
    }

    private static PlotModel CreateModel(params PlotSeries[] series)
    {
        return new PlotModel(
            "test",
            "X",
            "Y",
            series,
            [],
            "empty");
    }
}
