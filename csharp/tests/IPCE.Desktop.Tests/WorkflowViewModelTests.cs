using System.ComponentModel;
using System.IO;
using System.Text;
using IPCE.Core.Domain;
using IPCE.Desktop.Import;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.IO.Import;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class WorkflowViewModelTests
{
    [TestMethod]
    public void MainViewModel_ChildWorkflowsShareOneSession()
    {
        var session = new SessionState();

        var viewModel = new MainViewModel(session);

        Assert.AreSame(session, viewModel.Session);
        Assert.AreSame(session, viewModel.Silicon.Session);
        Assert.AreSame(session, viewModel.Sample.Session);
        Assert.AreSame(session, viewModel.Spectrum.Session);
    }

    [TestMethod]
    public async Task SiliconImportCommand_UsesPathAndRaisesTraceNotification()
    {
        using var files = new TemporaryFiles();
        string path = files.Write(
            "silicon.txt",
            "Time (s)\tCurrent (A)\n0\t1e-6\n1\t2e-6\n");
        var context = new RecordingSynchronizationContext();
        var viewModel = new SiliconWorkflowViewModel(
            new SessionState(),
            context);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);

        await viewModel.ImportTraceCommand.ExecuteAsync(path);

        Assert.IsNotNull(viewModel.Trace);
        StringAssert.Contains(viewModel.TraceImportSummary, "2 点");
        StringAssert.Contains(viewModel.TraceImportSummary, "s/A");
        CollectionAssert.Contains(changedProperties, nameof(viewModel.Trace));
        Assert.IsTrue(
            context.PostCount > 0,
            "The final state assignment must be marshalled to the UI context.");
    }

    [TestMethod]
    public async Task CancelledUnitSelection_PreservesPriorTraceAndSummary()
    {
        using var files = new TemporaryFiles();
        string path = files.Write(
            "missing-units.txt",
            "elapsed,signal\n0,1\n1,2\n");
        var session = new SessionState();
        var prior = new TraceData(
            [0d, 1d],
            [1e-6, 2e-6],
            TraceMetadata.Unknown);
        session.SetSiliconTrace(prior);
        var coordinator = new TraceImportCoordinator(
            new CancelSelections());
        var viewModel = new SiliconWorkflowViewModel(
            session,
            traceImports: coordinator);

        bool imported = await viewModel.ImportTraceAsync(path);

        Assert.IsFalse(imported);
        Assert.AreSame(prior, session.SiliconTrace);
        Assert.AreEqual("", viewModel.TraceImportSummary);
    }

    [TestMethod]
    public async Task ExternalImportCommand_PreservesCalculatedAndNeedsNoDialog()
    {
        using var files = new TemporaryFiles();
        string path = files.Write(
            "external.csv",
            "Wavelength (nm),IPCE (%)\n400,20\n500,120\n");
        var session = new SessionState();
        session.SetCalculatedIpce(
            Array.AsReadOnly(new[]
            {
                CreateIpcePoint(400, 10),
                CreateIpcePoint(500, 30),
            }));
        IReadOnlyList<IpcePoint> calculated = session.CalculatedIpce!;
        var viewModel = new SpectrumWorkflowViewModel(session);

        await viewModel.ImportExternalIpceCommand.ExecuteAsync(path);

        Assert.AreEqual(120d, viewModel.ExternalIpce!.Points[1].IpcePercent);
        Assert.AreSame(calculated, session.CalculatedIpce);
        Assert.AreEqual(
            IpceSource.External,
            session.SelectedIpceSource);
    }

    [TestMethod]
    public void SourceSelectionCommand_UpdatesObservableSelectionOnly()
    {
        var session = new SessionState();
        session.SetCalculatedIpce(
            Array.AsReadOnly(new[]
            {
                CreateIpcePoint(400, 10),
                CreateIpcePoint(500, 30),
            }));
        session.SetExternalIpce(new ExternalIpceData(
            [
                new IpceValue(400, 20),
                new IpceValue(500, 40),
            ],
            "",
            ""));
        var viewModel = new SpectrumWorkflowViewModel(session);
        IReadOnlyList<IpcePoint> calculated = session.CalculatedIpce!;
        ExternalIpceData external = session.ExternalIpce!;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);

        viewModel.SelectSourceCommand.Execute(IpceSource.External);

        Assert.AreEqual(IpceSource.External, viewModel.SelectedIpceSource);
        CollectionAssert.Contains(
            changedProperties,
            nameof(viewModel.SelectedIpceSource));
        Assert.AreSame(calculated, session.CalculatedIpce);
        Assert.AreSame(external, session.ExternalIpce);
    }

    [TestMethod]
    public void SiliconParameters_MarkOnlyMeasurementChainStale()
    {
        var session = new SessionState();
        var viewModel = new SiliconWorkflowViewModel(session);
        (string ExpectedReason, Action Change)[] changes =
        [
            ("起始波长", () => viewModel.WavelengthStartNanometres += 1),
            ("终止波长", () => viewModel.WavelengthEndNanometres -= 1),
            ("波长步长", () => viewModel.WavelengthStepNanometres += 1),
            ("时间对齐", () =>
                viewModel.AlignmentMode = AlignmentMode.FixedDelay),
            ("固定起点", () => viewModel.FixedStartTimeSeconds += 1),
            ("标称延时", () => viewModel.NominalDelaySeconds += 1),
            ("平均时长", () => viewModel.AveragingDurationSeconds += 1),
            ("暗电流", () => viewModel.SubtractDark = !viewModel.SubtractDark),
            ("暗区起点", () => viewModel.DarkStartSeconds += 1),
            ("暗区终点", () => viewModel.DarkEndSeconds += 1),
            ("硅面积", () => viewModel.AreaSquareCentimetres = 0.64),
        ];

        foreach ((string expectedReason, Action change) in changes)
        {
            session.SetPowerDensity(CreatePowerDensity());

            change();

            Assert.AreEqual(
                ResultFreshness.Stale,
                session.PowerDensityStatus.Freshness,
                expectedReason);
            StringAssert.Contains(
                session.PowerDensityStatus.Reason,
                expectedReason);
        }
    }

    [TestMethod]
    public void SampleAndIntegrationParameters_UseNarrowInvalidation()
    {
        var session = new SessionState();
        session.SetPowerDensity(CreatePowerDensity());
        var sample = new SampleWorkflowViewModel(session);
        (string ExpectedReason, Action Change)[] sampleChanges =
        [
            ("起始波长", () => sample.WavelengthStartNanometres += 1),
            ("终止波长", () => sample.WavelengthEndNanometres -= 1),
            ("波长步长", () => sample.WavelengthStepNanometres += 1),
            ("时间对齐", () =>
                sample.AlignmentMode = AlignmentMode.FixedDelay),
            ("固定起点", () => sample.FixedStartTimeSeconds += 1),
            ("标称延时", () => sample.NominalDelaySeconds += 1),
            ("平均时长", () => sample.AveragingDurationSeconds += 1),
            ("暗电流", () => sample.SubtractDark = !sample.SubtractDark),
            ("暗区起点", () => sample.DarkStartSeconds += 1),
            ("暗区终点", () => sample.DarkEndSeconds += 1),
            ("样品面积", () => sample.AreaSquareCentimetres = 0.75),
        ];
        foreach ((string expectedReason, Action change) in sampleChanges)
        {
            session.SetCalculatedIpce(CreateCalculatedIpce());

            change();

            Assert.AreEqual(
                ResultFreshness.Current,
                session.PowerDensityStatus.Freshness);
            Assert.AreEqual(
                ResultFreshness.Stale,
                session.CalculatedIpceStatus.Freshness,
                expectedReason);
            StringAssert.Contains(
                session.CalculatedIpceStatus.Reason,
                expectedReason);
        }

        session.SetExternalIpce(CreateExternalIpce());
        session.SetSpectrum(CreateSpectrum());
        session.SelectIpceSource(IpceSource.External);
        session.Integrate(400, 500);
        var spectrum = new SpectrumWorkflowViewModel(session);

        spectrum.IntegrationMinimumNanometres = 410;

        Assert.AreEqual(
            ResultFreshness.Stale,
            session.IntegrationStatus.Freshness);
        StringAssert.Contains(
            session.IntegrationStatus.Reason,
            "积分起点");
        session.Integrate(400, 500);

        spectrum.IntegrationMaximumNanometres = 490;

        Assert.AreEqual(
            ResultFreshness.Stale,
            session.IntegrationStatus.Freshness);
        StringAssert.Contains(
            session.IntegrationStatus.Reason,
            "积分终点");
    }

    [TestMethod]
    public void WorkflowMessages_ExplainMissingCurrentAndStaleStates()
    {
        var session = new SessionState();
        var main = new MainViewModel(session);

        StringAssert.Contains(main.Silicon.PrerequisiteMessage, "硅 i-t");
        StringAssert.Contains(main.Sample.PrerequisiteMessage, "样品 i-t");
        StringAssert.Contains(main.Spectrum.PrerequisiteMessage, "太阳光谱");
        StringAssert.Contains(main.Silicon.ResultStatusMessage, "尚未生成");

        session.SetPowerDensity(CreatePowerDensity());
        StringAssert.Contains(main.Silicon.ResultStatusMessage, "当前");
        main.Silicon.AreaSquareCentimetres = 0.64;
        StringAssert.Contains(
            main.Silicon.ResultStatusMessage,
            "需要重新计算");
        StringAssert.Contains(
            main.Silicon.ResultStatusMessage,
            "硅面积");
    }

    private static IReadOnlyList<PowerDensityPoint>
        CreatePowerDensity() =>
    [
        new(
            400,
            0.5,
            1e-6,
            1e-6,
            1e-6,
            0,
            0.36,
            1e-5,
            0,
            2),
        new(
            500,
            0.5,
            1e-6,
            1e-6,
            1e-6,
            0,
            0.36,
            1e-5,
            0,
            2),
    ];

    private static IReadOnlyList<IpcePoint> CreateCalculatedIpce() =>
    [
        CreateIpcePoint(400, 20),
        CreateIpcePoint(500, 50),
    ];

    private static ExternalIpceData CreateExternalIpce() =>
        new(
            [
                new IpceValue(400, 25),
                new IpceValue(500, 75),
            ],
            "Wavelength (nm)",
            "IPCE (%)");

    private static IReadOnlyList<SpectrumPoint> CreateSpectrum() =>
    [
        new SpectrumPoint(400, 1),
        new SpectrumPoint(500, 1),
    ];

    private static IpcePoint CreateIpcePoint(
        double wavelengthNm,
        double ipcePercent) =>
        new(
            wavelengthNm,
            1e-4,
            0,
            false,
            1e-6,
            1e-6,
            1e-6,
            0,
            1,
            1e-6,
            0,
            2,
            ipcePercent,
            0);

    private sealed class RecordingSynchronizationContext
        : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(
            SendOrPostCallback callback,
            object? state)
        {
            PostCount++;
            callback(state);
        }
    }

    private sealed class CancelSelections : IImportSelectionService
    {
        public UnitOverrides? SelectTraceUnits(
            TraceImportInspection inspection) => null;
    }

    private sealed class TemporaryFiles : IDisposable
    {
        public TemporaryFiles()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"ipce-viewmodel-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        private string DirectoryPath { get; }

        public string Write(string fileName, string contents)
        {
            string path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(
                path,
                contents,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
