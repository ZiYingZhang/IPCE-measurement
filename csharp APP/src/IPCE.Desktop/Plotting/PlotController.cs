using IPCE.Core.Errors;

namespace IPCE.Desktop.Plotting;

public readonly record struct PlotAxisSettings(
    double MinimumX,
    double MaximumX,
    double MinimumY,
    double MaximumY,
    bool LogarithmicX,
    bool LogarithmicY);

public sealed class PlotController
{
    private readonly PlotAxisSettings _dataAxis;

    public PlotController(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        string xLabel,
        string yLabel)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        double[] copiedX = x.ToArray();
        double[] copiedY = y.ToArray();
        if (copiedX.Length < 2 ||
            copiedX.Length != copiedY.Length ||
            copiedX.Any(value => !double.IsFinite(value)) ||
            copiedY.Any(value => !double.IsFinite(value)))
        {
            throw new IpceException(
                "IPCE:InvalidPlotData",
                "绘图数据必须包含至少两个成对的有限数值。");
        }

        X = Array.AsReadOnly(copiedX);
        Y = Array.AsReadOnly(copiedY);
        XLabel = xLabel ?? "";
        YLabel = yLabel ?? "";
        (double minimumX, double maximumX) =
            ExpandConstantRange(copiedX.Min(), copiedX.Max());
        (double minimumY, double maximumY) =
            ExpandConstantRange(copiedY.Min(), copiedY.Max());
        _dataAxis = new PlotAxisSettings(
            minimumX,
            maximumX,
            minimumY,
            maximumY,
            false,
            false);
        Axis = _dataAxis;
    }

    public IReadOnlyList<double> X { get; }

    public IReadOnlyList<double> Y { get; }

    public string XLabel { get; }

    public string YLabel { get; }

    public PlotAxisSettings Axis { get; private set; }

    public double FindNearestX(double clickedX)
    {
        if (!double.IsFinite(clickedX))
        {
            throw new IpceException(
                "IPCE:InvalidPlotCoordinate",
                "绘图点击坐标必须为有限数值。");
        }

        int nearestIndex = 0;
        double nearestDistance = Math.Abs(X[0] - clickedX);
        for (int index = 1; index < X.Count; index++)
        {
            double distance = Math.Abs(X[index] - clickedX);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = index;
            }
        }

        return X[nearestIndex];
    }

    public PlotAxisSettings SetAxis(PlotAxisSettings settings)
    {
        if (!double.IsFinite(settings.MinimumX) ||
            !double.IsFinite(settings.MaximumX) ||
            !double.IsFinite(settings.MinimumY) ||
            !double.IsFinite(settings.MaximumY) ||
            settings.MaximumX <= settings.MinimumX ||
            settings.MaximumY <= settings.MinimumY)
        {
            throw new IpceException(
                "IPCE:InvalidAxisLimits",
                "坐标轴上限必须大于下限，且所有范围必须为有限数值。");
        }

        if ((settings.LogarithmicX && settings.MinimumX <= 0) ||
            (settings.LogarithmicY && settings.MinimumY <= 0))
        {
            throw new IpceException(
                "IPCE:InvalidLogAxis",
                "对数坐标轴的上下限和数据必须大于零。");
        }

        Axis = settings;
        return Axis;
    }

    public PlotAxisSettings ResetAxis()
    {
        Axis = _dataAxis;
        return Axis;
    }

    private static (double Minimum, double Maximum)
        ExpandConstantRange(double minimum, double maximum)
    {
        if (maximum > minimum)
        {
            return (minimum, maximum);
        }

        double padding = Math.Abs(minimum) > 0
            ? Math.Abs(minimum) * 0.05
            : 1;
        return (minimum - padding, maximum + padding);
    }
}
