using System.Windows;
using System.Windows.Threading;
using IPCE.Core.Domain;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Services;
using IPCE.Desktop.ViewModels;
using IPCE.Desktop.Views;

namespace IPCE.Desktop;

public partial class App : Application
{
    private readonly IUserNotificationService _notifications;
    private readonly LocalCrashLogger _crashLogger;
    private readonly ILocalizationService _localization;

    public App()
        : this(
            new UserNotificationService(),
            new LocalCrashLogger())
    {
    }

    public App(
        IUserNotificationService notifications,
        LocalCrashLogger crashLogger,
        ILocalizationService? localization = null)
    {
        _notifications = notifications ??
            throw new ArgumentNullException(nameof(notifications));
        _crashLogger = crashLogger ??
            throw new ArgumentNullException(nameof(crashLogger));
        _localization = localization ?? LocalizationService.Current;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException +=
            OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException +=
            OnUnobservedTaskException;
        base.OnStartup(e);
        if (e.Args.Any(argument =>
            string.Equals(
                argument,
                "--smoke-test",
                StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunSmokeTestAsync();
            return;
        }

        if (MainWindow is null)
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }

    private async Task RunSmokeTestAsync()
    {
        MainWindow? window = null;
        try
        {
            var viewModel = new MainViewModel();
            window = new MainWindow(
                viewModel,
                loadStartupDefaults: false);
            MainWindow = window;
            window.Show();
            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.ApplicationIdle);
            await viewModel.LoadStartupDefaultsAsync();
            if (!viewModel.IsStartupLoaded ||
                viewModel.Session.Calibration?.Points.Count != 161 ||
                viewModel.Session.SiliconTrace?.TimeSeconds.Count != 14002 ||
                viewModel.Session.SiliconAnchors is not { Count: > 0 } ||
                viewModel.Session.Spectrum?.Count != 2002)
            {
                throw new InvalidOperationException(
                    "Embedded startup-data smoke validation failed.");
            }

            if (viewModel.Silicon.CalculatePowerDensity().Count != 161)
            {
                throw new InvalidOperationException(
                    "Default silicon power-density smoke validation failed.");
            }

            ConfigureSyntheticSample(viewModel);
            if (viewModel.Sample.CalculateIpce().Count != 161)
            {
                throw new InvalidOperationException(
                    "Synthetic sample-IPCE smoke validation failed.");
            }

            viewModel.Session.SetExternalIpce(new ExternalIpceData(
                viewModel.Session.PowerDensity!
                    .Select(point => new IpceValue(
                        point.WavelengthNm,
                        60))
                    .ToArray(),
                "Wavelength (nm)",
                "IPCE (%)"));
            viewModel.Spectrum.SelectedIpceSource = IpceSource.External;
            viewModel.Spectrum.IntegrationMinimumNanometres = 300;
            viewModel.Spectrum.IntegrationMaximumNanometres = 1100;
            IntegrationResult integration =
                viewModel.Spectrum.IntegrateSelectedSource();
            if (integration.Summary
                    .IntegratedCurrentDensityMilliamperePerSquareCentimetre <= 0)
            {
                throw new InvalidOperationException(
                    "External-IPCE integration smoke validation failed.");
            }

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.ApplicationIdle);
            ResultTabs results = window.ResultsPanel;
            string[] requiredViews =
            [
                "SiliconTraceView",
                "SampleTraceView",
                "SchedulePlotView",
                "PowerDensityPlotView",
                "IpcePlotView",
                "SpectrumIntegrationPlotView",
            ];
            if (requiredViews.Any(name => results.FindName(name) is null))
            {
                throw new InvalidOperationException(
                    "Required plot controls were not constructed.");
            }

            if (viewModel.Spectrum.BuildSelectedExportTables().Count < 7)
            {
                throw new InvalidOperationException(
                    "Reproducible export-table smoke validation failed.");
            }

            window.Close();
            Shutdown(0);
        }
        catch (Exception exception)
        {
            TryLog(exception);
            if (window?.IsVisible == true)
            {
                window.Close();
            }

            Shutdown(1);
        }
    }

    private static void ConfigureSyntheticSample(MainViewModel viewModel)
    {
        const double hcOverQElectronVoltNanometres =
            1239.8419843320026;
        IReadOnlyList<PowerDensityPoint> power =
            viewModel.Session.PowerDensity!;
        var times = new List<double> { 0 };
        var currents = new List<double> { 0 };
        for (int index = 0; index < power.Count; index++)
        {
            double end = (index + 1) * 10;
            double current =
                power[index]
                    .IncidentPowerDensityWattsPerSquareCentimetre *
                0.5 *
                power[index].WavelengthNm /
                hcOverQElectronVoltNanometres;
            times.Add(end - 1);
            currents.Add(current);
            times.Add(end - 0.5);
            currents.Add(current);
            times.Add(end);
            currents.Add(0);
        }

        viewModel.Session.SetSampleTrace(new TraceData(
            times,
            currents,
            TraceMetadata.Unknown));
        viewModel.Sample.WavelengthStartNanometres = 300;
        viewModel.Sample.WavelengthEndNanometres = 1100;
        viewModel.Sample.WavelengthStepNanometres = 5;
        viewModel.Sample.AlignmentMode = AlignmentMode.FixedDelay;
        viewModel.Sample.FixedStartTimeSeconds = 0;
        viewModel.Sample.NominalDelaySeconds = 10;
        viewModel.Sample.AveragingDurationSeconds = 1;
        viewModel.Sample.SubtractDark = false;
        viewModel.Sample.AreaSquareCentimetres = 1;
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        string path = TryLog(eventArgs.Exception);
        _notifications.ShowError(
            _localization["App.ErrorTitle"],
            _localization.Format("App.UnhandledError", path));
        eventArgs.Handled = true;
    }

    private void OnAppDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            TryLog(exception);
        }
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs)
    {
        TryLog(eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private string TryLog(Exception exception)
    {
        try
        {
            return _crashLogger.Log(exception);
        }
        catch
        {
            return _localization["Error.LogWriteFailed"];
        }
    }
}
