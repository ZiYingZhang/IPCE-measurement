using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Extraction;
using IPCE.Core.Scheduling;
using IPCE.Desktop.Import;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.IO.Import;
using IPCE.IO.Startup;

namespace IPCE.Desktop.ViewModels;

public sealed class SampleWorkflowViewModel : ViewModelBase
{
    private double _wavelengthStartNanometres =
        DefaultConfiguration.Current.WavelengthStartNanometres;
    private double _wavelengthEndNanometres =
        DefaultConfiguration.Current.WavelengthEndNanometres;
    private double _wavelengthStepNanometres =
        DefaultConfiguration.Current.WavelengthStepNanometres;
    private AlignmentMode _alignmentMode = AlignmentMode.Anchors;
    private double _fixedStartTimeSeconds = 50d;
    private double _nominalDelaySeconds =
        DefaultConfiguration.Current.NominalDelaySeconds;
    private double _averagingDurationSeconds =
        DefaultConfiguration.Current.PostConfirmationAverageSeconds;
    private bool _subtractDark =
        DefaultConfiguration.Current.SubtractDark;
    private double _darkStartSeconds =
        DefaultConfiguration.Current.SampleDarkStartSeconds;
    private double _darkEndSeconds =
        DefaultConfiguration.Current.SampleDarkEndSeconds;
    private double _areaSquareCentimetres =
        DefaultConfiguration.Current.SampleAreaSquareCentimetres;
    private readonly TraceImportCoordinator _traceImports;
    private bool _isTraceImporting;
    private string _traceImportSummary = "";
    private string _traceFileName = "";

    public SampleWorkflowViewModel(
        SessionState session,
        SynchronizationContext? synchronizationContext = null,
        IUserOperationRunner? operations = null,
        TraceImportCoordinator? traceImports = null)
        : base(synchronizationContext)
    {
        Session = session ?? throw new ArgumentNullException(
            nameof(session));
        IUserOperationRunner operationRunner =
            operations ?? UserOperationRunner.CreateDefault();
        _traceImports = traceImports ?? new TraceImportCoordinator(
            new ImportSelectionService());
        EditableAnchors = [];
        SyncEditableAnchors();
        Session.PropertyChanged += OnSessionPropertyChanged;
        ImportTraceCommand = new SafeAsyncRelayCommand(
            operationRunner,
            "导入样品 i-t 数据",
            parameter => ImportTraceAsync(RequirePath(parameter)),
            HasPath);
        ImportAnchorsCommand = new SafeAsyncRelayCommand(
            operationRunner,
            "导入样品时间锚点",
            parameter => ImportAnchorsAsync(RequirePath(parameter)),
            HasPath);
        ApplyCalculatedIpceCommand = new RelayCommand(
            parameter => Session.SetCalculatedIpce(
                RequireValue<IReadOnlyList<IpcePoint>>(parameter)),
            parameter => parameter is IReadOnlyList<IpcePoint>);
        CalculateIpceCommand = new SafeRelayCommand(
            operationRunner,
            "计算样品 IPCE",
            _ => CalculateIpce(),
            _ => CanCalculateIpce);
    }

    public SessionState Session { get; }

    public TraceData? Trace => Session.SampleTrace;

    public bool IsTraceImporting
    {
        get => _isTraceImporting;
        private set
        {
            if (SetProperty(ref _isTraceImporting, value))
            {
                OnPropertyChanged(nameof(TraceImportSummary));
            }
        }
    }

    public string TraceImportSummary => IsTraceImporting
        ? "正在导入…"
        : _traceImportSummary;

    public string TraceFileName => _traceFileName;

    public IReadOnlyList<AnchorPoint>? Anchors =>
        Session.SampleAnchors;

    public ObservableCollection<AnchorRowViewModel>
        EditableAnchors { get; }

    public IReadOnlyList<IpcePoint>? CalculatedIpce =>
        Session.CalculatedIpce;

