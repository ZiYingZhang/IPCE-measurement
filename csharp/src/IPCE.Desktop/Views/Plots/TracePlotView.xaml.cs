using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.ViewModels;
using Microsoft.Win32;

namespace IPCE.Desktop.Views.Plots;

public partial class TracePlotView : UserControl
{
    private PlotModel? _sourceModel;
    private readonly PlotInteractionController _controller;

    public TracePlotView()
    {
        InitializeComponent();
        _controller = new PlotInteractionController(
            PlotSurface,
            HoverText,
            ClippedText,
            () => Localization);
        Toolbar.ApplyRequested += ApplyRequested;
        Toolbar.ResetRequested += ResetRequested;
        Toolbar.ShowAllRequested += (_, _) => _controller.ShowAll();
        Toolbar.SaveImageRequested += SaveImageRequested;
    }

    public ScottPlot.WPF.WpfPlot PlotControl => PlotSurface;

    public PlotInteractionController InteractionController => _controller;

    public void Render(PlotModel model)
    {
        _sourceModel = model;
        EmptyMessage.Text = model.Series.Count == 0
            ? model.EmptyMessage
            : "";
        RenderFiltered();
    }

    private void ApplyRequested(
        object? sender,
        PlotViewSettings settings)
    {
        try
        {
            _controller.Apply(settings);
        }
        catch (IpceException exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                new UserMessageLocalizer(Localization)
                    .Localize(exception),
                Localization["Dialog.AxisSettings"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ResetRequested(object? sender, EventArgs eventArgs)
    {
        _controller.Reset();
    }

    private void SaveImageRequested(object? sender, EventArgs eventArgs)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Localization["FileFilter.Png"],
            DefaultExt = ".png",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            PlotSurface.Plot.SavePng(
                dialog.FileName,
                Math.Max(1, (int)PlotSurface.ActualWidth),
                Math.Max(1, (int)PlotSurface.ActualHeight));
        }
    }

    private void PlotSurface_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        _controller.Reset();
    }

    private void PlotSurface_MouseMove(
        object sender,
        MouseEventArgs eventArgs)
    {
        _controller.HandleMouseMove(eventArgs);
    }

    private void PlotSurface_MouseLeave(
        object sender,
        MouseEventArgs eventArgs) =>
        _controller.ClearHover();

    private void LayerBox_Changed(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (_controller is not null && _sourceModel is not null)
        {
            RenderFiltered();
        }
    }

    private void RenderFiltered()
    {
        if (_sourceModel is null)
        {
            return;
        }

        IReadOnlyList<PlotSeries> series = _sourceModel.Series
            .Where(series =>
                (RawTraceLayerBox.IsChecked == true &&
                 series.Id == "raw-trace") ||
                (DiagnosticLayerBox.IsChecked == true &&
                 series.Id != "raw-trace"))
            .ToArray();
        IReadOnlyList<PlotBand> bands =
            DarkLayerBox.IsChecked == true
                ? _sourceModel.Bands
                : [];
        IReadOnlyList<PlotIntervalMarker> intervals =
            DiagnosticLayerBox.IsChecked == true
                ? _sourceModel.Intervals
                : [];
        _controller.Render(new PlotModel(
            _sourceModel.Title,
            _sourceModel.XLabel,
            _sourceModel.YLabel,
            series,
            bands,
            _sourceModel.EmptyMessage,
            intervals,
            _sourceModel.ViewportPolicy));
    }

    private ILocalizationService Localization =>
        (DataContext as MainViewModel)?.Localization ??
        LocalizationService.Current;
}
