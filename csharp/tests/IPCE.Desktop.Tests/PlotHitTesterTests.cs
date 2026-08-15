using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class PlotHitTesterTests
{
    [TestMethod]
    public void FindNearest_SelectsPixelNearestPointAcrossSeries()
    {
        PlotModel model = CreateModel(
            new PlotSeries(
                "near in data units",
                [1d],
                [1d],
                PlotSeriesKind.Line,
                "#1565C0"),
            new PlotSeries(
                "near on screen",
                [10d],
                [10d],
                PlotSeriesKind.Scatter,
                "#EF6C00"));

        PlotHoverPoint? hit = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(11, 10),
            (x, y) => x < 5
                ? new PlotPixelPoint(x * 100, y * 100)
                : new PlotPixelPoint(x, y));

        Assert.IsNotNull(hit);
        Assert.AreEqual("near on screen", hit.SeriesLabel);
        Assert.AreEqual(1, hit.SeriesIndex);
        Assert.AreEqual(0, hit.PointIndex);
        Assert.AreEqual(10d, hit.X);
        Assert.AreEqual(10d, hit.Y);
        Assert.AreEqual(1d, hit.PixelDistance, 1e-12);
        Assert.AreEqual(
            "near on screen\nX = 10\nY = 10",
            hit.Details);
    }

    [TestMethod]
    public void FindNearest_UsesInclusiveTwelvePixelRadius()
    {
        PlotModel model = CreateModel(
            new PlotSeries(
                "data",
                [0d],
                [0d],
                PlotSeriesKind.Line,
                "#1565C0"));
        Func<double, double, PlotPixelPoint> identity =
            (x, y) => new PlotPixelPoint(x, y);

        PlotHoverPoint? atBoundary = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(12, 0),
            identity);
        PlotHoverPoint? outside = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(12.01, 0),
            identity);

        Assert.IsNotNull(atBoundary);
        Assert.IsNull(outside);
    }

    [TestMethod]
    public void FindNearest_IsRepeatableAndDoesNotMutateModel()
    {
        double[] x = [1, 2];
        double[] y = [3, 4];
        PlotSeries series = new(
            "data",
            x,
            y,
            PlotSeriesKind.Line,
            "#1565C0",
            contributesToAutoRange: false);
        PlotModel model = CreateModel(series);
        x[0] = 99;
        y[0] = 99;

        PlotHoverPoint? first = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(1, 3),
            (dataX, dataY) => new PlotPixelPoint(dataX, dataY));
        PlotHoverPoint? second = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(1, 3),
            (dataX, dataY) => new PlotPixelPoint(dataX, dataY));

        Assert.AreEqual(first, second);
        Assert.AreEqual(1d, series.X[0]);
        Assert.AreEqual(3d, series.Y[0]);
    }

    [TestMethod]
    public void FindNearest_RejectsInvalidRadiusAndSkipsNonfinitePixels()
    {
        PlotModel model = CreateModel(
            new PlotSeries(
                "data",
                [1d],
                [2d],
                PlotSeriesKind.Line,
                "#1565C0"));

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => PlotHitTester.FindNearest(
                model,
                new PlotPixelPoint(0, 0),
                (x, y) => new PlotPixelPoint(x, y),
                0));
        PlotHoverPoint? hit = PlotHitTester.FindNearest(
            model,
            new PlotPixelPoint(0, 0),
            (_, _) => new PlotPixelPoint(double.NaN, 0));

        Assert.AreEqual("IPCE:InvalidHitTestRadius", exception.Code);
        Assert.IsNull(hit);
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
