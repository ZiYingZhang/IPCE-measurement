using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Import;

namespace IPCE.Desktop.State;

public sealed class SessionState : INotifyPropertyChanged
{
    private TraceData? _siliconTrace;
    private IReadOnlyList<AnchorPoint>? _siliconAnchors;
    private CalibrationData? _calibration;
    private IReadOnlyList<PowerDensityPoint>? _powerDensity;
    private ResultStatus _powerDensityStatus =
        new(ResultFreshness.Missing, "");
    private TraceData? _sampleTrace;
    private IReadOnlyList<AnchorPoint>? _sampleAnchors;
    private IReadOnlyList<IpcePoint>? _calculatedIpce;
    private ResultStatus _calculatedIpceStatus =
        new(ResultFreshness.Missing, "");
    private ExternalIpceData? _externalIpce;
    private IReadOnlyList<SpectrumPoint>? _spectrum;
    private IpceSource _selectedIpceSource = IpceSource.Calculated;
    private IntegrationResult? _integrationResult;
    private ResultStatus _integrationStatus =
        new(ResultFreshness.Missing, "");

    public event PropertyChangedEventHandler? PropertyChanged;

    public TraceData? SiliconTrace => _siliconTrace;

    public IReadOnlyList<AnchorPoint>? SiliconAnchors =>
        _siliconAnchors;

    public CalibrationData? Calibration => _calibration;

    public IReadOnlyList<PowerDensityPoint>? PowerDensity =>
        _powerDensity;

    public ResultStatus PowerDensityStatus => _powerDensityStatus;

    public TraceData? SampleTrace => _sampleTrace;

    public IReadOnlyList<AnchorPoint>? SampleAnchors => _sampleAnchors;

    public IReadOnlyList<IpcePoint>? CalculatedIpce =>
        _calculatedIpce;

    public ResultStatus CalculatedIpceStatus =>
        _calculatedIpceStatus;

    public ExternalIpceData? ExternalIpce => _externalIpce;

    public IReadOnlyList<SpectrumPoint>? Spectrum => _spectrum;

    public IpceSource SelectedIpceSource => _selectedIpceSource;

    public IntegrationResult? IntegrationResult => _integrationResult;

    public ResultStatus IntegrationStatus => _integrationStatus;

    public TraceData ImportSiliconTrace(
        string path,
        UnitOverrides? overrides = null)
    {
        TraceData replacement = ItTraceReader.Read(path, overrides);
        SetSiliconTrace(replacement);
        return replacement;
    }

    public TraceData ImportSampleTrace(
        string path,
        UnitOverrides? overrides = null)
    {
        TraceData replacement = ItTraceReader.Read(path, overrides);
        SetSampleTrace(replacement);
        return replacement;
    }

    public IReadOnlyList<AnchorPoint> ImportSiliconAnchors(string path)
    {
        IReadOnlyList<AnchorPoint> replacement =
            AnchorReader.Read(path);
        SetSiliconAnchors(replacement);
        return _siliconAnchors!;
    }

    public IReadOnlyList<AnchorPoint> ImportSampleAnchors(string path)
    {
        IReadOnlyList<AnchorPoint> replacement =
            AnchorReader.Read(path);
        SetSampleAnchors(replacement);
        return _sampleAnchors!;
    }

    public CalibrationData ImportCalibration(string path)
    {
        CalibrationData replacement = CalibrationReader.Read(path);
        SetCalibration(replacement);
        return replacement;
    }

    public ExternalIpceData ImportExternalIpce(string path)
    {
        ExternalIpceData replacement = ExternalIpceReader.Read(path);
        SetExternalIpce(replacement);
        return replacement;
    }

    public IReadOnlyList<SpectrumPoint> ImportSpectrum(
        string path,
        string sheetName = "Spectra",
        int wavelengthColumn = 1,
        int irradianceColumn = 3)
    {
        IReadOnlyList<SpectrumPoint> replacement = SpectrumReader.Read(
            path,
            sheetName,
            wavelengthColumn,
            irradianceColumn);
        SetSpectrum(replacement);
        return _spectrum!;
    }

    public void SetSiliconTrace(TraceData replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Assign(ref _siliconTrace, replacement, nameof(SiliconTrace));
        InvalidatePowerDensity();
    }

    public void SetSiliconAnchors(
        IReadOnlyList<AnchorPoint> replacement)
    {
        AnchorPoint[] validated = ValidateAnchors(replacement);
        Assign(
            ref _siliconAnchors,
            Array.AsReadOnly(validated),
            nameof(SiliconAnchors));
        InvalidatePowerDensity();
    }

