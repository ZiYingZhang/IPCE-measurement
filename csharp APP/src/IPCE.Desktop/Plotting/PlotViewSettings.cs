using IPCE.Core.Errors;

namespace IPCE.Desktop.Plotting;

public sealed record PlotViewSettings(
    double? MinimumX,
    double? MaximumX,
    double? MinimumY,
    double? MaximumY,
    bool LogarithmicX,
    bool LogarithmicY)
{
    public void Validate(PlotModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ValidateLimits(MinimumX, MaximumX);
        ValidateLimits(MinimumY, MaximumY);

        bool invalidLogX =
            LogarithmicX &&
            (MinimumX is <= 0 ||
             MaximumX is <= 0 ||
             model.Series.SelectMany(series => series.X)
                 .Any(value => value <= 0));
        bool invalidLogY =
            LogarithmicY &&
            (MinimumY is <= 0 ||
             MaximumY is <= 0 ||
             model.Series.SelectMany(series => series.Y)
                 .Any(value => value <= 0));
        if (invalidLogX || invalidLogY)
        {
            throw new IpceException(
                "IPCE:InvalidAxisLimits",
                "数据或坐标范围包含非正值，不能使用对数轴。");
        }
    }

    private static void ValidateLimits(double? minimum, double? maximum)
    {
        if (minimum.HasValue != maximum.HasValue ||
            (minimum.HasValue &&
             (!double.IsFinite(minimum.Value) ||
              !double.IsFinite(maximum!.Value) ||
              maximum.Value <= minimum.Value)))
        {
            throw new IpceException(
                "IPCE:InvalidAxisLimits",
                "坐标轴上下限必须同时填写，上限必须大于下限，且所有范围必须为有效数值。");
        }
    }
}
