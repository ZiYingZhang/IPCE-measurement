using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using IPCE.Core.Domain;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.Desktop.Views;
using IPCE.Desktop.Views.Plots;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowSmokeTests
{
    [TestMethod]
    public void MainWindow_RemainsUsableAcrossRecoverableAndDispatcherErrors()
    {
        Exception? failure = null;
        string directory = TemporaryLogDirectory();
        var thread = new Thread(() =>
        {
            App? application = null;
            MainWindow? window = null;
            try
            {
                var notifications = new RecordingNotifications();
                var operations = new UserOperationRunner(
                    notifications,
                    new LocalCrashLogger(directory));
                var localization = new LocalizationService(
                    new LanguagePreferenceStore(
                        Path.Combine(directory, "settings.json")),
                    System.Globalization.CultureInfo.GetCultureInfo(
                        "zh-CN"));
                var viewModel = new MainViewModel(
                    CreateOutOfRangeSiliconSession(),
                    SynchronizationContext.Current,
                    operations,
                    localization: localization);
                application = new App(
                    notifications,
                    new LocalCrashLogger(directory))
                {
                    ShutdownMode = ShutdownMode.OnLastWindowClose,
                };
                application.InitializeComponent();
                window = new MainWindow(
                    viewModel,
                    loadStartupDefaults: false);
                application.MainWindow = window;
                window.Show();
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() =>
                    {
                        try
                        {
                            Assert.IsTrue(window.IsLoaded);
                            Assert.IsInstanceOfType<MainViewModel>(
                                window.DataContext);
                            Assert.IsNotNull(
                                window.FindName("WorkflowPanel"));
                            var results = Assert.IsInstanceOfType<ResultTabs>(
                                window.FindName("ResultsPanel"));
                            var siliconTrace =
                                Assert.IsInstanceOfType<TracePlotView>(
                                    results.FindName("SiliconTraceView"));
                            var sampleTrace =
                                Assert.IsInstanceOfType<TracePlotView>(
                                    results.FindName("SampleTraceView"));
                            var schedule =
                                Assert.IsInstanceOfType<SchedulePlotView>(
                                    results.FindName("SchedulePlotView"));
                            var powerDensity =
                                Assert.IsInstanceOfType<PowerDensityPlotView>(
                                    results.FindName("PowerDensityPlotView"));
                            var ipce =
                                Assert.IsInstanceOfType<IpcePlotView>(
                                    results.FindName("IpcePlotView"));
                            var integration =
                                Assert.IsInstanceOfType<
                                    SpectrumIntegrationPlotView>(
                                    results.FindName(
                                        "SpectrumIntegrationPlotView"));
                            AssertInteractivePlotShell(siliconTrace);
                            AssertInteractivePlotShell(sampleTrace);
                            AssertInteractivePlotShell(schedule);
                            AssertInteractivePlotShell(powerDensity);
                            AssertInteractivePlotShell(ipce);
                            AssertIntegrationPlotShell(integration);
                            var siliconAnchorGrid =
                                Assert.IsInstanceOfType<DataGrid>(
                                    results.FindName(
                                        "SiliconAnchorGrid"));
                            var sampleAnchorGrid =
                                Assert.IsInstanceOfType<DataGrid>(
                                    results.FindName(
                                        "SampleAnchorGrid"));
                            Assert.IsFalse(siliconAnchorGrid.IsReadOnly);
                            Assert.IsFalse(sampleAnchorGrid.IsReadOnly);
                            Assert.AreEqual(
                                2,
                                siliconAnchorGrid.Columns.Count);
                            Assert.AreEqual(
                                2,
                                sampleAnchorGrid.Columns.Count);
                            Assert.IsNotNull(
                                results.FindName(
                                    "ApplySiliconAnchorsButton"));
                            Assert.IsNotNull(
                                results.FindName(
                                    "ApplySampleAnchorsButton"));
                            Assert.AreEqual(
                                "IPCE 测量与光谱积分",
                                window.Title);

                            Assert.IsTrue(
                                viewModel.Silicon
                                    .CalculatePowerDensityCommand
                                    .CanExecute(null));
                            viewModel.Silicon
                                .CalculatePowerDensityCommand
                                .Execute(null);
                            Assert.AreEqual(
                                1,
                                notifications.Warnings.Count);
                            Assert.IsTrue(window.IsVisible);
                            Assert.IsFalse(
                                application.Dispatcher.HasShutdownStarted);
                            Assert.AreEqual(
                                0,
                                Directory.GetFiles(directory).Length);

                            window.Dispatcher.BeginInvoke(
                                DispatcherPriority.Normal,
                                new Action(() =>
                                    throw new InvalidOperationException(
                                        "dispatcher marker")));
                            window.Dispatcher.BeginInvoke(
                                DispatcherPriority.ApplicationIdle,
                                new Action(() =>
                                {
                                    try
                                    {
                                        Assert.AreEqual(
                                            1,
                                            notifications.Errors.Count);
                                        Assert.AreEqual(
                                            1,
                                            Directory.GetFiles(directory)
                                                .Length);
                                        Assert.IsTrue(window.IsVisible);
                                        Assert.IsFalse(
                                            application.Dispatcher
                                                .HasShutdownStarted);
                                    }
                                    catch (Exception exception)
                                    {
                                        failure = exception;
                                    }
                                    finally
                                    {
                                        window.Close();
                                    }
                                }));
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                            window.Close();
                        }
                    }));
                application.Run();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (window?.IsVisible == true)
                {
                    window.Close();
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(20)),
            "WPF smoke-test thread did not finish.");
        DeleteDirectory(directory);
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"WPF smoke test failed: {failure}",
                failure);
        }
    }

    [TestMethod]
    public async Task StartupDefaults_LoadAllFourIndependentInputs()
    {
        var viewModel = new MainViewModel();

        await viewModel.LoadStartupDefaultsAsync();

        Assert.IsTrue(viewModel.IsStartupLoaded);
        Assert.IsFalse(viewModel.IsStartupLoading);
        Assert.AreEqual("", viewModel.StartupError);
        Assert.IsNotNull(viewModel.Session.Calibration);
        Assert.IsNotNull(viewModel.Session.SiliconTrace);
        Assert.IsNotNull(viewModel.Session.SiliconAnchors);
        Assert.IsNotNull(viewModel.Session.Spectrum);
        Assert.AreEqual(
            AlignmentMode.Anchors,
            viewModel.Sample.AlignmentMode);
        Assert.AreEqual(161, viewModel.Session.Calibration.Points.Count);
        Assert.AreEqual(14002, viewModel.Session.SiliconTrace.TimeSeconds.Count);
        Assert.AreEqual(2002, viewModel.Session.Spectrum.Count);
        Assert.AreEqual(0.36, viewModel.Silicon.AreaSquareCentimetres);
        Assert.AreEqual(0.1, viewModel.Silicon.DarkStartSeconds);
        Assert.AreEqual(10, viewModel.Silicon.DarkEndSeconds);
        Assert.AreEqual(1, viewModel.Sample.AreaSquareCentimetres);
        Assert.AreEqual(50, viewModel.Sample.FixedStartTimeSeconds);
        Assert.AreEqual(50, viewModel.Sample.DarkStartSeconds);
        Assert.AreEqual(60, viewModel.Sample.DarkEndSeconds);
        Assert.AreEqual(300, viewModel.Spectrum.IntegrationMinimumNanometres);
        Assert.AreEqual(1100, viewModel.Spectrum.IntegrationMaximumNanometres);
    }

    [TestMethod]
    public void CrashLogger_WritesLocalDiagnosticFile()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-crash-log-{Guid.NewGuid():N}");
        try
        {
            var logger = new LocalCrashLogger(directory);

            string path = logger.Log(
                new InvalidOperationException("diagnostic marker"));

            Assert.IsTrue(File.Exists(path));
            string contents = File.ReadAllText(path);
            StringAssert.Contains(
                contents,
                nameof(InvalidOperationException));
            StringAssert.Contains(contents, "diagnostic marker");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static SessionState CreateOutOfRangeSiliconSession()
    {
        var session = new SessionState();
        session.SetSiliconTrace(new TraceData(
            [0d, 1d],
            [0d, 1e-6],
            TraceMetadata.Unknown));
        session.SetCalibration(new CalibrationData(
        [
            new CalibrationPoint(300, 1),
            new CalibrationPoint(1100, 1),
        ]));
        session.SetSiliconAnchors(
        [
            new AnchorPoint(300, 10),
            new AnchorPoint(1100, 20),
        ]);
        return session;
    }

    private static string TemporaryLogDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-ui-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AssertInteractivePlotShell(
        FrameworkElement view)
    {
        var hover = Assert.IsInstanceOfType<TextBlock>(
            view.FindName("HoverText"));
        Assert.IsGreaterThanOrEqualTo(14d, hover.FontSize);
        Assert.IsNotNull(view.FindName("ClippedText"));
        var toolbar = Assert.IsInstanceOfType<PlotToolbar>(
            view.FindName("Toolbar"));
        Assert.IsNotNull(toolbar.FindName("ShowAllButton"));
    }

    private static void AssertIntegrationPlotShell(
        SpectrumIntegrationPlotView view)
    {
        foreach (string prefix in
                 new[] { "Irradiance", "SelectedIpce", "Cumulative" })
        {
            var hover = Assert.IsInstanceOfType<TextBlock>(
                view.FindName($"{prefix}HoverText"));
            Assert.IsGreaterThanOrEqualTo(14d, hover.FontSize);
            Assert.IsNotNull(view.FindName($"{prefix}ClippedText"));
            var toolbar = Assert.IsInstanceOfType<PlotToolbar>(
                view.FindName($"{prefix}Toolbar"));
            Assert.IsNotNull(toolbar.FindName("ShowAllButton"));
        }
    }

    private sealed class RecordingNotifications
        : IUserNotificationService
    {
        public List<(string Title, string Message)> Warnings { get; } =
            [];

        public List<(string Title, string Message)> Errors { get; } =
            [];

        public void ShowWarning(string title, string message) =>
            Warnings.Add((title, message));

        public void ShowError(string title, string message) =>
            Errors.Add((title, message));
    }
}