    public void SetCalibration(CalibrationData replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Assign(ref _calibration, replacement, nameof(Calibration));
        InvalidatePowerDensity();
    }

    public void SetPowerDensity(
        IReadOnlyList<PowerDensityPoint> replacement)
    {
        PowerDensityPoint[] validated =
            ValidatePowerDensity(replacement);
        Assign(
            ref _powerDensity,
            Array.AsReadOnly(validated),
            nameof(PowerDensity));
        SetPowerDensityStatus(
            new ResultStatus(ResultFreshness.Current, ""));
        InvalidateCalculatedIpce();
    }

    public void SetSampleTrace(TraceData replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Assign(ref _sampleTrace, replacement, nameof(SampleTrace));
        InvalidateCalculatedIpce();
    }

    public void SetSampleAnchors(
        IReadOnlyList<AnchorPoint> replacement)
    {
        AnchorPoint[] validated = ValidateAnchors(replacement);
        Assign(
            ref _sampleAnchors,
            Array.AsReadOnly(validated),
            nameof(SampleAnchors));
        InvalidateCalculatedIpce();
    }

    public void SetCalculatedIpce(
        IReadOnlyList<IpcePoint> replacement)
    {
        IpcePoint[] validated = ValidateCalculatedIpce(replacement);
        Assign(
            ref _calculatedIpce,
            Array.AsReadOnly(validated),
            nameof(CalculatedIpce));
        SetCalculatedIpceStatus(
            new ResultStatus(ResultFreshness.Current, ""));
        if (_selectedIpceSource == IpceSource.Calculated)
        {
            MarkIntegrationStale("计算 IPCE 已更新");
        }
    }

    public void SetExternalIpce(ExternalIpceData replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Assign(
            ref _externalIpce,
            replacement,
            nameof(ExternalIpce));
        if (_selectedIpceSource == IpceSource.External)
        {
            MarkIntegrationStale("外部 IPCE 已更新");
        }
    }

    public void SetSpectrum(IReadOnlyList<SpectrumPoint> replacement)
    {
        SpectrumPoint[] validated = ValidateSpectrum(replacement);
        Assign(
            ref _spectrum,
            Array.AsReadOnly(validated),
            nameof(Spectrum));
        MarkIntegrationStale("太阳光谱已更新");
    }

    public void SelectIpceSource(IpceSource source)
    {
        if (!Enum.IsDefined(source))
        {
            throw new IpceException(
                "IPCE:UnknownIPCESource",
                $"未知的 IPCE 来源：{source}");
        }

        if (_selectedIpceSource == source)
        {
            return;
        }

        _selectedIpceSource = source;
        OnPropertyChanged(nameof(SelectedIpceSource));
        MarkIntegrationStale("积分使用的 IPCE 来源已改变");
    }

    public IntegrationResult Integrate(
        double minimumWavelengthNm,
        double maximumWavelengthNm)
    {
        if (_selectedIpceSource == IpceSource.Calculated &&
            _calculatedIpceStatus.Freshness ==
                ResultFreshness.Stale)
        {
            throw StaleResult(_calculatedIpceStatus.Reason);
        }

        IReadOnlyList<IpceValue> selected = IpceSourceResolver.Resolve(
            _calculatedIpce,
            _externalIpce,
            _selectedIpceSource);
        if (_spectrum is null)
        {
            throw new IpceException(
                "IPCE:MissingSpectrum",
                "尚未导入可用于积分的太阳光谱。");
        }

        IntegrationResult replacement = SpectrumIntegrator.Integrate(
            selected,
            _spectrum,
            minimumWavelengthNm,
            maximumWavelengthNm);
        Assign(
            ref _integrationResult,
            replacement,
            nameof(IntegrationResult));
        SetIntegrationStatus(
            new ResultStatus(ResultFreshness.Current, ""));
        return replacement;
    }

    public void MarkPowerDensityStale(string reason)
    {
        if (_powerDensity is not null)
        {
            SetPowerDensityStatus(
                new ResultStatus(
                    ResultFreshness.Stale,
                    NormalizeReason(reason)));
        }

        MarkCalculatedIpceStale(reason);
    }

