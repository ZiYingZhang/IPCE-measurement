using System.Text;
using IPCE.Core.Errors;
using IPCE.IO.Export;
using IPCE.IO.Tables;
using MatFileHandler;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class ExportServiceTests
{
    [TestMethod]
    public void NoSelection_IsRejected()
    {
        using var directory = new TemporaryDirectory();

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => ExportService.Write(
                [],
                Path.Combine(directory.Path, "empty.xlsx"),
                ExportFormat.Xlsx));

        Assert.AreEqual("IPCE:NoExportSelection", exception.Code);
    }

    [TestMethod]
    public void DuplicateTableNames_AreRejectedCaseInsensitively()
    {
        using var directory = new TemporaryDirectory();
        ExportTable first = CreateNumericTable("Results");
        ExportTable duplicate = CreateNumericTable("results");

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => ExportService.Write(
                [first, duplicate],
                Path.Combine(directory.Path, "duplicate.xlsx"),
                ExportFormat.Xlsx));

        Assert.AreEqual("IPCE:DuplicateExportNames", exception.Code);
    }

    [TestMethod]
    public void Xlsx_WritesMultipleNamedSheetsAndTypedCells()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "results.xlsx");
        ExportTable sample = CreateMixedTable("SampleIPCE");
        ExportTable summary = CreateNumericTable("SpectrumSummary");

        IReadOnlyList<string> written =
            ExportService.Write([sample, summary], path, ExportFormat.Xlsx);

        CollectionAssert.AreEqual(new[] { path }, written.ToArray());
        CollectionAssert.AreEqual(
            new[] { "SampleIPCE", "SpectrumSummary" },
            NpoiWorkbookReader.GetSheetNames(path).ToArray());
        WorkbookSheetData sheet =
            NpoiWorkbookReader.ReadSheet(path, "SampleIPCE");
        Assert.AreEqual("Wavelength_nm", sheet.Rows[0][0].Text);
        Assert.AreEqual(400d, sheet.Rows[1][0].NumericValue);
        Assert.AreEqual("note, one", sheet.Rows[1][2].Text);
        Assert.AreEqual("TRUE", sheet.Rows[1][3].Text);
    }

    [TestMethod]
    public void Csv_WritesUtf8BomAndQuotesSpecialCharacters()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "results.csv");

        IReadOnlyList<string> written = ExportService.Write(
            [CreateMixedTable("SampleIPCE")],
            path,
            ExportFormat.Csv);

        CollectionAssert.AreEqual(new[] { path }, written.ToArray());
        byte[] bytes = File.ReadAllBytes(path);
        CollectionAssert.AreEqual(
            new byte[] { 0xEF, 0xBB, 0xBF },
            bytes.Take(3).ToArray());
        string contents = Encoding.UTF8.GetString(bytes[3..]);
        StringAssert.StartsWith(
            contents,
            "Wavelength_nm,IPCE_percent,Note,Included\r\n");
        StringAssert.Contains(contents, "\"note, one\"");
        StringAssert.Contains(contents, "\"quoted \"\"value\"\"\"");
    }

    [TestMethod]
    public void Csv_MultipleTablesUseSuffixedFileNames()
    {
        using var directory = new TemporaryDirectory();
        string requestedPath = Path.Combine(directory.Path, "results.csv");
        string firstPath =
            Path.Combine(directory.Path, "results_SampleIPCE.csv");
        string secondPath =
            Path.Combine(directory.Path, "results_SpectrumSummary.csv");

        IReadOnlyList<string> written = ExportService.Write(
            [
                CreateNumericTable("SampleIPCE"),
                CreateNumericTable("SpectrumSummary"),
            ],
            requestedPath,
            ExportFormat.Csv);

        CollectionAssert.AreEqual(
            new[] { firstPath, secondPath },
            written.ToArray());
        Assert.IsTrue(File.Exists(firstPath));
        Assert.IsTrue(File.Exists(secondPath));
        Assert.IsFalse(File.Exists(requestedPath));
    }

    [TestMethod]
    public void Mat_WritesTopLevelExportDataStructure()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "results.mat");

        IReadOnlyList<string> written = ExportService.Write(
            [
                CreateMixedTable("SampleIPCE"),
                CreateNumericTable("SpectrumSummary"),
            ],
            path,
            ExportFormat.Mat);

        CollectionAssert.AreEqual(new[] { path }, written.ToArray());
        using var stream = File.OpenRead(path);
        IMatFile file = new MatFileReader(stream).Read();
        IVariable exportData = file["exportData"];
        Assert.IsNotNull(exportData);
        var root = (IStructureArray)exportData.Value;
        CollectionAssert.AreEquivalent(
            new[] { "SampleIPCE", "SpectrumSummary" },
            root.FieldNames.ToArray());
        var sample = (IStructureArray)root["SampleIPCE", 0, 0];
        CollectionAssert.Contains(sample.FieldNames.ToArray(), "VariableNames");
        CollectionAssert.Contains(sample.FieldNames.ToArray(), "RowCount");
        CollectionAssert.Contains(sample.FieldNames.ToArray(), "IPCE_percent");
        var rowCount = (IArrayOf<double>)sample["RowCount", 0, 0];
        Assert.AreEqual(2d, rowCount[0, 0]);
    }

    [TestMethod]
    public void Mat_WritesStableFixtureForMatlabVerification()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "ipce-csharp-mat-verification.mat");

        IReadOnlyList<string> written = ExportService.Write(
            [CreateMixedTable("SampleIPCE")],
            path,
            ExportFormat.Mat);

        CollectionAssert.AreEqual(new[] { path }, written.ToArray());
        Assert.IsTrue(new FileInfo(path).Length > 0);
    }

    [TestMethod]
    public void LockedDestination_PreservesExistingFileAndReturnsStableError()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "locked.csv");
        byte[] original = Encoding.UTF8.GetBytes("original");
        File.WriteAllBytes(path, original);
        using var lockStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => ExportService.Write(
                [CreateNumericTable("Results")],
                path,
                ExportFormat.Csv));

        Assert.AreEqual("IPCE:ExportFailed", exception.Code);
        lockStream.Dispose();
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        Assert.AreEqual(
            1,
            Directory.GetFiles(directory.Path).Length,
            "Temporary export files must be removed after failure.");
    }

    [TestMethod]
    public void ExportTable_MismatchedColumnLengthsAreRejected()
    {
        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => new ExportTable(
                "Bad",
                [
                    new ExportColumn("A", typeof(double), [1d, 2d]),
                    new ExportColumn("B", typeof(double), [3d]),
                ]));

        Assert.AreEqual("IPCE:InvalidExportTable", exception.Code);
    }

    private static ExportTable CreateNumericTable(string name) =>
        new(
            name,
            [
                new ExportColumn(
                    "Wavelength_nm",
                    typeof(double),
                    [400d, 500d]),
                new ExportColumn(
                    "IPCE_percent",
                    typeof(double),
                    [20d, 50d]),
            ]);

    private static ExportTable CreateMixedTable(string name) =>
        new(
            name,
            [
                new ExportColumn(
                    "Wavelength_nm",
                    typeof(double),
                    [400d, 500d]),
                new ExportColumn(
                    "IPCE_percent",
                    typeof(double),
                    [20d, 50d]),
                new ExportColumn(
                    "Note",
                    typeof(string),
                    ["note, one", "quoted \"value\""]),
                new ExportColumn(
                    "Included",
                    typeof(bool),
                    [true, false]),
            ]);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-export-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
