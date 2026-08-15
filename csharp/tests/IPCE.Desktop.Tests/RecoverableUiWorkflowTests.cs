using System.IO;
using IPCE.Core.Domain;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class RecoverableUiWorkflowTests
{
    [TestMethod]
    public void InvalidScheduleCanBeCorrectedAndStaleResultBlocksExport()
    {
        string logDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-recoverable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logDirectory);
        try
        {
            var notifications = new RecordingNotifications();
            var operations = new UserOperationRunner(
                notifications,
                new LocalCrashLogger(logDirectory));
            var session = new SessionState();
            session.SetPowerDensity(
            [
                PowerPoint(400),
                PowerPoint(500),
            ]);
            session.SetSampleTrace(new TraceData(
                [0d, 1d],
                [0d, 1e-6],
                TraceMetadata.Unknown));
            var viewModel = new MainViewModel(
                session,
                SynchronizationContext.Current,
                operations);
            ConfigureSample(viewModel.Sample);
            viewModel.Spectrum.IncludePowerDensityExport = false;
            viewModel.Spectrum.IncludeCalculatedIpceExport = true;
            viewModel.Spectrum.IncludeExternalIpceExport = false;
            viewModel.Spectrum.IncludeIntegrationExport = false;

            viewModel.Sample.CalculateIpceCommand.Execute(null);
            Assert.AreEqual(1, notifications.Warnings.Count);
            Assert.AreEqual(0, notifications.Errors.Count);
            Assert.AreEqual(0, Directory.GetFiles(logDirectory).Length);

            session.SetSampleTrace(CreateValidTrace());
            viewModel.Sample.CalculateIpce();
            Assert.AreEqual(
                ResultFreshness.Current,
                session.CalculatedIpceStatus.Freshness);
            Assert.IsTrue(viewModel.Spectrum.CanExport);

            viewModel.Sample.AreaSquareCentimetres = 0.36;
            Assert.AreEqual(
                ResultFreshness.Stale,
                session.CalculatedIpceStatus.Freshness);
            Assert.IsFalse(viewModel.Spectrum.CanExport);
        }
        finally
        {
            Directory.Delete(logDirectory, true);
        }
    }

    private static void ConfigureSample(SampleWorkflowViewModel sample)
    {
        sample.WavelengthStartNanometres = 400;
        sample.WavelengthEndNanometres = 500;
        sample.WavelengthStepNanometres = 100;
        sample.AlignmentMode = AlignmentMode.FixedDelay;
        sample.FixedStartTimeSeconds = 0;
        sample.NominalDelaySeconds = 10;
        sample.AveragingDurationSeconds = 1;
        sample.SubtractDark = false;
        sample.AreaSquareCentimetres = 1;
    }

    private static TraceData CreateValidTrace() => new(
        [0d, 9d, 9.5d, 10d, 19d, 19.5d, 20d],
        [0d, 1e-6, 1e-6, 0d, 1e-6, 1e-6, 0d],
        TraceMetadata.Unknown);

    private static PowerDensityPoint PowerPoint(double wavelength) => new(
        wavelength,
        1,
        1e-6,
        1e-6,
        1e-6,
        0,
        1,
        1e-5,
        0,
        2);

    private sealed class RecordingNotifications
        : IUserNotificationService
    {
        public List<(string Title, string Message)> Warnings { get; } = [];

        public List<(string Title, string Message)> Errors { get; } = [];

        public void ShowWarning(string title, string message) =>
            Warnings.Add((title, message));

        public void ShowError(string title, string message) =>
            Errors.Add((title, message));
    }
}
