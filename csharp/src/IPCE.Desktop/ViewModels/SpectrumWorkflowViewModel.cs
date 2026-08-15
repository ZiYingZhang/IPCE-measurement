using System.ComponentModel;
using System.IO;
using System.Windows;
using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Import;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.IO.Export;
using IPCE.IO.Import;
using IPCE.IO.Startup;

namespace IPCE.Desktop.ViewModels;

public readonly record struct IntegrationRange(
    double MinimumWavelengthNm,
    double MaximumWavelengthNm);

public readonly record struct ExportRequest(
    string OutputPath,
    ExportFormat Format);

public sealed class SpectrumWorkflowViewModel : ViewModelBase
{
    private double _integrationMinimumNanometres =
        DefaultConfiguration.Current.IntegrationStartNanometres;
    private double _integrationMaximumNanometres =
        DefaultConfiguration.Current.IntegrationEndNanometres;
    private bool _includePowerDensityExport = true;
    private bool _includeCalculatedIpceExport = true;
    private bool _includeExternalIpceExport = true;
    private bool _includeIntegrationExport = true;
    private string _lastOperationKey = "Common.Ready";
    private object?[] _lastOperationArguments = [];
    private readonly SpectrumImportCoordinator _spectrumImports;
    private SpectrumImportResult? _spectrumImportMetadata;
    private string _spectrumImportSummary = "";
    private string _spectrumFileName = "";
    private string _externalIpceFileName = "";
    private readonly SiliconWorkflowViewModel? _siliconWorkflow;
    private readonly SampleWorkflowViewModel? _sampleWorkflow;
    private readonly LocalizedReasonFormatter _reasonFormatter;

    public SpectrumWorkflowViewModel(
        SessionState session,
        SynchronizationContext? synchronizationContext = null,
        IUserOperationRunner? operations = null,
        SpectrumImportCoordinator? spectrumImports = null,
        SiliconWorkflowViewModel? siliconWorkflow = null,
        SampleWorkflowViewModel? sampleWorkflow = null,
        ILocalizationService? localization = null)
        : base(synchronizationContext)
    {
        Session = session ?? throw new ArgumentNullException(
            nameof(session));
        Localization = localization ?? LocalizationService.Current;
        _reasonFormatter = new LocalizedReasonFormatter(Localization);
        IUserOperationRunner operationRunner =
            operations ?? UserOperationRunner.CreateDefault(Localization);
        _spectrumImports =
            spectrumImports ?? new SpectrumImportCoordinator(
                new ImportSelectionService(Localization));
        _siliconWorkflow = siliconWorkflow;
        _sampleWorkflow = sampleWorkflow;
        Session.PropertyChanged += OnSessionPropertyChanged;
        PropertyChangedEventManager.AddHandler(
            Localization,
            OnLocalizationPropertyChanged,
            "Item[]");
        ImportExternalIpceCommand = new SafeAsyncRelayCommand(
            operationRunner,
            () => Localization["Operation.ImportExternalIpce"],
            parameter => ImportExternalIpceAsync(RequirePath(parameter)),
            HasPath);
        ImportSpectrumCommand = new SafeAsyncRelayCommand(
            operationRunner,
            () => Localization["Operation.ImportSpectrum"],
            parameter => ImportSpectrumAsync(RequirePath(parameter)),
            HasPath);
        SelectSourceCommand = new RelayCommand(
            parameter => SelectedIpceSource =
                RequireValue<IpceSource>(parameter),
            parameter => parameter is IpceSource);
        IntegrateCommand = new SafeRelayCommand(
            operationRunner,
            () => Localization["Operation.Integrate"],
            parameter =>
            {
                if (parameter is IntegrationRange range)
                {
                    IntegrationMinimumNanometres =
                        range.MinimumWavelengthNm;
                    IntegrationMaximumNanometres =
                        range.MaximumWavelengthNm;
                }

                IntegrateSelectedSource();
            },
            _ => CanIntegrate);
        ExportCommand = new SafeRelayCommand(
            operationRunner,
            () => Localization["Operation.Export"],
            parameter =>
            {
                ExportRequest request =
                    RequireValue<ExportRequest>(parameter);
                ExportSelected(request.OutputPath, request.Format);
            },
            parameter => parameter is ExportRequest && CanExport);
    }

    public SessionState Session { get; }

    public ILocalizationService Localization { get; }

    public ExternalIpceData? ExternalIpce => Session.ExternalIpce;

