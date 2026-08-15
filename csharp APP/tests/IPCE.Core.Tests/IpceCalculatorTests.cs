using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class IpceCalculatorTests
{
    private const double HcOverQElectronVoltNanometres =
        1239.8419843320026;

    [TestMethod]
    public void SyntheticCalculation_ReturnsTwentyFiftyAndEightyPercent()
    {
        double[] wavelengths = [400, 500, 600];
        double[] responsivities = [0.2, 0.3, 0.4];
        double[] powerDensities = [10e-6, 15e-6, 12e-6];
        double[] expectedFractions = [0.2, 0.5, 0.8];
        const double siliconArea = 0.36;
        const double sampleArea = 0.75;

        var calibration = new CalibrationData(
            wavelengths.Zip(responsivities, (wavelength, responsivity) =>
                new CalibrationPoint(wavelength, responsivity)).ToArray());
        ExtractedPoint[] silicon = wavelengths
            .Select((wavelength, index) =>
            {
                double absoluteCurrent =
                    powerDensities[index] * siliconArea *
                    responsivities[index];
                return new ExtractedPoint(
                    wavelength,
                    -absoluteCurrent,
                    -absoluteCurrent,
                    absoluteCurrent,
                    0,
                    40);
            })
            .ToArray();

        IReadOnlyList<PowerDensityPoint> power =
            IpceCalculator.CalculatePowerDensity(
                calibration, silicon, siliconArea);
        ExtractedPoint[] sample = wavelengths
            .Select((wavelength, index) =>
            {
                double currentDensity =
                    powerDensities[index] * expectedFractions[index] *
                    wavelength / HcOverQElectronVoltNanometres;
                double current = currentDensity * sampleArea;
                return new ExtractedPoint(
                    wavelength, current, current, current, 0, 40);
            })
            .ToArray();

        IReadOnlyList<IpcePoint> result =
            IpceCalculator.CalculateIpce(power, sample, sampleArea);

        AssertClose(10e-6, power[0].IncidentPowerDensityWattsPerSquareCentimetre);
        AssertClose(-silicon[0].AbsolutePhotoCurrentAmperes,
            power[0].SiliconPhotoCurrentSignedAmperes);
        AssertClose(20, result[0].IpcePercent);
        AssertClose(50, result[1].IpcePercent);
        AssertClose(80, result[2].IpcePercent);
        Assert.AreEqual(sampleArea,
            result[0].SampleIlluminatedAreaSquareCentimetres);
        AssertClose(sample[0].PhotoCurrentSignedAmperes,
            result[0].SamplePhotoCurrentSignedAmperes);
    }

    [TestMethod]
    public void DifferentSampleGrid_UsesPchipPowerInterpolation()
    {
        IReadOnlyList<PowerDensityPoint> power =
        [
            PowerPoint(400, 10e-6),
            PowerPoint(500, 15e-6),
            PowerPoint(600, 12e-6),
        ];
        double[] wavelengths = [450, 550];
        double[] interpolatedPower = [13.625e-6, 14.375e-6];
        double[] expectedFraction = [0.3, 0.7];
        ExtractedPoint[] sample = wavelengths
            .Select((wavelength, index) =>
            {
                double current = interpolatedPower[index] *
                    expectedFraction[index] * wavelength /
                    HcOverQElectronVoltNanometres;
                return new ExtractedPoint(
                    wavelength, current, current, current, 0, 20);
            })
            .ToArray();

        IReadOnlyList<IpcePoint> result =
            IpceCalculator.CalculateIpce(power, sample, 1);

        AssertClose(13.625e-6,
            result[0].IncidentPowerDensityWattsPerSquareCentimetre);
        AssertClose(14.375e-6,
            result[1].IncidentPowerDensityWattsPerSquareCentimetre);
        AssertClose(30, result[0].IpcePercent);
        AssertClose(70, result[1].IpcePercent);
        Assert.IsTrue(result.All(point => point.PowerDensityInterpolated));
    }

    [TestMethod]
    public void CalibrationCoverage_IsNotExtrapolated()
    {
        var calibration = new CalibrationData(
        [
            new CalibrationPoint(400, 0.2),
            new CalibrationPoint(500, 0.3),
        ]);

        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceCalculator.CalculatePowerDensity(
                calibration,
                [new ExtractedPoint(600, 1, 1, 1, 0, 1)],
                1));

        Assert.AreEqual("IPCE:CalibrationRange", error.Code);
    }

    [TestMethod]
    public void SamplePowerCoverage_IsNotExtrapolated()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceCalculator.CalculateIpce(
                [PowerPoint(400, 1e-5), PowerPoint(500, 1e-5)],
                [new ExtractedPoint(600, 1e-6, 1e-6, 1e-6, 0, 1)],
                1));

        Assert.AreEqual("IPCE:PowerInterpolationRange", error.Code);
    }

    [TestMethod]
    public void NonPositivePowerDensity_ThrowsStableCode()
    {
        var calibration = new CalibrationData(
        [
            new CalibrationPoint(400, 0.2),
            new CalibrationPoint(500, 0.3),
        ]);

        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceCalculator.CalculatePowerDensity(
                calibration,
                [
                    new ExtractedPoint(400, 0, 0, 0, 0, 1),
                    new ExtractedPoint(500, 1, 1, 1, 0, 1),
                ],
                1));

        Assert.AreEqual("IPCE:InvalidPowerDensity", error.Code);
    }

    [TestMethod]
    public void PowerDensity_MatchesEveryMatlabGoldenColumn()
    {
        var extractedRows = GoldenCsv.ReadNumeric(
            "default_silicon_extracted.csv");
        var expectedRows = GoldenCsv.ReadNumeric(
            "default_power_density.csv");
        var calibration = new CalibrationData(expectedRows
            .Select(row => new CalibrationPoint(
                row["Wavelength_nm"],
                row["SiResponsivity_A_per_W"]))
            .ToArray());
        ExtractedPoint[] extracted = extractedRows
            .Select(row => new ExtractedPoint(
                row["Wavelength_nm"],
                row["MeanCurrent_A"],
                row["PhotoCurrent_A"],
                row["AbsPhotoCurrent_A"],
                row["PhotoCurrentSE_A"],
                (int)row["SampleCount"]))
            .ToArray();

        IReadOnlyList<PowerDensityPoint> actual =
            IpceCalculator.CalculatePowerDensity(calibration, extracted, 0.36);

        Assert.AreEqual(expectedRows.Count, actual.Count);
        for (int index = 0; index < actual.Count; index++)
        {
            IReadOnlyDictionary<string, double> expected = expectedRows[index];
            PowerDensityPoint point = actual[index];
            AssertClose(expected["Wavelength_nm"], point.WavelengthNm);
            AssertClose(expected["SiResponsivity_A_per_W"],
                point.SiliconResponsivityAmperesPerWatt);
            AssertClose(expected["SiMeanCurrent_A"],
                point.SiliconMeanCurrentAmperes);
            AssertClose(expected["SiPhotoCurrentSigned_A"],
                point.SiliconPhotoCurrentSignedAmperes);
            AssertClose(expected["SiPhotocurrent_A"],
                point.SiliconPhotocurrentAmperes);
            AssertClose(expected["SiPhotoCurrentSE_A"],
                point.SiliconPhotoCurrentStandardErrorAmperes);
            AssertClose(expected["SiliconIlluminatedArea_cm2"],
                point.SiliconIlluminatedAreaSquareCentimetres);
            AssertClose(expected["IncidentPowerDensity_W_cm2"],
                point.IncidentPowerDensityWattsPerSquareCentimetre);
            AssertClose(expected["IncidentPowerDensitySE_W_cm2"],
                point.IncidentPowerDensityStandardError);
            Assert.AreEqual((int)expected["SiSampleCount"], point.SampleCount);
        }
    }

    [TestMethod]
    public void Ipce_MatchesEveryMatlabGoldenColumn()
    {
        var expectedRows = GoldenCsv.ReadNumeric("synthetic_sample_ipce.csv");
        PowerDensityPoint[] power = expectedRows
            .Select(row => new PowerDensityPoint(
                row["Wavelength_nm"],
                1,
                0,
                0,
                1,
                0,
                1,
                row["IncidentPowerDensity_W_cm2"],
                row["IncidentPowerDensitySE_W_cm2"],
                1))
            .ToArray();
        ExtractedPoint[] sample = expectedRows
            .Select(row => new ExtractedPoint(
                row["Wavelength_nm"],
                row["SampleMeanCurrent_A"],
                row["SamplePhotoCurrentSigned_A"],
                row["SamplePhotocurrent_A"],
                row["SamplePhotoCurrentSE_A"],
                (int)row["SampleSampleCount"]))
            .ToArray();

        IReadOnlyList<IpcePoint> actual =
            IpceCalculator.CalculateIpce(power, sample, 0.75);

        for (int index = 0; index < actual.Count; index++)
        {
            IpcePoint point = actual[index];
            IReadOnlyDictionary<string, double> expected = expectedRows[index];
            AssertClose(expected["Wavelength_nm"], point.WavelengthNm);
            AssertClose(expected["IncidentPowerDensity_W_cm2"],
                point.IncidentPowerDensityWattsPerSquareCentimetre);
            AssertClose(expected["IncidentPowerDensitySE_W_cm2"],
                point.IncidentPowerDensityStandardError);
            Assert.AreEqual(
                expected["PowerDensityInterpolated"] != 0,
                point.PowerDensityInterpolated);
            AssertClose(expected["SampleMeanCurrent_A"],
                point.SampleMeanCurrentAmperes);
            AssertClose(expected["SamplePhotoCurrentSigned_A"],
                point.SamplePhotoCurrentSignedAmperes);
            AssertClose(expected["SamplePhotocurrent_A"],
                point.SamplePhotocurrentAmperes);
            AssertClose(expected["SamplePhotoCurrentSE_A"],
                point.SamplePhotoCurrentStandardErrorAmperes);
            AssertClose(expected["SampleIlluminatedArea_cm2"],
                point.SampleIlluminatedAreaSquareCentimetres);
            AssertClose(expected["SamplePhotocurrentDensity_A_cm2"],
                point.SamplePhotocurrentDensityAmperesPerSquareCentimetre);
            AssertClose(expected["SamplePhotoCurrentDensitySE_A_cm2"],
                point.SamplePhotoCurrentDensityStandardError);
            Assert.AreEqual(
                (int)expected["SampleSampleCount"],
                point.SampleCount);
            AssertClose(expected["IPCE_percent"], point.IpcePercent);
            AssertClose(expected["IPCE_EstimatedSE_percent"],
                point.IpceEstimatedStandardErrorPercent);
        }
    }

    private static PowerDensityPoint PowerPoint(
        double wavelength,
        double powerDensity)
    {
        return new PowerDensityPoint(
            wavelength, 1, 1, 1, 1, 0, 1, powerDensity, 0, 1);
    }

    private static void AssertClose(double expected, double actual)
    {
        double tolerance = Math.Max(1e-12, 1e-9 * Math.Abs(expected));
        Assert.IsTrue(
            Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
    }
}
