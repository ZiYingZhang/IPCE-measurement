using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScottPlot;
using ScottPlot.WPF;

namespace IPCE.Desktop.Plotting;

public sealed class PlotInteractionController
{
    private const double HitRadiusPixels = 12;
    private readonly WpfPlot _plot;
    private readonly TextBlock _hoverText;
    private readonly TextBlock _clippedText;
    private readonly List<IPlottable> _hoverPlottables = [];
    private PlotViewSettings? _settings;

    public PlotInteractionController(
        WpfPlot plot,
        TextBlock hoverText,
        TextBlock clippedText)
    {
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
        _hoverText =
            hoverText ?? throw new ArgumentNullException(nameof(hoverText));
        _clippedText =
            clippedText ?? throw new ArgumentNullException(nameof(clippedText));
    }

    public PlotModel? Model { get; private set; }

    public PlotViewportMode ViewportMode { get; private set; } =
        PlotViewportMode.Robust;

    public void Render(PlotModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        ViewportMode = PlotViewportMode.Robust;
        _settings = null;
        RenderCurrent();
    }

    public void Apply(PlotViewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Model is null)
        {
            return;
        }

        settings.Validate(Model);
        PlotViewSettings? previous = _settings;
        try
        {
            _settings = settings;
            RenderCurrent();
        }
        catch
        {
            _settings = previous;
            RenderCurrent();
            throw;
        }
    }

    public void Reset()
    {
        if (Model is null)
        {
            return;
        }

        ViewportMode = PlotViewportMode.Robust;
        _settings = null;
        RenderCurrent();
    }

    public void ShowAll()
    {
        if (Model is null)
        {
            return;
        }

        ViewportMode = PlotViewportMode.Full;
        _settings = null;
        RenderCurrent();
    }

    public void HandleMouseMove(MouseEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (Model is null)
        {
            ClearHover();
            return;
        }

        Pixel pointer = _plot.GetPlotPixelPosition(eventArgs);
        PlotPixelPoint pointerPoint =
            new(pointer.X, pointer.Y);
        PlotHoverPoint? rawHit = PlotHitTester.FindNearest(
            Model,
            pointerPoint,
            ToPixel,
            HitRadiusPixels);
        IntervalHit? intervalHit = FindNearestInterval(pointerPoint);

        if (intervalHit is not null &&
            (rawHit is null ||
             intervalHit.PixelDistance < rawHit.PixelDistance))
        {
            ShowHover(
                intervalHit.X,
                intervalHit.Y,
                intervalHit.Details);
        }
        else if (rawHit is not null)
        {
            ShowHover(rawHit.X, rawHit.Y, rawHit.Details);
        }
        else
        {
            ClearHover();
        }
    }

    public void ClearHover()
    {
        RemoveHoverPlottables();
        _hoverText.Text = "";
        _hoverText.Visibility = Visibility.Collapsed;
        _plot.Refresh();
    }

    private void RenderCurrent()
    {
        if (Model is null)
        {
            return;
        }

        _hoverPlottables.Clear();
        _hoverText.Text = "";
        _hoverText.Visibility = Visibility.Collapsed;
        PlotViewport viewport = PlotViewportCalculator.Calculate(
            Model,
            Model.ViewportPolicy,
            ViewportMode);
        PlotModelRenderer.Render(
            _plot,
            Model,
            viewport,
            _settings);
        if (ViewportMode == PlotViewportMode.Robust &&
            viewport.ClippedYPointCount > 0)
        {
            _clippedText.Text =
                $"默认显示主体范围；视野外 {viewport.ClippedYPointCount} 个极端点。可点“显示全部”。";
            _clippedText.Visibility = Visibility.Visible;
        }
        else
        {
            _clippedText.Text = "";
            _clippedText.Visibility = Visibility.Collapsed;
        }
    }

    private PlotPixelPoint ToPixel(double x, double y)
    {
        Pixel pixel = _plot.Plot.GetPixel(
            new Coordinates(TransformX(x), TransformY(y)),
            _plot.Plot.Axes.Bottom,
            _plot.Plot.Axes.Left);
        return new PlotPixelPoint(pixel.X, pixel.Y);
    }

    private IntervalHit? FindNearestInterval(PlotPixelPoint pointer)
    {
        if (Model is null)
        {
            return null;
        }

        IntervalHit? nearest = null;
        foreach (PlotIntervalMarker interval in Model.Intervals)
        {
            PlotPixelPoint start = ToPixel(interval.MinimumX, interval.Y);
            PlotPixelPoint end = ToPixel(interval.MaximumX, interval.Y);
            if (!double.IsFinite(start.X) ||
                !double.IsFinite(start.Y) ||
                !double.IsFinite(end.X) ||
                !double.IsFinite(end.Y))
            {
                continue;
            }

            double minimumPixelX = Math.Min(start.X, end.X);
            double maximumPixelX = Math.Max(start.X, end.X);
            double projectedX = Math.Clamp(
                pointer.X,
                minimumPixelX,
                maximumPixelX);
            double deltaX = pointer.X - projectedX;
            double deltaY = pointer.Y - start.Y;
            double distance = Math.Sqrt(
                deltaX * deltaX +
                deltaY * deltaY);
            if (distance > HitRadiusPixels ||
                (nearest is not null &&
                 distance >= nearest.PixelDistance))
            {
                continue;
            }

            double ratio = maximumPixelX == minimumPixelX
                ? 0.5
                : (projectedX - start.X) / (end.X - start.X);
            double x = interval.MinimumX +
                ratio * (interval.MaximumX - interval.MinimumX);
            nearest = new IntervalHit(
                x,
                interval.Y,
                distance,
                interval.HoverDetails);
        }

        return nearest;
    }

    private void ShowHover(double x, double y, string details)
    {
        RemoveHoverPlottables();
        double plotX = TransformX(x);
        double plotY = TransformY(y);
        ScottPlot.Color color = ScottPlot.Color.FromHex("#C62828");

        var marker = _plot.Plot.Add.Scatter(
            new[] { plotX },
            new[] { plotY });
        marker.Color = color;
        marker.LineWidth = 0;
        marker.MarkerSize = 11;
        var vertical = _plot.Plot.Add.VerticalLine(plotX);
        vertical.Color = color;
        vertical.LineWidth = 1;
        var horizontal = _plot.Plot.Add.HorizontalLine(plotY);
        horizontal.Color = color;
        horizontal.LineWidth = 1;
        _hoverPlottables.Add(marker);
        _hoverPlottables.Add(vertical);
        _hoverPlottables.Add(horizontal);

        _hoverText.Text = details;
        _hoverText.Visibility = Visibility.Visible;
        _plot.Refresh();
    }

    private void RemoveHoverPlottables()
    {
        foreach (IPlottable plottable in _hoverPlottables)
        {
            _plot.Plot.Remove(plottable);
        }

        _hoverPlottables.Clear();
    }

    private double TransformX(double value) =>
        _settings?.LogarithmicX == true
            ? Math.Log10(value)
            : value;

    private double TransformY(double value) =>
        _settings?.LogarithmicY == true
            ? Math.Log10(value)
            : value;

    private sealed record IntervalHit(
        double X,
        double Y,
        double PixelDistance,
        string Details);
}
