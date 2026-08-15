using IPCE.IO.Import;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class CalibrationReaderTests
{
    [TestMethod]
    public void DefaultCalibration_ImportsPositiveResponsivityData()
    {
        string path = Path.Combine(
            TestPaths.DefaultsRoot,
            "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx");

        var calibration = CalibrationReader.Read(path);

        Assert.IsTrue(calibration.Points.Count >= 2);
        Assert.IsTrue(calibration.Points.All(point =>
            point.WavelengthNm > 0 &&
            point.ResponsivityAmperesPerWatt > 0));
        Assert.AreEqual(300, calibration.Points[0].WavelengthNm);
        Assert.AreEqual(
            0.1291,
            calibration.Points[0].ResponsivityAmperesPerWatt,
            1e-12);
        Assert.AreEqual(1100, calibration.Points[^1].WavelengthNm);
    }
}
