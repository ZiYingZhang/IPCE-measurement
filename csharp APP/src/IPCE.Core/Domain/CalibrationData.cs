using IPCE.Core.Errors;

namespace IPCE.Core.Domain;

public readonly record struct CalibrationPoint(
    double WavelengthNm,
    double ResponsivityAmperesPerWatt);

public sealed record CalibrationData
{
    public CalibrationData(IReadOnlyList<CalibrationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        CalibrationPoint[] copiedPoints = points.ToArray();

        bool invalidPoint = copiedPoints.Any(point =>
            !double.IsFinite(point.WavelengthNm) ||
            !double.IsFinite(point.ResponsivityAmperesPerWatt) ||
            point.WavelengthNm <= 0 ||
            point.ResponsivityAmperesPerWatt <= 0);
        bool invalidOrder = copiedPoints
            .Zip(copiedPoints.Skip(1), (left, right) =>
                right.WavelengthNm <= left.WavelengthNm)
            .Any(isInvalid => isInvalid);

        if (copiedPoints.Length < 2 || invalidPoint || invalidOrder)
        {
            throw new IpceException(
                "IPCE:InvalidReference",
                "校准数据必须包含至少两个有限、波长严格递增的正波长和正响应度数据点。");
        }

        Points = Array.AsReadOnly(copiedPoints);
    }

    public IReadOnlyList<CalibrationPoint> Points { get; }
}
