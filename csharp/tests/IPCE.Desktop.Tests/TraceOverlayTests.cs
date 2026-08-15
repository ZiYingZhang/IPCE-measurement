using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class TraceOverlayTests
{
    [TestMethod]
    public void BuildMeans_MatchesByWavelengthAndResolvesExactWindows()
    {
        SchedulePreview preview = CreatePreview(
            new SchedulePoint(500, 0, 0, 10, "fixed-delay"),
            new SchedulePoint(600, 13, 10, 20, "anchor"));
        TraceMeanResult[] means =
        [
            new TraceMeanResult(600, -2e-5, 7),
            new TraceMeanResult(500, -1e-5, 9),
        ];

        IReadOnlyList<PlotIntervalMarker> markers =
            TraceOverlayBuilder.BuildMeans(
                preview,
                4,
                means,
                TestLocalization.Chinese());

        Assert.AreEqual(2, markers.Count);
        Assert.AreEqual(6d, markers[0].MinimumX);
        Assert.AreEqual(10d, markers[0].MaximumX);
        Assert.AreEqual(-1e-5, markers[0].Y);
        Assert.AreEqual("平均电流", markers[0].Label);
        Assert.AreEqual("#EF6C00", markers[0].ColorHex);
        StringAssert.Contains(markers[0].HoverDetails, "波长：500 nm");
        StringAssert.Contains(
            markers[0].HoverDetails,
            "平均窗口：6–10 s");
        StringAssert.Contains(
            markers[0].HoverDetails,
            "平均电流：-1.000000E-005 A");
        StringAssert.Contains(markers[0].HoverDetails, "样本数：9");

        Assert.AreEqual(13d, markers[1].MinimumX);
        Assert.AreEqual(17d, markers[1].MaximumX);
        Assert.AreEqual(-2e-5, markers[1].Y);
        StringAssert.Contains(markers[1].HoverDetails, "样本数：7");
    }

    [TestMethod]
    public void BuildMeans_RejectsDuplicateOrUnmatchedWavelengths()
    {
        SchedulePreview preview = CreatePreview(
            new SchedulePoint(500, 0, 0, 10, "fixed-delay"));

        IpceException duplicate = Assert.ThrowsExactly<IpceException>(
            () => TraceOverlayBuilder.BuildMeans(
                preview,
                4,
                [
                    new TraceMeanResult(500, -1e-5, 9),
                    new TraceMeanResult(500, -2e-5, 8),
                ]));
        IpceException unmatched = Assert.ThrowsExactly<IpceException>(
            () => TraceOverlayBuilder.BuildMeans(
                preview,
                4,
                [new TraceMeanResult(600, -1e-5, 9)]));

        Assert.AreEqual("IPCE:InvalidTraceOverlay", duplicate.Code);
        Assert.AreEqual("IPCE:InvalidTraceOverlay", unmatched.Code);
    }

    [TestMethod]
    public void PlotModel_CopiesAndValidatesIntervals()
    {
        List<PlotIntervalMarker> intervals =
        [
            new PlotIntervalMarker(
                1,
                2,
                3,
                "平均电流",
                "#EF6C00",
                "details"),
        ];
        PlotModel model = new(
            "trace",
            "time",
            "current",
            [],
            [],
            "empty",
            intervals: intervals);
        intervals.Clear();

        Assert.AreEqual(1, model.Intervals.Count);
        IpceException invalid = Assert.ThrowsExactly<IpceException>(
            () => new PlotIntervalMarker(
                2,
                1,
                double.NaN,
                "invalid",
                "",
                ""));
        Assert.AreEqual("IPCE:InvalidTraceOverlay", invalid.Code);
    }

    private static SchedulePreview CreatePreview(
        params SchedulePoint[] points)
    {
        return new SchedulePreview(
            points,
            [],
            new CoveragePreview(0, 20, 0, 20, true, "covered"));
    }
}
