using IPCE.IO.Import;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class ExternalIpceReaderTests
{
    [TestMethod]
    public void SortsAndAveragesDuplicateWavelengthsWithoutClipping()
    {
        using var file = new TemporaryTextFile(
            "Wavelength/nm,IPCE/%\n600,120\n400,50\n500,80\n500,100\n",
            ".csv");

        var result = ExternalIpceReader.Read(file.Path);

        CollectionAssert.AreEqual(
            new[] { 400d, 500d, 600d },
            result.Points.Select(point => point.WavelengthNm).ToArray());
        CollectionAssert.AreEqual(
            new[] { 50d, 90d, 120d },
            result.Points.Select(point => point.IpcePercent).ToArray());
        Assert.AreEqual("Wavelength/nm", result.WavelengthHeader);
        Assert.AreEqual("IPCE/%", result.IpceHeader);
    }

    [TestMethod]
    public void HeaderlessTwoColumnText_IsAccepted()
    {
        using var file = new TemporaryTextFile(
            "400 50\n500 80\n");

        var result = ExternalIpceReader.Read(file.Path);

        Assert.AreEqual(2, result.Points.Count);
        Assert.AreEqual("", result.WavelengthHeader);
        Assert.AreEqual("", result.IpceHeader);
    }

    [TestMethod]
    public void TxtCsvXlsAndXlsx_ProduceIdenticalUnclippedPoints()
    {
        using var files = new ExternalIpceFiles();

        foreach (string path in files.Paths)
        {
            var result = ExternalIpceReader.Read(path);

            CollectionAssert.AreEqual(
                new[] { 400d, 500d, 600d },
                result.Points
                    .Select(point => point.WavelengthNm)
                    .ToArray(),
                Path.GetExtension(path));
            CollectionAssert.AreEqual(
                new[] { 40d, 100d, 120d },
                result.Points
                    .Select(point => point.IpcePercent)
                    .ToArray(),
                Path.GetExtension(path));
        }
    }

    private sealed class ExternalIpceFiles : IDisposable
    {
        private readonly string _directory;

        public ExternalIpceFiles()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                $"ipce-four-format-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            const string text =
                "Wavelength_nm,IPCE_percent\n" +
                "500,120\n400,40\n500,80\n600,120\n";
            string txt = Path.Combine(_directory, "external.txt");
            string csv = Path.Combine(_directory, "external.csv");
            File.WriteAllText(txt, text);
            File.WriteAllText(csv, text);
            string xls = Path.Combine(_directory, "external.xls");
            string xlsx = Path.Combine(_directory, "external.xlsx");
            WriteWorkbook(new HSSFWorkbook(), xls);
            WriteWorkbook(new XSSFWorkbook(), xlsx);
            Paths = [txt, csv, xls, xlsx];
        }

        public IReadOnlyList<string> Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static void WriteWorkbook(
            IWorkbook workbook,
            string path)
        {
            using (workbook)
            {
                ISheet sheet = workbook.CreateSheet("IPCE");
                IRow header = sheet.CreateRow(0);
                header.CreateCell(0).SetCellValue("Wavelength_nm");
                header.CreateCell(1).SetCellValue("IPCE_percent");
                (double Wavelength, double Ipce)[] values =
                [
                    (500, 120),
                    (400, 40),
                    (500, 80),
                    (600, 120),
                ];
                for (int index = 0; index < values.Length; index++)
                {
                    IRow row = sheet.CreateRow(index + 1);
                    row.CreateCell(0)
                        .SetCellValue(values[index].Wavelength);
                    row.CreateCell(1)
                        .SetCellValue(values[index].Ipce);
                }

                using var stream = File.Create(path);
                workbook.Write(stream, leaveOpen: false);
            }
        }
    }
}
