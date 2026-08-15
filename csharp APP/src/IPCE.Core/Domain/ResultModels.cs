using IPCE.Core.Errors;

namespace IPCE.Core.Domain;

public readonly record struct PowerDensityPoint(
    double WavelengthNm,
    double SiliconResponsivityAmperesPerWatt,
    double SiliconMeanCurrentAmperes,
    double SiliconPhotoCurrentSignedAmperes,
    double SiliconPhotocurrentAmperes,
    double SiliconPhotoCurrentStandardErrorAmperes,
    double SiliconIlluminatedAreaSquareCentimetres,
    double IncidentPowerDensityWattsPerSquareCentimetre,
    double IncidentPowerDensityStandardError,
    int SampleCount);

public readonly record struct IpcePoint(
    double WavelengthNm,
    double IncidentPowerDensityWattsPerSquareCentimetre,
    double IncidentPowerDensityStandardError,
    bool PowerDensityInterpolated,
    double SampleMeanCurrentAmperes,
    double SamplePhotoCurrentSignedAmperes,
    double SamplePhotocurrentAmperes,
    double SamplePhotoCurrentStandardErrorAmperes,
    double SampleIlluminatedAreaSquareCentimetres,
    double SamplePhotocurrentDensityAmperesPerSquareCentimetre,
    double SamplePhotoCurrentDensityStandardError,
    int SampleCount,
    double IpcePercent,
    double IpceEstimatedStandardErrorPercent);

public readonly record struct IpceValue(
    double WavelengthNm,
    double IpcePercent);

public readonly record struct SpectrumPoint(
    double WavelengthNm,
    double IrradianceWattsPerSquareMetrePerNanometre);

public enum IpceSource
{
    Calculated,
    External,
}

public sealed record ExternalIpceData
{
    public ExternalIpceData(
        IReadOnlyList<IpceValue> points,
        string wavelengthHeader,
        string ipceHeader)
    {
        ArgumentNullException.ThrowIfNull(points);
        IpceValue[] copiedPoints = points.ToArray();
        bool invalidPoint = copiedPoints.Any(point =>
            !double.IsFinite(point.WavelengthNm) ||
            !double.IsFinite(point.IpcePercent) ||
            point.WavelengthNm <= 0);
        bool invalidOrder = copiedPoints
            .Zip(copiedPoints.Skip(1), (left, right) =>
                right.WavelengthNm <= left.WavelengthNm)
            .Any(isInvalid => isInvalid);

        if (copiedPoints.Length < 2 || invalidPoint || invalidOrder)
        {
            throw new IpceException(
                "IPCE:InvalidExternalIPCE",
                "外部 IPCE 必须包含至少两个有限、波长严格递增的数据点。");
        }

        Points = Array.AsReadOnly(copiedPoints);
        WavelengthHeader = wavelengthHeader ?? "";
        IpceHeader = ipceHeader ?? "";
    }

    public IReadOnlyList<IpceValue> Points { get; }

    public string WavelengthHeader { get; }

    public string IpceHeader { get; }
}

public sealed record IntegrationSummary(
    double MinimumWavelengthNm,
    double MaximumWavelengthNm,
    double IntegratedCurrentDensityMilliamperePerSquareCentimetre,
    double IntegratedPowerWattsPerSquareMetre,
    int IntegrationGridPoints,
    string Interpolation);

public readonly record struct IntegrationCurvePoint(
    double WavelengthNm,
    double IrradianceWattsPerSquareMetrePerNanometre,
    double IpcePercent,
    double EqeFraction,
    double PhotonFluxPerSquareMetreSecondNanometre,
    double SpectralCurrentMilliamperePerSquareCentimetreNanometre,
    double CumulativeCurrentDensityMilliamperePerSquareCentimetre);

public sealed record IntegrationResult
{
    public IntegrationResult(
        IntegrationSummary summary,
        IReadOnlyList<IntegrationCurvePoint> curve)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(curve);
        Summary = summary;
        Curve = Array.AsReadOnly(curve.ToArray());
    }

    public IntegrationSummary Summary { get; }

    public IReadOnlyList<IntegrationCurvePoint> Curve { get; }
}
