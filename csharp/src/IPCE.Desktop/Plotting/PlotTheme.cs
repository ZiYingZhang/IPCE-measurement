namespace IPCE.Desktop.Plotting;

public static class PlotTheme
{
    public const string PreferredEnglishFont = "Arial";
    public const string PreferredChineseFont = "Microsoft YaHei";
    public const float TitleFontSize = 28;
    public const float AxisLabelFontSize = 30;
    public const float TickFontSize = 24;
    public const float LegendFontSize = 24;
    public const double HoverFontSize = 14;
    public const double ToolbarFontSize = 14;
    public const float SeriesLineWidth = 3;
    public const float RangeBoundaryLineWidth = 3;
    public const float LegendSymbolWidth = 24;
    public const float LegendSymbolHeight = 12;
    public const string RangeFillColorHex = "#9E9E9E";
    public const double RangeFillOpacity = 0.14;

    public static void Apply(
        ScottPlot.Plot plot,
        string? fontName = null)
    {
        ArgumentNullException.ThrowIfNull(plot);

        ApplyLabels(plot, fontName);
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#F7F9FC");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        plot.Axes.DefaultGrid.MajorLineColor =
            ScottPlot.Color.FromHex("#D9E1EA");
        plot.Legend.OutlineWidth = 0;
        plot.Legend.SymbolWidth = LegendSymbolWidth;
        plot.Legend.SymbolHeight = LegendSymbolHeight;
    }

    public static void ApplyLabels(
        ScottPlot.Plot plot,
        string? fontName = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        string resolvedFont = string.IsNullOrWhiteSpace(fontName)
            ? PreferredChineseFont
            : fontName;

        plot.Axes.Title.Label.FontName = resolvedFont;
        plot.Axes.Bottom.Label.FontName = resolvedFont;
        plot.Axes.Left.Label.FontName = resolvedFont;
        plot.Axes.Top.Label.FontName = resolvedFont;
        plot.Axes.Right.Label.FontName = resolvedFont;
        plot.Axes.Bottom.TickLabelStyle.FontName = resolvedFont;
        plot.Axes.Left.TickLabelStyle.FontName = resolvedFont;
        plot.Axes.Top.TickLabelStyle.FontName = resolvedFont;
        plot.Axes.Right.TickLabelStyle.FontName = resolvedFont;
        plot.Legend.FontName = resolvedFont;

        plot.Axes.Title.Label.FontSize = TitleFontSize;
        plot.Axes.Bottom.Label.FontSize = AxisLabelFontSize;
        plot.Axes.Left.Label.FontSize = AxisLabelFontSize;
        plot.Axes.Top.Label.FontSize = AxisLabelFontSize;
        plot.Axes.Right.Label.FontSize = AxisLabelFontSize;
        plot.Axes.Bottom.TickLabelStyle.FontSize = TickFontSize;
        plot.Axes.Left.TickLabelStyle.FontSize = TickFontSize;
        plot.Axes.Top.TickLabelStyle.FontSize = TickFontSize;
        plot.Axes.Right.TickLabelStyle.FontSize = TickFontSize;
        plot.Legend.FontSize = LegendFontSize;
    }

    public static string FontFor(PlotModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        IEnumerable<string> visibleText = new[]
        {
            model.Title,
            model.XLabel,
            model.YLabel,
            model.EmptyMessage,
        }
        .Concat(model.Series.Select(series => series.Label))
        .Concat(model.Bands.Select(band => band.Label))
        .Concat(model.Intervals.Select(interval => interval.Label));
        return visibleText.Any(ContainsChineseText)
            ? PreferredChineseFont
            : PreferredEnglishFont;
    }

    private static bool ContainsChineseText(string value) =>
        value.Any(character =>
            (character >= '\u3400' && character <= '\u4DBF') ||
            (character >= '\u4E00' && character <= '\u9FFF') ||
            (character >= '\uF900' && character <= '\uFAFF'));
}