    public void MarkCalculatedIpceStale(string reason)
    {
        if (_calculatedIpce is not null)
        {
            SetCalculatedIpceStatus(
                new ResultStatus(
                    ResultFreshness.Stale,
                    NormalizeReason(reason)));
        }

        if (_selectedIpceSource == IpceSource.Calculated)
        {
            MarkIntegrationStale(reason);
        }
    }

    public void MarkIntegrationStale(string reason)
    {
        if (_integrationResult is not null)
        {
            SetIntegrationStatus(
                new ResultStatus(
                    ResultFreshness.Stale,
                    NormalizeReason(reason)));
        }
    }

    private void InvalidatePowerDensity() =>
        MarkPowerDensityStale("硅测量输入已更新");

    private void InvalidateCalculatedIpce() =>
        MarkCalculatedIpceStale("样品计算输入已更新");

    private void SetPowerDensityStatus(ResultStatus replacement)
    {
        if (_powerDensityStatus == replacement)
        {
            return;
        }

        _powerDensityStatus = replacement;
        OnPropertyChanged(nameof(PowerDensityStatus));
        OnPropertyChanged(nameof(PowerDensity));
    }

    private void SetCalculatedIpceStatus(ResultStatus replacement)
    {
        if (_calculatedIpceStatus == replacement)
        {
            return;
        }

        _calculatedIpceStatus = replacement;
        OnPropertyChanged(nameof(CalculatedIpceStatus));
        OnPropertyChanged(nameof(CalculatedIpce));
    }

    private void SetIntegrationStatus(ResultStatus replacement)
    {
        if (_integrationStatus == replacement)
        {
            return;
        }

        _integrationStatus = replacement;
        OnPropertyChanged(nameof(IntegrationStatus));
        OnPropertyChanged(nameof(IntegrationResult));
    }

    private void Assign<T>(
        ref T field,
        T value,
        string propertyName)
    {
        if (ReferenceEquals(field, value)
            || EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

    private static string NormalizeReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? "相关输入已改变"
            : reason.Trim();

    private static IpceException StaleResult(string reason) =>
        new(
            "IPCE:StaleResult",
            $"结果已过期，需要重新计算：{NormalizeReason(reason)}。");

    private static AnchorPoint[] ValidateAnchors(
        IReadOnlyList<AnchorPoint> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var validated = new AnchorData(replacement);
        return validated.Points.ToArray();
    }

    private static PowerDensityPoint[] ValidatePowerDensity(
        IReadOnlyList<PowerDensityPoint> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        PowerDensityPoint[] points = replacement.ToArray();
        bool invalid = points.Length == 0 ||
            points.Any(point =>
                !double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(
                    point.IncidentPowerDensityWattsPerSquareCentimetre) ||
                point.WavelengthNm <= 0 ||
                point.IncidentPowerDensityWattsPerSquareCentimetre <= 0) ||
            points.Zip(points.Skip(1), (left, right) =>
                right.WavelengthNm <= left.WavelengthNm).Any(value => value);
        if (invalid)
        {
            throw new IpceException(
                "IPCE:InvalidPowerDensity",
                "功率密度必须按正波长严格递增且包含有限正值。");
        }

        return points;
    }

    private static IpcePoint[] ValidateCalculatedIpce(
        IReadOnlyList<IpcePoint> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        IpcePoint[] points = replacement.ToArray();
        bool invalid = points.Length == 0 ||
            points.Any(point =>
                !double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(point.IpcePercent) ||
                point.WavelengthNm <= 0) ||
            points.Zip(points.Skip(1), (left, right) =>
                right.WavelengthNm <= left.WavelengthNm).Any(value => value);
        if (invalid)
        {
            throw new IpceException(
                "IPCE:InvalidIPCEResult",
                "计算 IPCE 必须按正波长严格递增且包含有限值。");
        }

        return points;
    }

    private static SpectrumPoint[] ValidateSpectrum(
        IReadOnlyList<SpectrumPoint> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        SpectrumPoint[] points = replacement.ToArray();
        bool invalid = points.Length < 2 ||
            points.Any(point =>
                !double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(
                    point.IrradianceWattsPerSquareMetrePerNanometre) ||
                point.WavelengthNm <= 0 ||
                point.IrradianceWattsPerSquareMetrePerNanometre < 0) ||
            points.Zip(points.Skip(1), (left, right) =>
                right.WavelengthNm <= left.WavelengthNm).Any(value => value);
        if (invalid)
        {
            throw new IpceException(
                "IPCE:InvalidSpectrum",
                "光谱必须按正波长严格递增且辐照度为有限非负值。");
        }

        return points;
    }
}