    public bool CanCalculateIpce =>
        Trace is not null &&
        Session.PowerDensityStatus.CanUse &&
        Session.PowerDensity is { Count: > 0 } &&
        (AlignmentMode == AlignmentMode.FixedDelay ||
            Anchors is { Count: > 0 });

    public string PrerequisiteMessage =>
        Trace is null
            ? "缺少：样品 i-t"
            : Session.PowerDensityStatus.Freshness ==
                ResultFreshness.Stale
                ? $"需要重新计算：{Session.PowerDensityStatus.Reason}"
                : !Session.PowerDensityStatus.CanUse
                    ? "缺少：当前功率密度"
                    : AlignmentMode == AlignmentMode.Anchors &&
                        Anchors is not { Count: > 0 }
                        ? "缺少：样品时间锚点"
                        : "可以计算：轨迹与调度覆盖将在执行前检查";

    public string ResultStatusMessage =>
        FormatResultStatus(
            Session.CalculatedIpceStatus,
            "样品 IPCE");

    public SchedulePreview? Preview
    {
        get
        {
            if (Trace is null)
            {
                return null;
            }

            try
            {
                return WorkflowPreviewBuilder.BuildSchedule(
                    Trace,
                    WorkflowCalculation.BuildWavelengths(
                        WavelengthStartNanometres,
                        WavelengthEndNanometres,
                        WavelengthStepNanometres),
                    AlignmentMode,
                    WorkflowCalculation.AnchorsOrEmpty(
                        AlignmentMode,
                        Anchors),
                    FixedStartTimeSeconds,
                    NominalDelaySeconds);
            }
            catch (IpceException)
            {
                return null;
            }
        }
    }

    public string CoverageMessage =>
        Preview?.Coverage.Message ?? PrerequisiteMessage;

    public bool? IsCoverageValid =>
        Preview?.Coverage.IsWithinCoverage;

    public double WavelengthStartNanometres
    {
        get => _wavelengthStartNanometres;
        set => SetMeasurementParameter(
            ref _wavelengthStartNanometres,
            value,
            "样品起始波长已改变");
    }

    public double WavelengthEndNanometres
    {
        get => _wavelengthEndNanometres;
        set => SetMeasurementParameter(
            ref _wavelengthEndNanometres,
            value,
            "样品终止波长已改变");
    }

    public double WavelengthStepNanometres
    {
        get => _wavelengthStepNanometres;
        set => SetMeasurementParameter(
            ref _wavelengthStepNanometres,
            value,
            "样品波长步长已改变");
    }

