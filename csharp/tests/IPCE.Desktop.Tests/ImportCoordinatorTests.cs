using System.IO;
using System.Text;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Import;
using IPCE.Desktop.Services;
using IPCE.IO.Import;
using NPOI.XSSF.UserModel;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class ImportCoordinatorTests
{
    [TestMethod]
    public async Task RecognizedTraceUnits_DoNotOpenSelector()
    {
        using var file = new TemporaryTextFile(
            "Time/sec,Current/uA\n0,1\n1,2\n");
        var selections = new RecordingSelections(null);
        var coordinator = new TraceImportCoordinator(selections);

        TraceData? trace = await coordinator.ReadAsync(file.Path);

        Assert.IsNotNull(trace);
        Assert.AreEqual(0, selections.TraceUnitSelectionCount);
        Assert.AreEqual(1e-6, trace.CurrentAmperes[0]);
    }

    [TestMethod]
    public async Task MissingTraceUnits_UsesHeadersAndExplicitSelection()
    {
        using var file = new TemporaryTextFile(
            "elapsed,signal\n0,1\n1,2\n");
        var selections = new RecordingSelections(
            new UnitOverrides("min", "uA"));
        var coordinator = new TraceImportCoordinator(selections);

        TraceData? trace = await coordinator.ReadAsync(file.Path);

        Assert.IsNotNull(trace);
        Assert.AreEqual(1, selections.TraceUnitSelectionCount);
        Assert.AreEqual(
            "elapsed",
            selections.LastInspection!.TimeHeader);
        Assert.AreEqual(
            "signal",
            selections.LastInspection.CurrentHeader);
        CollectionAssert.AreEqual(
            new[] { 0d, 60d },
            trace.TimeSeconds.ToArray());
        CollectionAssert.AreEqual(
            new[] { 1e-6, 2e-6 },
            trace.CurrentAmperes.ToArray());
    }

    [TestMethod]
    public async Task CancelledTraceUnitSelection_ReturnsNull()
    {
        using var file = new TemporaryTextFile(
            "elapsed,signal\n0,1\n1,2\n");
        var coordinator = new TraceImportCoordinator(
            new RecordingSelections(null));

        TraceData? trace = await coordinator.ReadAsync(file.Path);

        Assert.IsNull(trace);
    }

    [TestMethod]
    public async Task InvalidTraceOverride_ThrowsWithoutReturningReplacement()
    {
        using var file = new TemporaryTextFile(
            "elapsed,signal\n0,1\n1,2\n");
        var coordinator = new TraceImportCoordinator(
            new RecordingSelections(
                new UnitOverrides("fortnight", "uA")));

        IpceException exception =
            await Assert.ThrowsExactlyAsync<IpceException>(
                () => coordinator.ReadAsync(file.Path));

        Assert.AreEqual("IPCE:UnsupportedTimeUnit", exception.Code);
    }

    [TestMethod]
    public async Task SpectrumSelection_ReadsChosenSheetAndColumns()
    {
        using var workbook = new TemporarySpectrumWorkbook();
        var selections = new SpectrumSelections(
            new SpectrumImportSelection("Custom", 2, 4));
        var coordinator = new SpectrumImportCoordinator(selections);

        SpectrumImportResult? result =
            await coordinator.ReadAsync(workbook.Path);

        Assert.IsNotNull(result);
        Assert.AreEqual("Custom", result.Selection.SheetName);
        Assert.AreEqual(2, result.Selection.WavelengthColumn);
        Assert.AreEqual(4, result.Selection.IrradianceColumn);
        Assert.AreEqual("Wavelength Custom", result.WavelengthHeader);
        Assert.AreEqual("Irradiance Custom", result.IrradianceHeader);
        CollectionAssert.AreEqual(
            new[] { 400d, 500d },
            result.Points.Select(point => point.WavelengthNm).ToArray());
        CollectionAssert.Contains(
            selections.LastSheets!.ToArray(),
            "Custom");
    }

    [TestMethod]
    public async Task CancelledSpectrumSelection_ReturnsNull()
    {
        using var workbook = new TemporarySpectrumWorkbook();
        var coordinator = new SpectrumImportCoordinator(
            new SpectrumSelections(null));

        SpectrumImportResult? result =
            await coordinator.ReadAsync(workbook.Path);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task IdenticalSpectrumColumns_AreRejected()
    {
        using var workbook = new TemporarySpectrumWorkbook();
        var coordinator = new SpectrumImportCoordinator(
            new SpectrumSelections(
                new SpectrumImportSelection("Custom", 2, 2)));

        IpceException exception =
            await Assert.ThrowsExactlyAsync<IpceException>(
                () => coordinator.ReadAsync(workbook.Path));

        Assert.AreEqual(
            "IPCE:InvalidSpectrumSelection",
            exception.Code);
    }

    [TestMethod]
    public async Task SpectrumViewModel_CancelPreservesDataAndSelectionSummary()
    {
        using var workbook = new TemporarySpectrumWorkbook();
        var selections = new SpectrumSelections(
            new SpectrumImportSelection("Custom", 2, 4));
        var session = new IPCE.Desktop.State.SessionState();
        var viewModel = new IPCE.Desktop.ViewModels
            .SpectrumWorkflowViewModel(
                session,
                spectrumImports:
                    new SpectrumImportCoordinator(selections),
                localization: TestLocalization.Chinese());

        bool imported =
            await viewModel.ImportSpectrumAsync(workbook.Path);
        IReadOnlyList<SpectrumPoint> prior = session.Spectrum!;
        string priorSummary = viewModel.SpectrumImportSummary;
        selections.Selection = null;

        bool cancelled =
            await viewModel.ImportSpectrumAsync(workbook.Path);

        Assert.IsTrue(imported);
        Assert.IsFalse(cancelled);
        Assert.AreSame(prior, session.Spectrum);
        Assert.AreEqual(priorSummary, viewModel.SpectrumImportSummary);
        StringAssert.Contains(priorSummary, "Custom");
        StringAssert.Contains(priorSummary, "Wavelength Custom");
        StringAssert.Contains(priorSummary, "Irradiance Custom");
        StringAssert.Contains(priorSummary, "2 点");
        StringAssert.Contains(priorSummary, "400–500 nm");
    }

    private sealed class RecordingSelections(UnitOverrides? selection)
        : IImportSelectionService
    {
        public int TraceUnitSelectionCount { get; private set; }

        public TraceImportInspection? LastInspection { get; private set; }

        public UnitOverrides? SelectTraceUnits(
            TraceImportInspection inspection)
        {
            TraceUnitSelectionCount++;
            LastInspection = inspection;
            return selection;
        }
    }

    private sealed class SpectrumSelections(
        SpectrumImportSelection? selection)
        : IImportSelectionService
    {
        public SpectrumImportSelection? Selection { get; set; } =
            selection;

        public IReadOnlyList<string>? LastSheets { get; private set; }

        public UnitOverrides? SelectTraceUnits(
            TraceImportInspection inspection) => null;

        public SpectrumImportSelection? SelectSpectrum(
            IReadOnlyList<string> sheets,
            Func<string, IReadOnlyList<SpectrumColumn>>
                discoverColumns,
            SpectrumImportSelection? suggested)
        {
            LastSheets = sheets;
            if (Selection is not null)
            {
                _ = discoverColumns(Selection.SheetName);
            }

            return Selection;
        }
    }

    private sealed class TemporaryTextFile : IDisposable
    {
        public TemporaryTextFile(string contents)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-import-{Guid.NewGuid():N}.txt");
            File.WriteAllText(
                Path,
                contents,
                new UTF8Encoding(false));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private sealed class TemporarySpectrumWorkbook : IDisposable
    {
        public TemporarySpectrumWorkbook()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-spectrum-{Guid.NewGuid():N}.xlsx");
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Custom");
            string[] headers =
                ["Ignore", "Wavelength Custom", "Unused", "Irradiance Custom"];
            var header = sheet.CreateRow(0);
            for (int column = 0; column < headers.Length; column++)
            {
                header.CreateCell(column).SetCellValue(headers[column]);
            }

            for (int rowIndex = 1; rowIndex <= 2; rowIndex++)
            {
                var row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(rowIndex);
                row.CreateCell(1).SetCellValue(300 + rowIndex * 100);
                row.CreateCell(2).SetCellValue(rowIndex * 10);
                row.CreateCell(3).SetCellValue(rowIndex * 0.5);
            }

            workbook.CreateSheet("Other");
            using var stream = File.Create(Path);
            workbook.Write(stream, leaveOpen: false);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
