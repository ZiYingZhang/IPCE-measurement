using IPCE.Core.Errors;

namespace IPCE.Desktop.Plotting;

public enum PlotViewportMode
{
    Robust,
    Full,
}

public sealed record PlotViewportPolicy(
    double LowerQuantile = 0.005,
    double UpperQuantile = 0.995,
    double PaddingFraction = 0.08,
    double? PreferredMinimumX = null,
    double? PreferredMaximumX = null);

public readonly record struct PlotViewport(
    double MinimumX,
    double MaximumX,
    double MinimumY,
    double MaximumY,
    int ClippedYPointCount);

public static class PlotViewportCalculator
{
    public static PlotViewport Calculate(
        PlotModel model,
        PlotViewportPolicy policy,
        PlotViewportMode mode)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(policy);
        Validate(policy);

        List<(double X, double Y)> points = model.Series
            .Where(series => series.ContributesToAutoRange)
            .SelectMany(series => series.X
                .Zip(series.Y, (x, y) => (X: x, Y: y)))
            .ToList();

        if (points.Count == 0)
        {
            return new PlotViewport(0, 1, 0, 1, 0);
        }

        double rawMinimumX = policy.PreferredMinimumX ??
            points.Min(point => point.X);
        double rawMaximumX = policy.PreferredMaximumX ??
            points.Max(point => point.X);
        (double minimumX, double maximumX) = AddPadding(
            rawMinimumX,
            rawMaximumX,
            policy.PaddingFraction);

        List<double> yValues = points
            .Where(point =>
                point.X >= rawMinimumX &&
                point.X <= rawMaximumX)
            .Select(point => point.Y)
            .Order()
            .ToList();
        if (yValues.Count == 0)
        {
            yValues = points.Select(point => point.Y).Order().ToList();
        }

        double rawMinimumY;
        double rawMaximumY;
        if (mode == PlotViewportMode.Robust)
        {
            rawMinimumY = Quantile(yValues, policy.LowerQuantile);
            rawMaximumY = Quantile(yValues, policy.UpperQuantile);
        }
        else
        {
            rawMinimumY = yValues[0];
            rawMaximumY = yValues[^1];
        }

        (double minimumY, double maximumY) = AddPadding(
            rawMinimumY,
            rawMaximumY,
            policy.PaddingFraction);
        int clippedCount = mode == PlotViewportMode.Robust
            ? yValues.Count(value =>
                value < minimumY ||
                value > maximumY)
            : 0;

        return new PlotViewport(
            minimumX,
            maximumX,
            minimumY,
            maximumY,
            clippedCount);
    }

    private static void Validate(PlotViewportPolicy policy)
    {
        bool preferredBoundsMatch =
            policy.PreferredMinimumX.HasValue ==
            policy.PreferredMaximumX.HasValue;
        bool preferredBoundsValid =
            !policy.PreferredMinimumX.HasValue ||
            (double.IsFinite(policy.PreferredMinimumX.Value) &&
             double.IsFinite(policy.PreferredMaximumX!.Value) &&
             policy.PreferredMaximumX.Value >
             policy.PreferredMinimumX.Value);

        if (!double.IsFinite(policy.LowerQuantile) ||
            !double.IsFinite(policy.UpperQuantile) ||
            policy.LowerQuantile < 0 ||
            policy.UpperQuantile > 1 ||
            policy.LowerQuantile >= policy.UpperQuantile ||
            !double.IsFinite(policy.PaddingFraction) ||
            policy.PaddingFraction < 0 ||
            !preferredBoundsMatch ||
            !preferredBoundsValid)
        {
            throw new IpceException(
                "IPCE:InvalidViewportPolicy",
                "绘图视野参数无效。");
        }
    }

    private static double Quantile(
        IReadOnlyList<double> sortedValues,
        double quantile)
    {
        double position = quantile * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        double fraction = position - lowerIndex;
        return sortedValues[lowerIndex] +
            (sortedValues[upperIndex] - sortedValues[lowerIndex]) *
            fraction;
    }

    private static (double Minimum, double Maximum) AddPadding(
        double minimum,
        double maximum,
        double paddingFraction)
    {
        double span = maximum - minimum;
        if (span == 0)
        {
            double padding = Math.Max(Math.Abs(minimum) * 0.05, 1e-12);
            return (minimum - padding, maximum + padding);
        }

        double extra = span * paddingFraction;
        return (minimum - extra, maximum + extra);
    }
}
