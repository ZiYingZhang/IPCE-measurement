using IPCE.Core.Domain;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.State;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class ResultPlotModelBuilderTests
{
    [TestMethod]
    public void BuildTrace_SeparatesPrimaryDiagnosticsDarkAndMeanLayers()
    {
        TraceData trace = new(
            [0d, 5d, 10d, 15d, 20d],
            [0d, 1d, 2d, 3d, 4d],
            TraceMetadata.Unknown);
        SchedulePreview preview = new(
            [
                new SchedulePoint(500, 0, 0, 10, "fixed-delay"),
                new SchedulePoint(600, 13, 10, 20, "anchor"),
            ],
            [new AnchorPoint(600, 13)],
            new CoveragePreview(0, 20, 0, 20, true, "covered"));
        TraceMeanResult[] means =
        [
            new TraceMeanResult(500, 1e-6, 4),
            new TraceMeanResult(600, 2e-6, 5),
        ];

        PlotModel enabled = ResultPlotModelBuilder.BuildTrace(
            "trace",
            trace,
            [new AnchorPoint(600, 13)],
            subtractDark: true,
            darkStartSeconds: 0.1,
            darkEndSeconds: 2,
            preview,
            averagingDurationSeconds: 4,
            means,
            new ResultStatus(ResultFreshness.Current, ""));
        PlotModel disabled = ResultPlotModelBuilder.BuildTrace(
            "trace",
            trace,
            [],
            subtractDark: false,
            darkStartSeconds: 0.1,
            darkEndSeconds: 2,
            preview: null,
            averagingDurationSeconds: 4,
            means: [],
            new ResultStatus(ResultFreshness.Missing, ""));

        Assert.IsTrue(enabled.Series[0].ContributesToAutoRange);
        Assert.IsFalse(enabled.Series[1].ContributesToAutoRange);
        Assert.AreEqual(1, enabled.Bands.Count);
        Assert.AreEqual(0.1, enabled.Bands[0].MinimumX);
        Assert.AreEqual(2d, enabled.Bands[0].MaximumX);
        Assert.AreEqual(0.14, enabled.Bands[0].Opacity, 1e-12);
        Assert.AreEqual("#9E9E9E", enabled.Bands[0].ColorHex);
        Assert.AreEqual(2, enabled.Intervals.Count);
        Assert.AreEqual(6d, enabled.Intervals[0].MinimumX);
        Assert.AreEqual(10d, enabled.Intervals[0].MaximumX);
        Assert.AreEqual(13d, enabled.Intervals[1].MinimumX);
        Assert.AreEqual(17d, enabled.Intervals[1].MaximumX);
        Assert.AreEqual(0, disabled.Bands.Count);
    }

    [TestMethod]
    public void BuildTrace_LabelsStaleMeanResults()
    {
        TraceData trace = new(
            [0d, 10d],
            [0d, 1d],
            TraceMetadata.Unknown);
        SchedulePreview preview = new(
            [new SchedulePoint(500, 0, 0, 10, "fixed-delay")],
            [],
            new CoveragePreview(0, 10, 0, 10, true, "covered"));

        PlotModel model = ResultPlotModelBuilder.BuildTrace(
            "trace",
            trace,
            [],
            subtractDark: false,
            darkStartSeconds: 0,
            darkEndSeconds: 1,
            preview,
            averagingDurationSeconds: 4,
            [new TraceMeanResult(500, 1e-6, 4)],
            new ResultStatus(ResultFreshness.Stale, "area changed"),
            TestLocalization.Chinese());

        StringAssert.Contains(model.Intervals[0].Label, "结果已过期");
        StringAssert.Contains(
            model.Intervals[0].HoverDetails,
            "状态：结果已过期");
    }

    [TestMethod]
    public void BuildSpectrumIntegration_FocusesOnCommonRequestedRange()
    {
        IReadOnlyList<SpectrumPoint> spectrum =
        [
            new SpectrumPoint(280, 1),
            new SpectrumPoint(300, 2),
            new SpectrumPoint(600, 3),
            new SpectrumPoint(4_000, 4),
        ];
        IReadOnlyList<IpceValue> ipce =
        [
            new IpceValue(300, 20),
            new IpceValue(600, 30),
        ];
        IntegrationResult result = new(
            new IntegrationSummary(300, 600, 1, 2, 2, "linear"),
            [
                new IntegrationCurvePoint(
                    300, 1, 20, 0.2, 1, 0.1, 0),
                new IntegrationCurvePoint(
                    600, 1, 30, 0.3, 1, 0.2, 1),
            ]);

        SpectrumPlotModels models =
            ResultPlotModelBuilder.BuildSpectrumIntegration(
                spectrum,
                ipce,
                result,
                300,
                600);

        Assert.AreEqual(1, models.Irradiance.Bands.Count);
        Assert.AreEqual(0.14, models.Irradiance.Bands[0].Opacity, 1e-12);
        Assert.AreEqual("#9E9E9E", models.Irradiance.Bands[0].ColorHex);
        Assert.AreEqual(1, models.SelectedIpce.Bands.Count);
        Assert.AreEqual(0.14, models.SelectedIpce.Bands[0].Opacity, 1e-12);
        Assert.AreEqual("#9E9E9E", models.SelectedIpce.Bands[0].ColorHex);
        Assert.AreEqual(
            300d,
            models.Irradiance.ViewportPolicy.PreferredMinimumX);
        Assert.AreEqual(
            600d,
            models.Irradiance.ViewportPolicy.PreferredMaximumX);
        Assert.AreEqual(
            300d,
            models.SelectedIpce.ViewportPolicy.PreferredMinimumX);
        Assert.AreEqual(
            600d,
            models.SelectedIpce.ViewportPolicy.PreferredMaximumX);
        Assert.AreEqual(
            300d,
            models.Cumulative.ViewportPolicy.PreferredMinimumX);
        Assert.AreEqual(
            600d,
            models.Cumulative.ViewportPolicy.PreferredMaximumX);
    }
}
