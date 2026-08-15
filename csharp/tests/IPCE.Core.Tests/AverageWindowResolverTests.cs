using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Extraction;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class AverageWindowResolverTests
{
    [TestMethod]
    public void Resolve_FixedDelayUsesEndOfWindow()
    {
        SchedulePoint point = new(
            500,
            0,
            0,
            10,
            "fixed-delay");

        (double start, double end) =
            AverageWindowResolver.Resolve(point, 4);

        Assert.AreEqual(6d, start);
        Assert.AreEqual(10d, end);
    }

    [TestMethod]
    public void Resolve_AnchorUsesReferenceTime()
    {
        SchedulePoint point = new(
            500,
            3,
            0,
            10,
            "anchor");

        (double start, double end) =
            AverageWindowResolver.Resolve(point, 4);

        Assert.AreEqual(3d, start);
        Assert.AreEqual(7d, end);
    }

    [TestMethod]
    public void Resolve_ZeroDurationUsesAllAvailableTime()
    {
        SchedulePoint fixedPoint = new(
            500,
            0,
            2,
            10,
            "fixed-delay");
        SchedulePoint anchorPoint = new(
            500,
            3,
            2,
            10,
            "anchor");

        Assert.AreEqual(
            (2d, 10d),
            AverageWindowResolver.Resolve(fixedPoint, 0));
        Assert.AreEqual(
            (3d, 10d),
            AverageWindowResolver.Resolve(anchorPoint, 0));
    }

    [TestMethod]
    public void Resolve_PreservesInvalidScheduleErrors()
    {
        SchedulePoint valid = new(
            500,
            0,
            0,
            10,
            "fixed-delay");
        SchedulePoint invalidAnchor = new(
            500,
            10,
            0,
            10,
            "anchor");

        IpceException invalidDuration =
            Assert.ThrowsExactly<IpceException>(
                () => AverageWindowResolver.Resolve(
                    valid,
                    double.NaN));
        IpceException anchor =
            Assert.ThrowsExactly<IpceException>(
                () => AverageWindowResolver.Resolve(
                    invalidAnchor,
                    4));

        Assert.AreEqual("IPCE:InvalidSchedule", invalidDuration.Code);
        Assert.AreEqual("IPCE:InvalidSchedule", anchor.Code);
        StringAssert.Contains(anchor.Message, "确认时间不在驻留窗口内");
    }
}
