using System.ComponentModel;
using System.IO;
using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Import;
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
    private string _lastOperationMessage = "就绪";
    private readonly SpectrumImportCoordinator _spectrumImports;
    private SpectrumImportResult? _spectrumImportMetadata;
    private string _spectrumImportSummary = "";
    private string _spectrumFileName = "";
    private string _externalIpceFileName = "";
    private readonly SiliconWorkflowViewModel? _siliconWorkflow;
    private readonly SampleWorkflowViewModel? _sampleWorkflow;

    public SpectrumWorkflowViewModel(
        SessionState session,
        SynchronizationContext? synchronizationContext = null,
        IUserOperationRunner? operations = null,
        SpectrumImportCoordinator? spectrumImports = null,
        SiliconWorkflowViewModel? siliconWorkflow = null,
        SampleWorkflowViewModel? sampleWorkflow = null)
        : base(synchronizationContext)
    {
        Session = session ?? throw new ArgumentNullException(
            nameof(session));
        IUserOperationRunner operationRunner =
            operations ?? UserOperationRunner.CreateDefault();
        _spectrumImports =
            spectrumImports ?? new SpectrumImportCoordinator(
                new ImportSelectionService());
        _siliconWorkflow = siliconWorkflow;
        _sampleWorkflow = sampleWorkflow;
        Session.PropertyChanged += OnSessionPropertyChanged;
        ImportExternalIpceCommand = new SafeAsyncRelayCommand(
            operationRunner,
            "导入外部 IPCE 数据",
            parameter => ImportExternalIpceAsync(RequirePath(parameter)),
            HasPath);
        ImportSpectrumCommand = new SafeAsyncRelayCommand(
            operationRunner,
            "导入太阳光谱",
            parameter => ImportSpectrumAsync(RequirePath(parameter)),
            HasPath);
        SelectSourceCommand = new RelayCommand(
            parameter => SelectedIpceSource =
                RequireValue<IpceSource>(parameter),
            parameter => parameter is IpceSource);
        IntegrateCommand = new SafeRelayCommand(
            operationRunner,
            "计算积分电流密度",
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
            "导出结果",
            parameter =>
            {
                ExportRequest request =
                    RequireValue<ExportRequest>(parameter);
                ExportSelected(request.OutputPath, request.Format);
            },
            parameter => parameter is ExportRequest && CanExport);
    }

    public SessionState Session { get; }

    public ExternalIpceData? ExternalIpce => Session.ExternalIpce;

    public IReadOnlyList<SpectrumPoint>? Spectrum => Session.Spectrum;

    public SpectrumImportResult? SpectrumImportMetadata =>
        _spectrumImportMetadata;

    public string SpectrumImportSummary => _spectrumImportSummary;

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
            ? "缺少：太阳光谱"
            : SelectedIpceSource == IpceSource.Calculated
                ? Session.CalculatedIpceStatus.Freshness ==
                    ResultFreshness.Stale
                    ? $"需要重新计算：{Session.CalculatedIpceStatus.Reason}"
                    : !Session.CalculatedIpceStatus.CanUse
                        ? "缺少：当前计算 IPCE"
                        : "可以积分：计算 IPCE 与光谱已就绪"
                : ExternalIpce is null
                    ? "缺少：外部 IPCE"
                    : "可以积分：外部 IPCE 与光谱已就绪";

    public string ResultStatusMessage =>
        Session.IntegrationStatus.Freshness switch
        {
            ResultFreshness.Current => "当前积分结果可用",
            ResultFreshness.Stale =>
                $"需要重新计算：{Session.IntegrationStatus.Reason}",
            _ => "尚未生成积分结果",
        };

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
                    IntegrationMaximumNanometres);
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
        get => _lastOperationMessage;
        private set => SetProperty(ref _lastOperationMessage, value);
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
                LastOperationMessage =
                    $"已导入 {replacement.Points.Count} 个外部 IPCE 点";
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
                _spectrumImportSummary =
                    FormatSpectrumSummary(path, replacement);
                OnPropertyChanged(nameof(SpectrumImportMetadata));
                OnPropertyChanged(nameof(SpectrumFileName));
                OnPropertyChanged(nameof(SpectrumImportSummary));
                LastOperationMessage =
                    $"已导入 {replacement.Points.Count} 个光谱点";
            });
        return true;
    }

    public IntegrationResult IntegrateSelectedSource()
    {
        IntegrationResult replacement = Session.Integrate(
            IntegrationMinimumNanometres,
            IntegrationMaximumNanometres);
        LastOperationMessage =
            $"积分完成：{replacement.Summary.IntegratedCurrentDensityMilliamperePerSquareCentimetre:g6} mA cm⁻²";
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
        LastOperationMessage = $"已导出 {written.Count} 个文件";
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

    private static string FormatSpectrumSummary(
        string path,
        SpectrumImportResult result) =>
        $"{Path.GetFileName(path)} · 表: {result.Selection.SheetName} · " +
        $"{result.WavelengthHeader} / {result.IrradianceHeader} · " +
        $"{result.Points.Count} 点 · " +
        $"{result.Points[0].WavelengthNm:g6}–" +
        $"{result.Points[^1].WavelengthNm:g6} nm";
}
