using IPCE.Core.Errors;

namespace IPCE.Core.Domain;

public enum AlignmentMode
{
    FixedDelay,
    Anchors,
}

public readonly record struct AnchorPoint(
    double WavelengthNm,
    double ConfirmedTimeSeconds);

public sealed record AnchorData
{
    public AnchorData(IReadOnlyList<AnchorPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        AnchorPoint[] copiedPoints = points.ToArray();

        if (copiedPoints.Any(point =>
                !double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(point.ConfirmedTimeSeconds) ||
                point.WavelengthNm <= 0) ||
            copiedPoints
                .GroupBy(point => point.WavelengthNm)
                .Any(group => group.Skip(1).Any()))
        {
            throw new IpceException(
                "IPCE:InvalidAnchorFile",
                "锚点必须包含有限数值、正波长，并且锚点波长不能重复。");
        }

        Points = Array.AsReadOnly(copiedPoints);
    }

    public IReadOnlyList<AnchorPoint> Points { get; }
}

public readonly record struct SchedulePoint(
    double WavelengthNm,
    double ReferenceTimeSeconds,
    double WindowStartSeconds,
    double WindowEndSeconds,
    string AlignmentSource);

public readonly record struct DarkCorrection(
    bool Enabled,
    double StartSeconds,
    double EndSeconds);

public readonly record struct ExtractedPoint(
    double WavelengthNm,
    double MeanCurrentAmperes,
    double PhotoCurrentSignedAmperes,
    double AbsolutePhotoCurrentAmperes,
    double PhotoCurrentStandardErrorAmperes,
    int SampleCount);
