using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Extraction;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class TraceExtractorTests
{
    [TestMethod]
    public void ExplicitDarkRange_IsSubtractedFromStableWindowMean()
    {
        TraceData trace = CreateTrace(
            [0, 0.5, 1, 2, 2.5, 3, 3.5, 4],
            [2, 2, 2, 8, 8, 12, 12, 12]);
        SchedulePoint[] schedule =
        [
            new(500, 2, 2, 4, "fixed-delay"),
        ];

        IReadOnlyList<ExtractedPoint> result = TraceExtractor.Extract(
            trace,
            schedule,
            averagingDurationSeconds: 1,
            new DarkCorrection(true, 0, 1));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(12, result[0].MeanCurrentAmperes, 1e-15);
        Assert.AreEqual(10, result[0].PhotoCurrentSignedAmperes, 1e-15);
        Assert.AreEqual(10, result[0].AbsolutePhotoCurrentAmperes, 1e-15);
        Assert.AreEqual(2, result[0].SampleCount);
    }

    [TestMethod]
    public void SignedPhotocurrent_IsPreservedAlongsideAbsoluteValue()
    {
        TraceData trace = CreateTrace(
            [0, 0.5, 1, 2, 2.5, 3, 3.5, 4],
            [2, 2, 2, -3, -3, -3, -3, -3]);
        SchedulePoint[] schedule =
        [
            new(500, 2, 2, 4, "fixed-delay"),
        ];

        ExtractedPoint point = TraceExtractor.Extract(
            trace,
            schedule,
            averagingDurationSeconds: 1,
            new DarkCorrection(true, 0, 1))[0];

        Assert.AreEqual(-5, point.PhotoCurrentSignedAmperes, 1e-15);
        Assert.AreEqual(5, point.AbsolutePhotoCurrentAmperes, 1e-15);
    }

    [TestMethod]
    public void MeasurementStandardError_UsesSampleStandardDeviation()
    {
        TraceData trace = CreateTrace(
            [0, 1, 2, 3, 4],
            [0, 0, 1, 3, 0]);
        SchedulePoint[] schedule =
        [
            new(500, 1, 1, 4, "fixed-delay"),
        ];

        ExtractedPoint point = TraceExtractor.Extract(
            trace,
            schedule,
            averagingDurationSeconds: 2,
            new DarkCorrection(false, 0, 0))[0];

        Assert.AreEqual(2, point.MeanCurrentAmperes, 1e-15);
        Assert.AreEqual(1, point.PhotoCurrentStandardErrorAmperes, 1e-15);
        Assert.AreEqual(2, point.SampleCount);
    }

    [TestMethod]
    public void AnchorWindow_AveragesForwardFromReferenceTime()
    {
        TraceData trace = CreateTrace(
            [0, 1, 2, 2.5, 3, 3.5, 4],
            [0, 0, 7, 9, 20, 20, 20]);
        SchedulePoint[] schedule =
        [
            new(500, 2, 1.5, 4, "piecewise-anchor"),
        ];

        ExtractedPoint point = TraceExtractor.Extract(
            trace,
            schedule,
            averagingDurationSeconds: 1,
            new DarkCorrection(false, 0, 0))[0];

        Assert.AreEqual(8, point.MeanCurrentAmperes, 1e-15);
        Assert.AreEqual(2, point.SampleCount);
    }

    [TestMethod]
    public void DarkRangeOutsideTrace_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TraceExtractor.Extract(
                CreateTrace([0, 1, 2], [0, 0, 1]),
                [new SchedulePoint(500, 1, 1, 2, "fixed-delay")],
                averagingDurationSeconds: 1,
                new DarkCorrection(true, -1, 0.5)));

        Assert.AreEqual("IPCE:DarkRangeOutsideTrace", error.Code);
    }

    [TestMethod]
    public void FewerThanTwoDarkSamples_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TraceExtractor.Extract(
                CreateTrace([0, 1, 2, 3], [0, 0, 1, 1]),
                [new SchedulePoint(500, 2, 2, 3, "fixed-delay")],
                averagingDurationSeconds: 1,
                new DarkCorrection(true, 0, 0.25)));

        Assert.AreEqual("IPCE:InsufficientDarkData", error.Code);
    }

    [TestMethod]
    public void EmptyAverageWindow_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TraceExtractor.Extract(
                CreateTrace([0, 1, 4], [0, 0, 1]),
                [new SchedulePoint(500, 2, 2, 3, "fixed-delay")],
                averagingDurationSeconds: 0.5,
                new DarkCorrection(false, 0, 0)));

        Assert.AreEqual("IPCE:EmptyWindow", error.Code);
    }

    [TestMethod]
    public void ScheduleOutsideTrace_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TraceExtractor.Extract(
                CreateTrace([0, 1, 2, 3], [0, 0, 1, 1]),
                [new SchedulePoint(500, 2, 2, 4, "fixed-delay")],
                averagingDurationSeconds: 1,
                new DarkCorrection(false, 0, 0)));

        Assert.AreEqual("IPCE:InsufficientCoverage", error.Code);
    }

    private static TraceData CreateTrace(
        IReadOnlyList<double> time,
        IReadOnlyList<double> current)
    {
        return new TraceData(time, current, TraceMetadata.Unknown);
    }
}
