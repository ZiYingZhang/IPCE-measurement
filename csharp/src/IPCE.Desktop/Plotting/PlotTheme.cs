namespace IPCE.Desktop.Plotting;

public static class PlotTheme
{
    public const string PreferredChineseFont = "Microsoft YaHei UI";
    public const float TitleFontSize = 26;
    public const float AxisLabelFontSize = 24;
    public const float TickFontSize = 20;
    public const float LegendFontSize = 20;
    public const double HoverFontSize = 14;
    public const double ToolbarFontSize = 14;

    public static void Apply(ScottPlot.Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);

        ApplyLabels(plot);
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#F7F9FC");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        plot.Axes.DefaultGrid.MajorLineColor =
            ScottPlot.Color.FromHex("#D9E1EA");
    }

    public static void ApplyLabels(ScottPlot.Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);

        plot.Axes.Title.Label.FontName = PreferredChineseFont;
        plot.Axes.Bottom.Label.FontName = PreferredChineseFont;
        plot.Axes.Left.Label.FontName = PreferredChineseFont;
        plot.Axes.Top.Label.FontName = PreferredChineseFont;
        plot.Axes.Right.Label.FontName = PreferredChineseFont;
        plot.Axes.Bottom.TickLabelStyle.FontName = PreferredChineseFont;
        plot.Axes.Left.TickLabelStyle.FontName = PreferredChineseFont;
        plot.Axes.Top.TickLabelStyle.FontName = PreferredChineseFont;
        plot.Axes.Right.TickLabelStyle.FontName = PreferredChineseFont;
        plot.Legend.FontName = PreferredChineseFont;

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
}
