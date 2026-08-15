using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Scheduling;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class ScheduleBuilderTests
{
    [TestMethod]
    public void FixedDelay_BuildsConsecutiveWindows()
    {
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            [400, 500],
            AlignmentMode.FixedDelay,
            [],
            fixedStartTimeSeconds: 2,
            nominalDelaySeconds: 3);

        Assert.AreEqual(2, schedule.Count);
        AssertPoint(schedule[0], 400, 2, 2, 5, "fixed-delay");
        AssertPoint(schedule[1], 500, 5, 5, 8, "fixed-delay");
    }

    [TestMethod]
    public void SingleAnchor_UsesNominalDelayAroundAnchor()
    {
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            [400, 500, 600],
            AlignmentMode.Anchors,
            [new AnchorPoint(500, 20)],
            fixedStartTimeSeconds: 0,
            nominalDelaySeconds: 5);

        AssertPoint(
            schedule[0], 400, 15, 12.5, 17.5,
            "single-anchor+nominal-delay");
        AssertPoint(
            schedule[1], 500, 20, 17.5, 22.5,
            "single-anchor+nominal-delay");
        AssertPoint(
            schedule[2], 600, 25, 22.5, 27.5,
            "single-anchor+nominal-delay");
    }

    [TestMethod]
    public void SingleWavelengthAnchor_UsesNominalDelayWindow()
    {
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            [500],
            AlignmentMode.Anchors,
            [new AnchorPoint(500, 20)],
            fixedStartTimeSeconds: 0,
            nominalDelaySeconds: 5);

        Assert.AreEqual(1, schedule.Count);
        AssertPoint(
            schedule[0], 500, 20, 17.5, 22.5,
            "single-anchor+nominal-delay");
    }

    [TestMethod]
    public void MultipleAnchors_ReproducesRealReferenceTimes()
    {
        double[] wavelengths = Enumerable.Range(0, 161)
            .Select(index => 300d + 5 * index)
            .ToArray();
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            wavelengths,
            AlignmentMode.Anchors,
            [
                new AnchorPoint(370, 127),
                new AnchorPoint(400, 168),
                new AnchorPoint(500, 333),
                new AnchorPoint(885, 965),
            ],
            fixedStartTimeSeconds: 50,
            nominalDelaySeconds: 8);

        AssertClose(127, schedule.Single(
            point => point.WavelengthNm == 370).ReferenceTimeSeconds);
        AssertClose(168, schedule.Single(
            point => point.WavelengthNm == 400).ReferenceTimeSeconds);
        AssertClose(333, schedule.Single(
            point => point.WavelengthNm == 500).ReferenceTimeSeconds);
        AssertClose(965, schedule.Single(
            point => point.WavelengthNm == 885).ReferenceTimeSeconds);
        Assert.IsTrue(schedule
            .Zip(schedule.Skip(1), (left, right) =>
                right.ReferenceTimeSeconds > left.ReferenceTimeSeconds)
            .All(isIncreasing => isIncreasing));
    }

    [TestMethod]
    public void MultipleAnchors_ExtrapolatesUsingEndpointPairSlopes()
    {
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            [400, 500, 600],
            AlignmentMode.Anchors,
            [
                new AnchorPoint(450, 10),
                new AnchorPoint(550, 30),
            ],
            fixedStartTimeSeconds: 0,
            nominalDelaySeconds: 5);

        AssertClose(0, schedule[0].ReferenceTimeSeconds);
        AssertClose(20, schedule[1].ReferenceTimeSeconds);
        AssertClose(40, schedule[2].ReferenceTimeSeconds);
    }

    [TestMethod]
    public void DuplicateAnchors_ThrowStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            ScheduleBuilder.Build(
                [400, 500],
                AlignmentMode.Anchors,
                [
                    new AnchorPoint(450, 10),
                    new AnchorPoint(450, 20),
                ],
                fixedStartTimeSeconds: 0,
                nominalDelaySeconds: 5));

        Assert.AreEqual("IPCE:DuplicateAnchors", error.Code);
    }

    [TestMethod]
    public void NonMonotonicAnchorTimes_ThrowStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            ScheduleBuilder.Build(
                [400, 500, 600],
                AlignmentMode.Anchors,
                [
                    new AnchorPoint(400, 20),
                    new AnchorPoint(600, 10),
                ],
                fixedStartTimeSeconds: 0,
                nominalDelaySeconds: 5));

        Assert.AreEqual("IPCE:NonMonotonicSchedule", error.Code);
    }

    private static void AssertPoint(
        SchedulePoint actual,
        double wavelength,
        double reference,
        double start,
        double end,
        string source)
    {
        AssertClose(wavelength, actual.WavelengthNm);
        AssertClose(reference, actual.ReferenceTimeSeconds);
        AssertClose(start, actual.WindowStartSeconds);
        AssertClose(end, actual.WindowEndSeconds);
        Assert.AreEqual(source, actual.AlignmentSource);
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.AreEqual(expected, actual, 1e-12);
    }
}
