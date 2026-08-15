using IPCE.Core.Errors;

namespace IPCE.Desktop.Plotting;

public readonly record struct PlotPixelPoint(double X, double Y);

public sealed record PlotHoverPoint(
    string SeriesLabel,
    int SeriesIndex,
    int PointIndex,
    double X,
    double Y,
    double PixelDistance,
    string Details);

public static class PlotHitTester
{
    public static PlotHoverPoint? FindNearest(
        PlotModel model,
        PlotPixelPoint pointer,
        Func<double, double, PlotPixelPoint> toPixel,
        double maximumDistancePixels = 12)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(toPixel);
        if (!double.IsFinite(maximumDistancePixels) ||
            maximumDistancePixels <= 0)
        {
            throw new IpceException(
                "IPCE:InvalidHitTestRadius",
                "数据点捕捉半径必须为大于零的有效数值。");
        }

        PlotHoverPoint? nearest = null;
        double nearestDistance = maximumDistancePixels;
        for (int seriesIndex = 0;
             seriesIndex < model.Series.Count;
             seriesIndex++)
        {
            PlotSeries series = model.Series[seriesIndex];
            for (int pointIndex = 0;
                 pointIndex < series.X.Count;
                 pointIndex++)
            {
                PlotPixelPoint pixel =
                    toPixel(series.X[pointIndex], series.Y[pointIndex]);
                if (!double.IsFinite(pixel.X) ||
                    !double.IsFinite(pixel.Y))
                {
                    continue;
                }

                double deltaX = pixel.X - pointer.X;
                double deltaY = pixel.Y - pointer.Y;
                double distance = Math.Sqrt(
                    deltaX * deltaX +
                    deltaY * deltaY);
                if (distance > nearestDistance)
                {
                    continue;
                }

                double x = series.X[pointIndex];
                double y = series.Y[pointIndex];
                nearestDistance = distance;
                nearest = new PlotHoverPoint(
                    series.Label,
                    seriesIndex,
                    pointIndex,
                    x,
                    y,
                    distance,
                    $"{series.Label}\nX = {x:G8}\nY = {y:G8}");
            }
        }

        return nearest;
    }
}
