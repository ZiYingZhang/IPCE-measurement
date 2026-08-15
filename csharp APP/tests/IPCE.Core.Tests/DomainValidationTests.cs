using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class DomainValidationTests
{
    [TestMethod]
    public void TraceData_MismatchedLengths_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidTrace",
            () => new TraceData([0, 1], [1e-6], TraceMetadata.Unknown));
    }

    [TestMethod]
    public void TraceData_FewerThanTwoPoints_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidTrace",
            () => new TraceData([0], [1e-6], TraceMetadata.Unknown));
    }

    [TestMethod]
    public void TraceData_NonFiniteValue_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidTrace",
            () => new TraceData([0, 1], [1e-6, double.NaN], TraceMetadata.Unknown));
    }

    [TestMethod]
    public void TraceData_TimeMustBeNondecreasingWithPositiveProgress()
    {
        AssertIpceCode(
            "IPCE:InvalidTrace",
            () => new TraceData([0, 2, 1], [1e-6, 2e-6, 3e-6], TraceMetadata.Unknown));
        AssertIpceCode(
            "IPCE:InvalidTrace",
            () => new TraceData([1, 1], [1e-6, 2e-6], TraceMetadata.Unknown));
    }

    [TestMethod]
    public void TraceData_CopiesAcceptedInput()
    {
        var times = new List<double> { 0, 1 };
        var currents = new List<double> { 1e-6, 2e-6 };

        var trace = new TraceData(times, currents, TraceMetadata.Unknown);
        times[0] = 99;
        currents[0] = 99;

        Assert.AreEqual(0, trace.TimeSeconds[0]);
        Assert.AreEqual(1e-6, trace.CurrentAmperes[0]);
    }

    [TestMethod]
    public void CalibrationData_NonPositiveWavelength_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidReference",
            () => new CalibrationData(
            [
                new CalibrationPoint(0, 0.2),
                new CalibrationPoint(500, 0.3),
            ]));
    }

    [TestMethod]
    public void CalibrationData_NonPositiveResponsivity_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidReference",
            () => new CalibrationData(
            [
                new CalibrationPoint(400, 0.2),
                new CalibrationPoint(500, -0.3),
            ]));
    }

    [TestMethod]
    public void CalibrationData_RequiresAtLeastTwoStrictlyIncreasingPoints()
    {
        AssertIpceCode(
            "IPCE:InvalidReference",
            () => new CalibrationData([new CalibrationPoint(400, 0.2)]));
        AssertIpceCode(
            "IPCE:InvalidReference",
            () => new CalibrationData(
            [
                new CalibrationPoint(500, 0.3),
                new CalibrationPoint(400, 0.2),
            ]));
    }

    [TestMethod]
    public void AnchorData_DuplicateWavelength_ThrowsStableCode()
    {
        AssertIpceCode(
            "IPCE:InvalidAnchorFile",
            () => new AnchorData(
            [
                new AnchorPoint(400, 10),
                new AnchorPoint(400, 20),
            ]));
    }

    [TestMethod]
    public void ExternalIpceData_PreservesValuesAboveOneHundredPercent()
    {
        var points = new List<IpceValue>
        {
            new(400, 50),
            new(500, 120),
        };

        var data = new ExternalIpceData(points, "Wavelength/nm", "IPCE/%");
        points[1] = new IpceValue(500, 5);

        Assert.AreEqual(120, data.Points[1].IpcePercent);
    }

    private static void AssertIpceCode(string expectedCode, Action operation)
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(operation);
        Assert.AreEqual(expectedCode, error.Code);
    }
}
