using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Extraction;
using IPCE.Core.Scheduling;
using IPCE.IO.Import;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class RealFileRegressionTests
{
    [TestMethod]
    public void SuppliedMeasurementFiles_RunBothCompleteMeasurementStages()
    {
        CalibrationData calibration = CalibrationReader.Read(Path.Combine(
            TestPaths.DefaultsRoot,
            "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx"));
        TraceData siliconTrace = ItTraceReader.Read(Path.Combine(
            TestPaths.DefaultsRoot,
            "Si-i t [300 1100] nm-grating 2-filter.txt"));
        IReadOnlyList<AnchorPoint> siliconAnchors = AnchorReader.Read(
            Path.Combine(
                TestPaths.DefaultsRoot,
                "Si-i t [300 1100] nm-grating 2-filter-time match.txt"));
        double[] siliconWavelengths = Enumerable.Range(0, 161)
            .Select(index => 300d + 5 * index)
            .ToArray();
        IReadOnlyList<SchedulePoint> siliconSchedule =
            ScheduleBuilder.Build(
                siliconWavelengths,
                AlignmentMode.Anchors,
                siliconAnchors,
                50,
                8);
        IReadOnlyList<ExtractedPoint> siliconExtracted =
            TraceExtractor.Extract(
                siliconTrace,
                siliconSchedule,
                4,
                new DarkCorrection(true, 0.1, 10));
        IReadOnlyList<PowerDensityPoint> power =
            IpceCalculator.CalculatePowerDensity(
                calibration,
                siliconExtracted,
                0.36);

        TraceData sampleTrace = ItTraceReader.Read(Path.Combine(
            TestPaths.ExamplesRoot,
            "MBVO-IT-300-600 nm.txt"));
        IReadOnlyList<AnchorPoint> sampleAnchors = AnchorReader.Read(
            Path.Combine(
                TestPaths.ExamplesRoot,
                "MBVO-300-600-match time.txt"));
        double[] sampleWavelengths = Enumerable.Range(0, 61)
            .Select(index => 300d + 5 * index)
            .ToArray();
        IReadOnlyList<SchedulePoint> sampleSchedule =
            ScheduleBuilder.Build(
                sampleWavelengths,
                AlignmentMode.Anchors,
                sampleAnchors,
                50,
                8);
        IReadOnlyList<ExtractedPoint> sampleExtracted =
            TraceExtractor.Extract(
                sampleTrace,
                sampleSchedule,
                4,
                new DarkCorrection(true, 50, 60));
        IReadOnlyList<IpcePoint> ipce = IpceCalculator.CalculateIpce(
            power,
            sampleExtracted,
            1);

        Assert.AreEqual(161, power.Count);
        Assert.AreEqual(61, ipce.Count);
        Assert.IsTrue(power.All(point =>
            point.IncidentPowerDensityWattsPerSquareCentimetre > 0));
        Assert.IsTrue(ipce.All(point =>
            double.IsFinite(point.IpcePercent)));
    }

    [TestMethod]
    public void SuppliedSpectrumFile_ImportsDocumentedGlobalTiltColumn()
    {
        IReadOnlyList<SpectrumPoint> spectrum = SpectrumReader.Read(
            Path.Combine(
                TestPaths.DefaultsRoot,
                "标准太阳能光谱数据.xls"),
            "Spectra",
            1,
            3);

        Assert.AreEqual(2002, spectrum.Count);
        Assert.AreEqual(280, spectrum[0].WavelengthNm);
        Assert.AreEqual(4000, spectrum[^1].WavelengthNm);
        Assert.IsTrue(spectrum.All(point =>
            point.IrradianceWattsPerSquareMetrePerNanometre >= 0));
    }
}