    public AlignmentMode AlignmentMode
    {
        get => _alignmentMode;
        set
        {
            if (SetProperty(ref _alignmentMode, value))
            {
                Session.MarkCalculatedIpceStale(
                    "样品时间对齐方式已改变");
                OnPropertyChanged(nameof(CanCalculateIpce));
                OnPropertyChanged(nameof(PrerequisiteMessage));
                OnPropertyChanged(nameof(ResultStatusMessage));
                NotifyPreviewChanged();
                CalculateIpceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double FixedStartTimeSeconds
    {
        get => _fixedStartTimeSeconds;
        set => SetMeasurementParameter(
            ref _fixedStartTimeSeconds,
            value,
            "样品固定起点已改变");
    }

    public double NominalDelaySeconds
    {
        get => _nominalDelaySeconds;
        set => SetMeasurementParameter(
            ref _nominalDelaySeconds,
            value,
            "样品标称延时已改变");
    }

    public double AveragingDurationSeconds
    {
        get => _averagingDurationSeconds;
        set => SetMeasurementParameter(
            ref _averagingDurationSeconds,
            value,
            "样品平均时长已改变");
    }

    public bool SubtractDark
    {
        get => _subtractDark;
        set => SetMeasurementParameter(
            ref _subtractDark,
            value,
            "样品暗电流扣除设置已改变");
    }

    public double DarkStartSeconds
    {
        get => _darkStartSeconds;
        set => SetMeasurementParameter(
            ref _darkStartSeconds,
            value,
            "样品暗区起点已改变");
    }

    public double DarkEndSeconds
    {
        get => _darkEndSeconds;
        set => SetMeasurementParameter(
            ref _darkEndSeconds,
            value,
            "样品暗区终点已改变");
    }

    public double AreaSquareCentimetres
    {
        get => _areaSquareCentimetres;
        set => SetMeasurementParameter(
            ref _areaSquareCentimetres,
            value,
            "样品面积已改变");
    }

    public IAsyncCommand ImportTraceCommand { get; }

    public IAsyncCommand ImportAnchorsCommand { get; }

    public RelayCommand ApplyCalculatedIpceCommand { get; }

    public SafeRelayCommand CalculateIpceCommand { get; }

    public async Task<bool> ImportTraceAsync(string path)
    {
        IsTraceImporting = true;
        try
        {
            TraceData? replacement =
                await _traceImports.ReadAsync(path);
            if (replacement is null)
            {
                return false;
            }

            await RunOnUiContextAsync(
                () =>
                {
                    Session.SetSampleTrace(replacement);
                    _traceFileName = Path.GetFileName(path);
                    _traceImportSummary =
                        FormatTraceSummary(path, replacement);
                    OnPropertyChanged(nameof(TraceFileName));
                    OnPropertyChanged(nameof(TraceImportSummary));
                });
            return true;
        }
        finally
        {
            IsTraceImporting = false;
        }
    }

    public async Task ImportAnchorsAsync(string path)
    {
        IReadOnlyList<AnchorPoint> replacement = await Task.Run(
            () => AnchorReader.Read(path));
        await RunOnUiContextAsync(
            () => Session.SetSampleAnchors(replacement));
    }

    public IReadOnlyList<IpcePoint> CalculateIpce()
    {
        TraceData trace = Trace ?? throw new IpceException(
            "IPCE:MissingSampleTrace",
            "尚未导入样品 i-t 数据。");
        IReadOnlyList<PowerDensityPoint> powerDensity =
            Session.PowerDensity ?? throw new IpceException(
                "IPCE:MissingPowerDensity",
                "尚未计算标准硅单色光功率密度。");
        if (Session.PowerDensityStatus.Freshness ==
            ResultFreshness.Stale)
        {
            throw new IpceException(
                "IPCE:StaleResult",
                $"功率密度已过期，需要重新计算：{Session.PowerDensityStatus.Reason}。");
        }

        IReadOnlyList<double> wavelengths =
            WorkflowCalculation.BuildWavelengths(
                WavelengthStartNanometres,
                WavelengthEndNanometres,
                WavelengthStepNanometres);
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            wavelengths,
            AlignmentMode,
            WorkflowCalculation.AnchorsOrEmpty(
                AlignmentMode,
                Anchors),
            FixedStartTimeSeconds,
            NominalDelaySeconds);
        IReadOnlyList<ExtractedPoint> extracted = TraceExtractor.Extract(
            trace,
            schedule,
            AveragingDurationSeconds,
            new DarkCorrection(
                SubtractDark,
                DarkStartSeconds,
                DarkEndSeconds));
        IReadOnlyList<IpcePoint> replacement =
            IpceCalculator.CalculateIpce(
                powerDensity,
                extracted,
                AreaSquareCentimetres);
        Session.SetCalculatedIpce(replacement);
        return Session.CalculatedIpce!;
    }

    public double FindNearestSampleTime(double clickedTimeSeconds)
    {
        TraceData trace = Trace ?? throw new IpceException(
            "IPCE:MissingSampleTrace",
            "尚未导入样品 i-t 数据。");
        var controller = new PlotController(
            trace.TimeSeconds,
            trace.CurrentAmperes,
            "Time (s)",
            "Current (A)");
        return controller.FindNearestX(clickedTimeSeconds);
    }

    public void ConfirmAnchor(
        double wavelengthNm,
        double clickedTimeSeconds,
        double? adjustedTimeSeconds = null)
    {
        double snappedTime =
            FindNearestSampleTime(clickedTimeSeconds);
        double confirmedTime =
            adjustedTimeSeconds ?? snappedTime;
        AnchorPoint[] replacement = (Anchors ?? [])
            .Where(anchor => anchor.WavelengthNm != wavelengthNm)
            .Append(new AnchorPoint(wavelengthNm, confirmedTime))
            .OrderBy(anchor => anchor.WavelengthNm)
            .ToArray();
        Session.SetSampleAnchors(replacement);
    }

    public void ReplaceAnchors(
        IEnumerable<AnchorRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        AnchorPoint[] replacement = rows
            .Select(row => new AnchorPoint(
                row.WavelengthNm,
                row.ConfirmedTimeSeconds))
            .OrderBy(anchor => anchor.WavelengthNm)
            .ToArray();
        try
        {
            Session.SetSampleAnchors(replacement);
            SyncEditableAnchors();
        }
        catch
        {
            SyncEditableAnchors();
            throw;
        }
    }

    public void DeleteAnchor(AnchorRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ReplaceAnchors(EditableAnchors.Where(
            candidate => !ReferenceEquals(candidate, row)));
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        string? propertyName = eventArgs.PropertyName switch
        {
            nameof(SessionState.SampleTrace) => nameof(Trace),
            nameof(SessionState.SampleAnchors) => nameof(Anchors),
            nameof(SessionState.CalculatedIpce) =>
                nameof(CalculatedIpce),
            _ => null,
        };
        if (propertyName is not null)
        {
            OnPropertyChanged(propertyName);
        }

        if (eventArgs.PropertyName ==
            nameof(SessionState.SampleAnchors))
        {
            SyncEditableAnchors();
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.SampleTrace) or
            nameof(SessionState.SampleAnchors) or
            nameof(SessionState.PowerDensity) or
            nameof(SessionState.PowerDensityStatus))
        {
            OnPropertyChanged(nameof(PrerequisiteMessage));
            NotifyPreviewChanged();
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.CalculatedIpce) or
            nameof(SessionState.CalculatedIpceStatus))
        {
            OnPropertyChanged(nameof(ResultStatusMessage));
        }

        if (eventArgs.PropertyName is
            nameof(SessionState.SampleTrace) or
            nameof(SessionState.SampleAnchors) or
            nameof(SessionState.PowerDensity))
        {
            OnPropertyChanged(nameof(CanCalculateIpce));
            CalculateIpceCommand.RaiseCanExecuteChanged();
        }
    }