    public IReadOnlyList<SpectrumPoint>? Spectrum => Session.Spectrum;

    public SpectrumImportResult? SpectrumImportMetadata =>
        _spectrumImportMetadata;

    public string SpectrumImportSummary =>
        _spectrumImportSummary.Length > 0 &&
        _spectrumImportMetadata is not null
            ? FormatSpectrumSummary(
                _spectrumImportSummary,
                _spectrumImportMetadata)
            : "";

    public string SpectrumFileName => _spectrumFileName;

    public string ExternalIpceFileName => _externalIpceFileName;

    public IpceSource SelectedIpceSource
    {
        get => Session.SelectedIpceSource;
        set => Session.SelectIpceSource(value);
    }

    public IntegrationResult? IntegrationResult =>
        Session.IntegrationResult;

    public bool CanIntegrate =>
        Spectrum is { Count: > 1 } &&
        (SelectedIpceSource == IpceSource.Calculated
            ? Session.CalculatedIpceStatus.CanUse &&
                Session.CalculatedIpce is { Count: > 0 }
            : ExternalIpce is not null);

    public string PrerequisiteMessage =>
        Spectrum is not { Count: > 1 }
            ? Localization["Prerequisite.MissingSpectrum"]
            : SelectedIpceSource == IpceSource.Calculated
                ? Session.CalculatedIpceStatus.Freshness ==
                    ResultFreshness.Stale
                    ? Localization.Format(
                        "Freshness.Stale",
                        _reasonFormatter.Format(
                            Session.CalculatedIpceStatus.Reason))
                    : !Session.CalculatedIpceStatus.CanUse
                        ? Localization["Prerequisite.MissingCalculatedIpce"]
                        : Localization["Prerequisite.CalculatedReady"]
                : ExternalIpce is null
                    ? Localization["Prerequisite.MissingExternalIpce"]
                    : Localization["Prerequisite.ExternalReady"];

    public string ResultStatusMessage =>
        Session.IntegrationStatus.Freshness switch
        {
            ResultFreshness.Current =>
                Localization["Freshness.CurrentIntegration"],
            ResultFreshness.Stale =>
                Localization.Format(
                    "Freshness.Stale",
                    _reasonFormatter.Format(
                        Session.IntegrationStatus.Reason)),
            _ => Localization["Freshness.MissingIntegration"],
        };

