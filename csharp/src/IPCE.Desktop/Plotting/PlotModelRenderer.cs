using ScottPlot.WPF;

namespace IPCE.Desktop.Plotting;

public static class PlotModelRenderer
{
    public static void Render(
        WpfPlot target,
        PlotModel model,
        PlotViewport viewport,
        PlotViewSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(model);
        settings?.Validate(model);

        target.Plot.Clear();
        foreach (PlotBand band in model.Bands)
        {
            double minimum = TransformX(band.MinimumX, settings);
            double maximum = TransformX(band.MaximumX, settings);
            var span = target.Plot.Add.VerticalSpan(minimum, maximum);
            span.FillColor = WithOpacity(
                ScottPlot.Color.FromHex(band.ColorHex),
                band.Opacity);
            span.LegendText = band.Label;
            var left = target.Plot.Add.VerticalLine(minimum);
            var right = target.Plot.Add.VerticalLine(maximum);
            left.Color = right.Color =
                ScottPlot.Color.FromHex(band.ColorHex);
            left.LineWidth = right.LineWidth = 3;
        }

        foreach (PlotSeries series in model.Series)
        {
            double[] x = series.X
                .Select(value => TransformX(value, settings))
                .ToArray();
            double[] y = series.Y
                .Select(value => TransformY(value, settings))
                .ToArray();
            var scatter = target.Plot.Add.Scatter(x, y);
            scatter.Color = ScottPlot.Color.FromHex(series.ColorHex);
            scatter.LegendText = series.Label;
            if (series.Kind == PlotSeriesKind.Scatter)
            {
                scatter.LineWidth = 0;
                scatter.MarkerSize = 7;
            }
            else
            {
                scatter.LineWidth = 2;
                scatter.MarkerSize = 3;
            }
        }

        bool intervalLabelAdded = false;
        foreach (PlotIntervalMarker interval in model.Intervals)
        {
            double minimum = TransformX(interval.MinimumX, settings);
            double maximum = TransformX(interval.MaximumX, settings);
            double y = TransformY(interval.Y, settings);
            var line = target.Plot.Add.Scatter(
                new[] { minimum, maximum },
                new[] { y, y });
            line.Color = ScottPlot.Color.FromHex(interval.ColorHex);
            line.LineWidth = 5;
            line.MarkerSize = 0;
            if (!intervalLabelAdded)
            {
                line.LegendText = interval.Label;
                intervalLabelAdded = true;
            }

            var midpoint = target.Plot.Add.Scatter(
                new[] { (minimum + maximum) / 2 },
                new[] { y });
            midpoint.Color = line.Color;
            midpoint.LineWidth = 0;
            midpoint.MarkerSize = 9;
        }

        target.Plot.Title(model.Title);
        target.Plot.XLabel(settings?.LogarithmicX == true
            ? $"log10({model.XLabel})"
            : model.XLabel);
        target.Plot.YLabel(settings?.LogarithmicY == true
            ? $"log10({model.YLabel})"
            : model.YLabel);
        PlotTheme.Apply(target.Plot);
        if (model.Series.Any(series => !string.IsNullOrWhiteSpace(series.Label)) ||
            model.Bands.Any(band => !string.IsNullOrWhiteSpace(band.Label)))
        {
            target.Plot.ShowLegend();
        }

        ApplyLimits(target, viewport, settings);
        target.Refresh();
    }

    private static void ApplyLimits(
        WpfPlot target,
        PlotViewport viewport,
        PlotViewSettings? settings)
    {
        target.Plot.Axes.SetLimits(
            settings?.MinimumX.HasValue == true
                ? TransformX(settings.MinimumX.Value, settings)
                : TransformX(viewport.MinimumX, settings),
            settings?.MaximumX.HasValue == true
                ? TransformX(settings.MaximumX.Value, settings)
                : TransformX(viewport.MaximumX, settings),
            settings?.MinimumY.HasValue == true
                ? TransformY(settings.MinimumY.Value, settings)
                : TransformY(viewport.MinimumY, settings),
            settings?.MaximumY.HasValue == true
                ? TransformY(settings.MaximumY.Value, settings)
                : TransformY(viewport.MaximumY, settings));
    }

    private static double TransformX(
        double value,
        PlotViewSettings? settings) =>
        settings?.LogarithmicX == true ? Math.Log10(value) : value;

    private static double TransformY(
        double value,
        PlotViewSettings? settings) =>
        settings?.LogarithmicY == true ? Math.Log10(value) : value;

    private static ScottPlot.Color WithOpacity(
        ScottPlot.Color color,
        double opacity) =>
        color.WithAlpha((byte)Math.Round(opacity * byte.MaxValue));
}