    private void SetMeasurementParameter<T>(
        ref T field,
        T value,
        string reason,
        [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Session.MarkCalculatedIpceStale(reason);
            OnPropertyChanged(nameof(ResultStatusMessage));
            NotifyPreviewChanged();
        }
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(CoverageMessage));
        OnPropertyChanged(nameof(IsCoverageValid));
    }

    private void SyncEditableAnchors()
    {
        EditableAnchors.Clear();
        foreach (AnchorPoint anchor in Anchors ?? [])
        {
            EditableAnchors.Add(new AnchorRowViewModel(
                anchor.WavelengthNm,
                anchor.ConfirmedTimeSeconds));
        }
    }

    private static string FormatResultStatus(
        ResultStatus status,
        string resultName) =>
        status.Freshness switch
        {
            ResultFreshness.Current => $"当前{resultName}可用",
            ResultFreshness.Stale =>
                $"需要重新计算：{status.Reason}",
            _ => $"尚未生成{resultName}",
        };

    private static string FormatTraceSummary(
        string path,
        TraceData trace)
    {
        string currentUnit = trace.Metadata.OriginalCurrentUnit
            .Replace("uA", "µA", StringComparison.Ordinal);
        return $"{Path.GetFileName(path)} · {trace.TimeSeconds.Count} 点 · " +
            $"{trace.TimeSeconds[0]:g6}–{trace.TimeSeconds[^1]:g6} s · " +
            $"{trace.Metadata.OriginalTimeUnit}/{currentUnit} 已换算为 s/A";
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
}