    private void OnLocalizationPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(PrerequisiteMessage));
        OnPropertyChanged(nameof(ResultStatusMessage));
        OnPropertyChanged(nameof(SpectrumImportSummary));
        OnPropertyChanged(nameof(LastOperationMessage));
        NotifyCoverageChanged();
    }

    public CoveragePreview? Coverage
    {
        get
        {
            if (Spectrum is not { Count: > 0 })
            {
                return null;
            }

            try
            {
                IReadOnlyList<IpceValue> ipce =
                    IpceSourceResolver.Resolve(
                        Session.CalculatedIpce,
                        ExternalIpce,
                        SelectedIpceSource);
                return WorkflowPreviewBuilder.BuildIntegrationCoverage(
                    ipce,
                    Spectrum,
                    IntegrationMinimumNanometres,
                    IntegrationMaximumNanometres,
                    Localization);
            }
            catch (IpceException)
            {
                return null;
            }
        }
    }

    public string CoverageMessage =>
        Coverage?.Message ?? PrerequisiteMessage;

    public bool? IsCoverageValid =>
        Coverage?.IsWithinCoverage;

    public bool CanExport =>
        (IncludePowerDensityExport &&
            Session.PowerDensityStatus.CanUse &&
            Session.PowerDensity is { Count: > 0 }) ||
        (IncludeCalculatedIpceExport &&
            Session.CalculatedIpceStatus.CanUse &&
            Session.CalculatedIpce is { Count: > 0 }) ||
        (IncludeExternalIpceExport && ExternalIpce is not null) ||
        (IncludeIntegrationExport &&
            Session.IntegrationStatus.CanUse &&
            IntegrationResult is not null);

    public double IntegrationMinimumNanometres
    {
        get => _integrationMinimumNanometres;
        set
        {
            if (SetProperty(
                ref _integrationMinimumNanometres,
                value))
            {
                Session.MarkIntegrationStale(
                    "积分起点已改变");
                OnPropertyChanged(nameof(ResultStatusMessage));
                NotifyCoverageChanged();
            }
        }
    }

    public double IntegrationMaximumNanometres
    {
        get => _integrationMaximumNanometres;
        set
        {
            if (SetProperty(
                ref _integrationMaximumNanometres,
                value))
            {
                Session.MarkIntegrationStale(
                    "积分终点已改变");
                OnPropertyChanged(nameof(ResultStatusMessage));
                NotifyCoverageChanged();
            }
        }
    }

    public bool IncludePowerDensityExport
    {
        get => _includePowerDensityExport;
        set => SetExportSelection(
            ref _includePowerDensityExport,
            value,
            nameof(IncludePowerDensityExport));
    }

    public bool IncludeCalculatedIpceExport
    {
        get => _includeCalculatedIpceExport;
        set => SetExportSelection(
            ref _includeCalculatedIpceExport,
            value,
            nameof(IncludeCalculatedIpceExport));
    }

    public bool IncludeExternalIpceExport
    {
        get => _includeExternalIpceExport;
        set => SetExportSelection(
            ref _includeExternalIpceExport,
            value,
            nameof(IncludeExternalIpceExport));
    }

    public bool IncludeIntegrationExport
    {
        get => _includeIntegrationExport;
        set => SetExportSelection(
            ref _includeIntegrationExport,
            value,
            nameof(IncludeIntegrationExport));
    }

    public string LastOperationMessage
    {
        get => Localization.Format(
            _lastOperationKey,
            _lastOperationArguments);
    }

    public IAsyncCommand ImportExternalIpceCommand { get; }

    public IAsyncCommand ImportSpectrumCommand { get; }

    public RelayCommand SelectSourceCommand { get; }

    public SafeRelayCommand IntegrateCommand { get; }

    public SafeRelayCommand ExportCommand { get; }

    public async Task ImportExternalIpceAsync(string path)
    {
        ExternalIpceData replacement = await Task.Run(
            () => ExternalIpceReader.Read(path));
        await RunOnUiContextAsync(
            () =>
            {
                Session.SetExternalIpce(replacement);
                Session.SelectIpceSource(IpceSource.External);
                _externalIpceFileName = Path.GetFileName(path);
                OnPropertyChanged(nameof(ExternalIpceFileName));
                SetLastOperation(
                    "Status.ExternalIpceImported",
                    replacement.Points.Count);
            });
    }

    public async Task<bool> ImportSpectrumAsync(string path)
    {
        SpectrumImportResult? replacement =
            await _spectrumImports.ReadAsync(path);
        if (replacement is null)
        {
            return false;
        }

        await RunOnUiContextAsync(
            () =>
            {
                Session.SetSpectrum(replacement.Points);
                _spectrumImportMetadata = replacement;
                _spectrumFileName = Path.GetFileName(path);
                _spectrumImportSummary = path;
                OnPropertyChanged(nameof(SpectrumImportMetadata));
                OnPropertyChanged(nameof(SpectrumFileName));
                OnPropertyChanged(nameof(SpectrumImportSummary));
                SetLastOperation(
                    "Status.SpectrumImported",
                    replacement.Points.Count);
            });
        return true;
    }

    public IntegrationResult IntegrateSelectedSource()
    {
        IntegrationResult replacement = Session.Integrate(
            IntegrationMinimumNanometres,
            IntegrationMaximumNanometres);
        SetLastOperation(
            "Status.IntegrationCompleted",
            replacement.Summary
                .IntegratedCurrentDensityMilliamperePerSquareCentimetre);
        return replacement;
    }

    public IReadOnlyList<ExportTable> BuildSelectedExportTables()
    {
        var tables = new List<ExportTable>();
        if (IncludePowerDensityExport &&
            Session.PowerDensityStatus.CanUse &&
            Session.PowerDensity is { } powerDensity)
        {
            tables.Add(WorkflowExportTables.PowerDensity(powerDensity));
        }

        if (IncludeCalculatedIpceExport &&
            Session.CalculatedIpceStatus.CanUse &&
            Session.CalculatedIpce is { } calculatedIpce)
        {
            tables.Add(WorkflowExportTables.CalculatedIpce(
                calculatedIpce));
        }

        if (IncludeExternalIpceExport &&
            ExternalIpce is { } externalIpce)
        {
            tables.Add(WorkflowExportTables.ExternalIpce(externalIpce));
        }

        if (IncludeIntegrationExport &&
            Session.IntegrationStatus.CanUse &&
            IntegrationResult is { } integration)
        {
            tables.Add(WorkflowExportTables.SpectrumSummary(
                integration.Summary));
            tables.Add(WorkflowExportTables.SpectrumCurve(
                integration.Curve));
        }

        if (tables.Count == 0)
        {
            throw new IpceException(
                "IPCE:NoCurrentExportSelection",
                "所选结果已过期或尚未生成，请重新计算后导出。");
        }

        WorkflowExportSnapshot snapshot =
            WorkflowExportSnapshot.Build(
                _siliconWorkflow,
                _sampleWorkflow,
                this);
        tables.Add(WorkflowExportTables.MeasurementSettings(
            snapshot.Settings));
        if (snapshot.SiliconAnchors.Count > 0)
        {
            tables.Add(WorkflowExportTables.Anchors(
                "SiliconAnchors",
                snapshot.SiliconAnchors));
        }
        if (snapshot.SampleAnchors.Count > 0)
        {
            tables.Add(WorkflowExportTables.Anchors(
                "SampleAnchors",
                snapshot.SampleAnchors));
        }
        tables.Add(WorkflowExportTables.InputMetadata(snapshot.Inputs));

        return Array.AsReadOnly(tables.ToArray());
    }

    public IReadOnlyList<string> ExportSelected(
        string outputPath,
        ExportFormat format)
    {
        IReadOnlyList<string> written = ExportService.Write(
            BuildSelectedExportTables(),
            outputPath,
            format);
        SetLastOperation("Status.FilesExported", written.Count);
        return written;
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        string? propertyName = eventArgs.PropertyName switch
        {
            nameof(SessionState.ExternalIpce) =>
                nameof(ExternalIpce),
            nameof(SessionState.Spectrum) => nameof(Spectrum),
            nameof(SessionState.SelectedIpceSource) =>
                nameof(SelectedIpceSource),
            nameof(SessionState.IntegrationResult) =>
                nameof(IntegrationResult),
            _ => null,
        };
        if (propertyName is not null)
        {
            OnPropertyChanged(propertyName);
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.ExternalIpce) or
            nameof(SessionState.Spectrum) or
            nameof(SessionState.SelectedIpceSource) or
            nameof(SessionState.CalculatedIpce) or
            nameof(SessionState.CalculatedIpceStatus))
        {
            OnPropertyChanged(nameof(PrerequisiteMessage));
            NotifyCoverageChanged();
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.IntegrationResult) or
            nameof(SessionState.IntegrationStatus))
        {
            OnPropertyChanged(nameof(ResultStatusMessage));
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.ExternalIpce) or
            nameof(SessionState.Spectrum) or
            nameof(SessionState.SelectedIpceSource) or
            nameof(SessionState.CalculatedIpce))
        {
            OnPropertyChanged(nameof(CanIntegrate));
            IntegrateCommand.RaiseCanExecuteChanged();
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.PowerDensity) or
            nameof(SessionState.CalculatedIpce) or
            nameof(SessionState.ExternalIpce) or
            nameof(SessionState.IntegrationResult))
        {
            OnPropertyChanged(nameof(CanExport));
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    private void SetExportSelection(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            OnPropertyChanged(nameof(CanExport));
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyCoverageChanged()
    {
        OnPropertyChanged(nameof(Coverage));
        OnPropertyChanged(nameof(CoverageMessage));
        OnPropertyChanged(nameof(IsCoverageValid));
    }

    private void SetLastOperation(
        string key,
        params object?[] arguments)
    {
        _lastOperationKey = key;
        _lastOperationArguments = arguments;
        OnPropertyChanged(nameof(LastOperationMessage));
    }

    private static bool HasPath(object? parameter) =>
        parameter is string path && !string.IsNullOrWhiteSpace(path);

    private static string RequirePath(object? parameter) =>
        parameter as string
        ?? throw new ArgumentException(
            "导入命令需要文件路径参数。",
            nameof(parameter));

    private static T RequireValue<T>(object? parameter) =>
        parameter is T value
            ? value
            : throw new ArgumentException(
                $"命令参数必须为 {typeof(T).Name}。",
                nameof(parameter));

    private string FormatSpectrumSummary(
        string path,
        SpectrumImportResult result) => Localization.Format(
            "Status.SpectrumSummary",
            Path.GetFileName(path),
            result.Selection.SheetName,
            result.WavelengthHeader,
            result.IrradianceHeader,
            result.Points.Count,
            result.Points[0].WavelengthNm,
            result.Points[^1].WavelengthNm);
}
