using IPCE.Core.Errors;

namespace IPCE.Desktop.Plotting;

public enum PlotSeriesKind
{
    Line,
    Scatter,
}

public sealed record PlotSeries
{
    public PlotSeries(
        string label,
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        PlotSeriesKind kind,
        string colorHex,
        IReadOnlyList<double>? yErrors = null,
        bool contributesToAutoRange = true,
        string id = "")
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        double[] copiedX = x.ToArray();
        double[] copiedY = y.ToArray();
        double[]? copiedErrors = yErrors?.ToArray();
        if (copiedX.Length != copiedY.Length ||
            copiedX.Any(value => !double.IsFinite(value)) ||
            copiedY.Any(value => !double.IsFinite(value)) ||
            (copiedErrors is not null &&
             (copiedErrors.Length != copiedY.Length ||
              copiedErrors.Any(value =>
                  !double.IsFinite(value) || value < 0))))
        {
            throw InvalidPlotSeries();
        }

        Label = label ?? "";
        X = Array.AsReadOnly(copiedX);
        Y = Array.AsReadOnly(copiedY);
        Kind = kind;
        ColorHex = colorHex ?? "";
        YErrors = copiedErrors is null
            ? null
            : Array.AsReadOnly(copiedErrors);
        ContributesToAutoRange = contributesToAutoRange;
        Id = id ?? "";
    }

    public string Label { get; }

    public IReadOnlyList<double> X { get; }

    public IReadOnlyList<double> Y { get; }

    public PlotSeriesKind Kind { get; }

    public string ColorHex { get; }

    public IReadOnlyList<double>? YErrors { get; }

    public bool ContributesToAutoRange { get; }

    public string Id { get; }

    private static IpceException InvalidPlotSeries()
    {
        return new IpceException(
            "IPCE:InvalidPlotSeries",
            "绘图序列的横纵坐标和误差数据必须等长，且只能包含有效数值。");
    }
}

public sealed record PlotBand
{
    public PlotBand(
        double minimumX,
        double maximumX,
        string label,
        string colorHex,
        double opacity)
    {
        if (!double.IsFinite(minimumX) ||
            !double.IsFinite(maximumX) ||
            maximumX <= minimumX ||
            !double.IsFinite(opacity) ||
            opacity < 0 ||
            opacity > 1)
        {
            throw new IpceException(
                "IPCE:InvalidPlotSeries",
                "绘图区域的上下限必须为有效数值且上限大于下限，透明度必须在 0 到 1 之间。");
        }

        MinimumX = minimumX;
        MaximumX = maximumX;
        Label = label ?? "";
        ColorHex = colorHex ?? "";
        Opacity = opacity;
    }

    public double MinimumX { get; }

    public double MaximumX { get; }

    public string Label { get; }

    public string ColorHex { get; }

    public double Opacity { get; }
}

public sealed record PlotIntervalMarker
{
    public PlotIntervalMarker(
        double minimumX,
        double maximumX,
        double y,
        string label,
        string colorHex,
        string hoverDetails)
    {
        if (!double.IsFinite(minimumX) ||
            !double.IsFinite(maximumX) ||
            maximumX <= minimumX ||
            !double.IsFinite(y) ||
            string.IsNullOrWhiteSpace(colorHex))
        {
            throw new IpceException(
                "IPCE:InvalidTraceOverlay",
                "平均电流覆盖层必须包含有效区间、电流和颜色。");
        }

        MinimumX = minimumX;
        MaximumX = maximumX;
        Y = y;
        Label = label ?? "";
        ColorHex = colorHex;
        HoverDetails = hoverDetails ?? "";
    }

    public double MinimumX { get; }

    public double MaximumX { get; }

    public double Y { get; }

    public string Label { get; }

    public string ColorHex { get; }

    public string HoverDetails { get; }
}

public sealed record PlotModel
{
    public PlotModel(
        string title,
        string xLabel,
        string yLabel,
        IReadOnlyList<PlotSeries> series,
        IReadOnlyList<PlotBand> bands,
        string emptyMessage,
        IReadOnlyList<PlotIntervalMarker>? intervals = null,
        PlotViewportPolicy? viewportPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(bands);

        Title = title ?? "";
        XLabel = xLabel ?? "";
        YLabel = yLabel ?? "";
        Series = Array.AsReadOnly(series.ToArray());
        Bands = Array.AsReadOnly(bands.ToArray());
        EmptyMessage = emptyMessage ?? "";
        Intervals = Array.AsReadOnly(
            intervals?.ToArray() ?? []);
        ViewportPolicy = viewportPolicy ?? new PlotViewportPolicy();
    }

    public string Title { get; }

    public string XLabel { get; }

    public string YLabel { get; }

    public IReadOnlyList<PlotSeries> Series { get; }

    public IReadOnlyList<PlotBand> Bands { get; }

    public string EmptyMessage { get; }

    public IReadOnlyList<PlotIntervalMarker> Intervals { get; }

    public PlotViewportPolicy ViewportPolicy { get; }
}
