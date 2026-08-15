using IPCE.Core.Errors;
using IPCE.IO.Import;
using NPOI.XSSF.UserModel;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class SpectrumReaderTests
{
    private static string SpectrumPath => Path.Combine(
        TestPaths.DefaultsRoot,
        "标准太阳能光谱数据.xls");

    [TestMethod]
    public void DefaultSpectrum_ExposesSpectraWorksheet()
    {
        IReadOnlyList<string> names =
            SpectrumReader.DiscoverSheets(SpectrumPath);

        CollectionAssert.Contains(names.ToArray(), "Spectra");
    }

    [TestMethod]
    public void DiscoverSheets_PreservesWorkbookOrder()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-sheets-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XSSFWorkbook())
            {
                workbook.CreateSheet("First");
                workbook.CreateSheet("Custom");
                using var stream = File.Create(path);
                workbook.Write(stream, leaveOpen: false);
            }

            IReadOnlyList<string> names =
                SpectrumReader.DiscoverSheets(path);

            CollectionAssert.AreEqual(
                new[] { "First", "Custom" },
                names.ToArray());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public void DefaultSpectrum_DiscoversWavelengthAndGlobalTiltColumns()
    {
        IReadOnlyList<SpectrumColumn> columns =
            SpectrumReader.DiscoverColumns(SpectrumPath, "Spectra");

        SpectrumColumn wavelength = columns.Single(
            column => column.ColumnIndex == 1);
        SpectrumColumn globalTilt = columns.Single(
            column => column.ColumnIndex == 3);
        StringAssert.Contains(
            wavelength.Header.ToLowerInvariant(),
            "wavelength");
        StringAssert.Contains(
            globalTilt.Header.ToLowerInvariant(),
            "global tilt");
        Assert.IsTrue(wavelength.NumericValueCount > 100);
    }

    [TestMethod]
    public void DefaultSpectrum_ImportsSelectedNonNegativeColumns()
    {
        IReadOnlyList<IPCE.Core.Domain.SpectrumPoint> spectrum =
            SpectrumReader.Read(SpectrumPath, "Spectra", 1, 3);

        Assert.IsTrue(spectrum.Count > 100);
        Assert.IsTrue(spectrum.All(point =>
            point.WavelengthNm > 0 &&
            point.IrradianceWattsPerSquareMetrePerNanometre >= 0));
        Assert.AreEqual(280, spectrum[0].WavelengthNm);
        Assert.AreEqual(4000, spectrum[^1].WavelengthNm);
    }

    [TestMethod]
    public void MissingWorksheet_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            SpectrumReader.DiscoverColumns(SpectrumPath, "missing"));

        Assert.AreEqual("IPCE:SpectrumSheetNotFound", error.Code);
    }
}
