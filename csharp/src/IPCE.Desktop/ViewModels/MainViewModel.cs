using System.ComponentModel;
using System.IO;
using System.Windows;
using IPCE.Core.Domain;
using IPCE.Desktop.Import;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.IO.Import;
using IPCE.IO.Startup;

namespace IPCE.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private bool _isStartupLoading;
    private bool _isStartupLoaded;
    private string _startupError = "";

    public MainViewModel()
        : this(new SessionState(), null, null)
    {
    }

    public MainViewModel(
        SessionState session,
        SynchronizationContext? synchronizationContext = null,
        IUserOperationRunner? operations = null,
        TraceImportCoordinator? traceImports = null,
        SpectrumImportCoordinator? spectrumImports = null,
        ILocalizationService? localization = null)
        : base(synchronizationContext)
    {
        Session = session ?? throw new ArgumentNullException(
            nameof(session));
        Localization = localization ?? LocalizationService.Current;
        IUserOperationRunner operationRunner =
            operations ?? UserOperationRunner.CreateDefault(Localization);
        TraceImportCoordinator traceImportCoordinator =
            traceImports ?? new TraceImportCoordinator(
                new ImportSelectionService(Localization));
        SpectrumImportCoordinator spectrumImportCoordinator =
            spectrumImports ?? new SpectrumImportCoordinator(
                new ImportSelectionService(Localization));
        Silicon = new SiliconWorkflowViewModel(
            session,
            synchronizationContext,
            operationRunner,
            traceImportCoordinator,
            Localization);
        Sample = new SampleWorkflowViewModel(
            session,
            synchronizationContext,
            operationRunner,
            traceImportCoordinator,
            Localization);
        Spectrum = new SpectrumWorkflowViewModel(
            session,
            synchronizationContext,
            operationRunner,
            spectrumImportCoordinator,
            Silicon,
            Sample,
            Localization);
        Session.PropertyChanged += OnSessionPropertyChanged;
        PropertyChangedEventManager.AddHandler(
            Localization,
            OnLocalizationPropertyChanged,
            "Item[]");
    }

    public SessionState Session { get; }

    public ILocalizationService Localization { get; }

    public SiliconWorkflowViewModel Silicon { get; }

    public SampleWorkflowViewModel Sample { get; }

    public SpectrumWorkflowViewModel Spectrum { get; }

    public bool IsStartupLoading
    {
        get => _isStartupLoading;
        private set
        {
            if (SetProperty(ref _isStartupLoading, value))
            {
                OnPropertyChanged(nameof(StartupStatusMessage));
            }
        }
    }

    public bool IsStartupLoaded
    {
        get => _isStartupLoaded;
        private set
        {
            if (SetProperty(ref _isStartupLoaded, value))
            {
                OnPropertyChanged(nameof(StartupStatusMessage));
            }
        }
    }

    public string StartupError
    {
        get => _startupError;
        private set
        {
            if (SetProperty(ref _startupError, value))
            {
                OnPropertyChanged(nameof(StartupStatusMessage));
            }
        }
    }

    public string StartupStatusMessage => IsStartupLoading
        ? Localization["Startup.Loading"]
        : StartupError.Length > 0
            ? Localization.Format("Startup.Failed", StartupError)
            : IsStartupLoaded
                ? Localization["Startup.Loaded"]
                : Localization["Common.Ready"];

    public IpceSource SelectedIpceSource
    {
        get => Session.SelectedIpceSource;
        set => Session.SelectIpceSource(value);
    }

    public async Task LoadStartupDefaultsAsync(
        string? applicationDirectory = null)
    {
        if (IsStartupLoading)
        {
            return;
        }

        IsStartupLoading = true;
        IsStartupLoaded = false;
        StartupError = "";
        try
        {
            StartupBundle replacement = await Task.Run(
                () => ReadStartupBundle(applicationDirectory));
            await RunOnUiContextAsync(
                () =>
                {
                    Session.SetCalibration(replacement.Calibration);
                    Session.SetSiliconTrace(replacement.SiliconTrace);
                    Session.SetSiliconAnchors(
                        replacement.SiliconAnchors);
                    Session.SetSpectrum(replacement.Spectrum);
                    IsStartupLoaded = true;
                });
        }
        catch (Exception exception)
        {
            await RunOnUiContextAsync(
                () => StartupError = exception.Message);
            throw;
        }
        finally
        {
            await RunOnUiContextAsync(
                () => IsStartupLoading = false);
        }
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName ==
            nameof(SessionState.SelectedIpceSource))
        {
            OnPropertyChanged(nameof(SelectedIpceSource));
        }
    }

    private void OnLocalizationPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs) =>
        OnPropertyChanged(nameof(StartupStatusMessage));

    private static StartupBundle ReadStartupBundle(
        string? applicationDirectory)
    {
        DefaultConfiguration defaults = DefaultConfiguration.Current;
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string calibrationPath = ResolveReadablePath(
                defaults.CalibrationFileName,
                applicationDirectory,
                temporaryDirectory);
            string tracePath = ResolveReadablePath(
                defaults.SiliconTraceFileName,
                applicationDirectory,
                temporaryDirectory);
            string anchorPath = ResolveReadablePath(
                defaults.SiliconAnchorFileName,
                applicationDirectory,
                temporaryDirectory);
            string spectrumPath = ResolveReadablePath(
                defaults.SpectrumFileName,
                applicationDirectory,
                temporaryDirectory);

            return new StartupBundle(
                CalibrationReader.Read(calibrationPath),
                ItTraceReader.Read(tracePath),
                AnchorReader.Read(anchorPath),
                SpectrumReader.Read(
                    spectrumPath,
                    defaults.SpectrumWorksheet,
                    defaults.SpectrumWavelengthColumn,
                    defaults.SpectrumIrradianceColumn));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static string ResolveReadablePath(
        string fileName,
        string? applicationDirectory,
        string temporaryDirectory)
    {
        ResolvedStartupData resolved = StartupDataResolver.Resolve(
            fileName,
            applicationDirectory);
        if (!resolved.IsEmbedded)
        {
            return resolved.Source;
        }

        string path = Path.Combine(temporaryDirectory, fileName);
        File.WriteAllBytes(path, resolved.Content);
        return path;
    }

    private sealed record StartupBundle(
        CalibrationData Calibration,
        TraceData SiliconTrace,
        IReadOnlyList<AnchorPoint> SiliconAnchors,
        IReadOnlyList<SpectrumPoint> Spectrum);
}
