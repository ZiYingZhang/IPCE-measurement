using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class PlotRenderingTests
{
    [TestMethod]
    public void Renderer_UsesExplicitViewportInsteadOfUnconditionalAutoscale()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var target = new ScottPlot.WPF.WpfPlot();
                PlotModel model = new(
                    "i-t",
                    "time",
                    "current",
                    [
                        new PlotSeries(
                            "trace",
                            [0d, 1d, 2d],
                            [-3e-4, -2e-5, -1e-5],
                            PlotSeriesKind.Line,
                            "#1565C0"),
                    ],
                    [
                        new PlotBand(0.1, 0.3, "暗电流区间", "#607D8B", 0.28),
                    ],
                    "empty");
                PlotViewport viewport = new(
                    -0.1,
                    2.1,
                    -2.5e-5,
                    -0.5e-5,
                    1);

                PlotModelRenderer.Render(target, model, viewport);

                ScottPlot.AxisLimits limits = target.Plot.Axes.GetLimits();
                Assert.AreEqual(viewport.MinimumX, limits.Left, 1e-12);
                Assert.AreEqual(viewport.MaximumX, limits.Right, 1e-12);
                Assert.AreEqual(viewport.MinimumY, limits.Bottom, 1e-12);
                Assert.AreEqual(viewport.MaximumY, limits.Top, 1e-12);
                ScottPlot.Plottables.VerticalLine[] boundaries = target.Plot
                    .GetPlottables<ScottPlot.Plottables.VerticalLine>()
                    .ToArray();
                Assert.AreEqual(2, boundaries.Length);
                Assert.IsTrue(boundaries.All(line => line.LineWidth == 3));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null)
        {
            throw failure;
        }
    }

    [TestMethod]
    public void Theme_UsesChineseCapableFontForEveryTextSurface()
    {
        ScottPlot.Plot plot = new();
        plot.Title("样品 i-t");
        plot.XLabel("时间 (s)");
        plot.YLabel("电流 (A)");

        PlotTheme.Apply(plot);

        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Title.Label.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Bottom.Label.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Left.Label.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Top.Label.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Right.Label.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Bottom.TickLabelStyle.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Left.TickLabelStyle.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Top.TickLabelStyle.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Axes.Right.TickLabelStyle.FontName);
        Assert.AreEqual(
            PlotTheme.PreferredChineseFont,
            plot.Legend.FontName);

        Assert.AreEqual(
            PlotTheme.TitleFontSize,
            plot.Axes.Title.Label.FontSize);
        Assert.AreEqual(
            PlotTheme.AxisLabelFontSize,
            plot.Axes.Bottom.Label.FontSize);
        Assert.AreEqual(
            PlotTheme.AxisLabelFontSize,
            plot.Axes.Left.Label.FontSize);
        Assert.AreEqual(
            PlotTheme.AxisLabelFontSize,
            plot.Axes.Top.Label.FontSize);
        Assert.AreEqual(
            PlotTheme.AxisLabelFontSize,
            plot.Axes.Right.Label.FontSize);
        Assert.AreEqual(
            PlotTheme.TickFontSize,
            plot.Axes.Bottom.TickLabelStyle.FontSize);
        Assert.AreEqual(
            PlotTheme.TickFontSize,
            plot.Axes.Left.TickLabelStyle.FontSize);
        Assert.AreEqual(
            PlotTheme.TickFontSize,
            plot.Axes.Top.TickLabelStyle.FontSize);
        Assert.AreEqual(
            PlotTheme.TickFontSize,
            plot.Axes.Right.TickLabelStyle.FontSize);
        Assert.AreEqual(
            PlotTheme.LegendFontSize,
            plot.Legend.FontSize);
        Assert.IsTrue(plot.Axes.Bottom.TickLabelStyle.FontSize >= 20);
        Assert.IsTrue(plot.Axes.Left.TickLabelStyle.FontSize >= 20);
        Assert.IsTrue(plot.Axes.Top.TickLabelStyle.FontSize >= 20);
        Assert.IsTrue(plot.Axes.Right.TickLabelStyle.FontSize >= 20);
        Assert.IsTrue(plot.Legend.FontSize >= 20);
        Assert.IsTrue(
            plot.Axes.Bottom.Label.FontSize >=
            1.2 * plot.Axes.Bottom.TickLabelStyle.FontSize);
        Assert.IsTrue(
            plot.Axes.Left.Label.FontSize >=
            1.2 * plot.Axes.Left.TickLabelStyle.FontSize);
        Assert.IsTrue(
            plot.Axes.Top.Label.FontSize >=
            1.2 * plot.Axes.Top.TickLabelStyle.FontSize);
        Assert.IsTrue(
            plot.Axes.Right.Label.FontSize >=
            1.2 * plot.Axes.Right.TickLabelStyle.FontSize);
        Assert.IsTrue(plot.Axes.Title.Label.FontSize >= 26);
        Assert.IsGreaterThanOrEqualTo(14d, PlotTheme.HoverFontSize);
        Assert.IsGreaterThanOrEqualTo(14d, PlotTheme.ToolbarFontSize);
    }

    [TestMethod]
    public void PlotSeries_CopiesInputsAndRejectsMismatchedOrNonfiniteData()
    {
        double[] x = [1, 2];
        double[] y = [3, 4];
        double[] errors = [0.1, 0.2];
        PlotSeries series = new(
            "测量值",
            x,
            y,
            PlotSeriesKind.Scatter,
            "#1565C0",
            errors);

        x[0] = 99;
        y[0] = 99;
        errors[0] = 99;

        Assert.AreEqual(1d, series.X[0]);
        Assert.AreEqual(3d, series.Y[0]);
        Assert.AreEqual(0.1d, series.YErrors![0]);

        IpceException mismatched = Assert.ThrowsExactly<IpceException>(
            () => new PlotSeries(
                "无效",
                [1d, 2d],
                [3d],
                PlotSeriesKind.Line,
                "#000000"));
        Assert.AreEqual("IPCE:InvalidPlotSeries", mismatched.Code);

        IpceException errorMismatch = Assert.ThrowsExactly<IpceException>(
            () => new PlotSeries(
                "无效",
                [1d, 2d],
                [3d, 4d],
                PlotSeriesKind.Scatter,
                "#000000",
                [0.1d]));
        Assert.AreEqual("IPCE:InvalidPlotSeries", errorMismatch.Code);

        IpceException nonfinite = Assert.ThrowsExactly<IpceException>(
            () => new PlotSeries(
                "无效",
                [1d, double.NaN],
                [3d, 4d],
                PlotSeriesKind.Line,
                "#000000"));
        Assert.AreEqual("IPCE:InvalidPlotSeries", nonfinite.Code);
    }

    [TestMethod]
    public void PlotBand_RejectsInvalidBounds()
    {
        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => new PlotBand(10, 10, "暗电流", "#90A4AE", 0.2));

        Assert.AreEqual("IPCE:InvalidPlotSeries", exception.Code);
    }

    [TestMethod]
    public void PlotModel_CopiesSeriesAndBands()
    {
        List<PlotSeries> series =
        [
            new PlotSeries(
                "测量值",
                [1d, 2d],
                [3d, 4d],
                PlotSeriesKind.Line,
                "#1565C0"),
        ];
        List<PlotBand> bands =
        [
            new PlotBand(1, 2, "选择范围", "#90A4AE", 0.2),
        ];

        PlotModel model = new(
            "样品 i-t",
            "时间 (s)",
            "电流 (A)",
            series,
            bands,
            "暂无数据");
        series.Clear();
        bands.Clear();

        Assert.AreEqual(1, model.Series.Count);
        Assert.AreEqual(1, model.Bands.Count);
    }

    [TestMethod]
    public void ViewSettings_RejectNonIncreasingLimits()
    {
        PlotViewSettings settings = new(
            MinimumX: 10,
            MaximumX: 10,
            MinimumY: null,
            MaximumY: null,
            LogarithmicX: false,
            LogarithmicY: false);

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => settings.Validate(CreatePositiveModel()));

        Assert.AreEqual("IPCE:InvalidAxisLimits", exception.Code);
    }

    [TestMethod]
    public void ViewSettings_RejectLogAxesWithNonpositiveDataOrLimits()
    {
        PlotModel nonpositive = new(
            "测试",
            "X",
            "Y",
            [
                new PlotSeries(
                    "数据",
                    [0d, 1d],
                    [1d, 2d],
                    PlotSeriesKind.Line,
                    "#1565C0"),
            ],
            [],
            "暂无数据");
        PlotViewSettings logarithmicX = new(
            null,
            null,
            null,
            null,
            LogarithmicX: true,
            LogarithmicY: false);

        IpceException dataException =
            Assert.ThrowsExactly<IpceException>(
                () => logarithmicX.Validate(nonpositive));
        Assert.AreEqual("IPCE:InvalidAxisLimits", dataException.Code);
        Assert.AreEqual(
            "数据或坐标范围包含非正值，不能使用对数轴。",
            dataException.Message);

        PlotViewSettings nonpositiveLimit = new(
            MinimumX: null,
            MaximumX: null,
            MinimumY: 0,
            MaximumY: 10,
            LogarithmicX: false,
            LogarithmicY: true);
        IpceException limitException =
            Assert.ThrowsExactly<IpceException>(
                () => nonpositiveLimit.Validate(CreatePositiveModel()));
        Assert.AreEqual("IPCE:InvalidAxisLimits", limitException.Code);
        Assert.AreEqual(
            "数据或坐标范围包含非正值，不能使用对数轴。",
            limitException.Message);
    }

    private static PlotModel CreatePositiveModel()
    {
        return new PlotModel(
            "测试",
            "X",
            "Y",
            [
                new PlotSeries(
                    "数据",
                    [1d, 2d],
                    [3d, 4d],
                    PlotSeriesKind.Line,
                    "#1565C0"),
            ],
            [],
            "暂无数据");
    }
}
