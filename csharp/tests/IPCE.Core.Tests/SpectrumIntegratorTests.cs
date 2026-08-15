using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class SpectrumIntegratorTests
{
    [TestMethod]
    public void ConstantOneHundredPercent_MatchesAnalyticMatlabResult()
    {
        IntegrationResult result = SpectrumIntegrator.Integrate(
            [
                new IpceValue(400, 100),
                new IpceValue(500, 100),
                new IpceValue(600, 100),
            ],
            Enumerable.Range(0, 9)
                .Select(index => new SpectrumPoint(400 + 25 * index, 1))
                .ToArray(),
            400,
            600);

        AssertClose(
            8.0655439373492115,
            result.Summary
                .IntegratedCurrentDensityMilliamperePerSquareCentimetre);
        AssertClose(200, result.Summary.IntegratedPowerWattsPerSquareMetre);
        Assert.AreEqual(9, result.Summary.IntegrationGridPoints);
        AssertClose(
            result.Summary
                .IntegratedCurrentDensityMilliamperePerSquareCentimetre,
            result.Curve[^1]
                .CumulativeCurrentDensityMilliamperePerSquareCentimetre);
        Assert.IsTrue(result.Curve
            .Zip(result.Curve.Skip(1), (left, right) =>
                right.CumulativeCurrentDensityMilliamperePerSquareCentimetre >=
                left.CumulativeCurrentDensityMilliamperePerSquareCentimetre)
            .All(isMonotone => isMonotone));
    }

    [TestMethod]
    public void Integration_InsertsBoundsAndUsesBothInterpolationMethods()
    {
        IntegrationResult result = SpectrumIntegrator.Integrate(
            [
                new IpceValue(400, 100),
                new IpceValue(500, 50),
                new IpceValue(600, 100),
            ],
            [
                new SpectrumPoint(350, 1),
                new SpectrumPoint(450, 2),
                new SpectrumPoint(550, 4),
                new SpectrumPoint(650, 8),
            ],
            400,
            600);

        CollectionAssert.AreEqual(
            new[] { 400d, 450d, 550d, 600d },
            result.Curve.Select(point => point.WavelengthNm).ToArray());
        AssertClose(1.5,
            result.Curve[0].IrradianceWattsPerSquareMetrePerNanometre);
        AssertClose(6,
            result.Curve[^1].IrradianceWattsPerSquareMetrePerNanometre);
        AssertClose(62.5, result.Curve[1].IpcePercent);
        Assert.AreEqual(
            "pchip(IPCE) + linear(spectrum)",
            result.Summary.Interpolation);
    }

    [TestMethod]
    public void Integration_DoesNotClipOneHundredTwentyPercent()
    {
        IntegrationResult result = SpectrumIntegrator.Integrate(
            [new IpceValue(400, 120), new IpceValue(500, 120)],
            [new SpectrumPoint(400, 1), new SpectrumPoint(500, 1)],
            400,
            500);

        Assert.IsTrue(result.Curve.All(point => point.IpcePercent == 120));
    }

    [TestMethod]
    public void Integration_RejectsRangeOutsideCommonCoverage()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            SpectrumIntegrator.Integrate(
                [new IpceValue(400, 100), new IpceValue(600, 100)],
                [new SpectrumPoint(450, 1), new SpectrumPoint(650, 1)],
                400,
                600));

        Assert.AreEqual("IPCE:IntegrationCoverage", error.Code);
    }

    [TestMethod]
    public void Integration_MatchesMatlabGoldenCurveAndSummary()
    {
        IntegrationResult result = SpectrumIntegrator.Integrate(
            [
                new IpceValue(400, 100),
                new IpceValue(500, 100),
                new IpceValue(600, 100),
            ],
            Enumerable.Range(0, 9)
                .Select(index => new SpectrumPoint(400 + 25 * index, 1))
                .ToArray(),
            400,
            600);
        var expectedRows = GoldenCsv.ReadNumeric("integration_curve.csv");

        Assert.AreEqual(expectedRows.Count, result.Curve.Count);
        for (int index = 0; index < result.Curve.Count; index++)
        {
            IntegrationCurvePoint point = result.Curve[index];
            IReadOnlyDictionary<string, double> expected = expectedRows[index];
            AssertClose(expected["Wavelength_nm"], point.WavelengthNm);
            AssertClose(expected["Irradiance_W_m2_nm"],
                point.IrradianceWattsPerSquareMetrePerNanometre);
            AssertClose(expected["IPCE_percent"], point.IpcePercent);
            AssertClose(expected["EQE_fraction"], point.EqeFraction);
            AssertClose(expected["PhotonFlux_m2_s_nm"],
                point.PhotonFluxPerSquareMetreSecondNanometre);
            AssertClose(expected["SpectralCurrent_mA_cm2_nm"],
                point.SpectralCurrentMilliamperePerSquareCentimetreNanometre);
            AssertClose(expected["CumulativeCurrentDensity_mA_cm2"],
                point.CumulativeCurrentDensityMilliamperePerSquareCentimetre);
        }
    }

    private static void AssertClose(double expected, double actual)
    {
        double tolerance = Math.Max(1e-12, 1e-9 * Math.Abs(expected));
        Assert.IsTrue(
            Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
    }
}
