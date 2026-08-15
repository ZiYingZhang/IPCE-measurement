using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Views.Plots;

public partial class IpcePlotView : UserControl
{
    private readonly PlotInteractionController _controller;

    public IpcePlotView()
    {
        InitializeComponent();
        _controller = new PlotInteractionController(
            PlotSurface,
            HoverText,
            ClippedText);
        Toolbar.ApplyRequested += (_, settings) => Apply(settings);
        Toolbar.ResetRequested += (_, _) => _controller.Reset();
        Toolbar.ShowAllRequested += (_, _) => _controller.ShowAll();
        Toolbar.SaveImageRequested += (_, _) =>
            PlotViewSaveHelper.Save(PlotSurface, this);
    }

    public ScottPlot.WPF.WpfPlot PlotControl => PlotSurface;

    public PlotInteractionController InteractionController => _controller;

    public void Render(PlotModel model)
    {
        EmptyMessage.Text = model.Series.Count == 0 ? model.EmptyMessage : "";
        _controller.Render(model);
    }

    public void SetSelectedSource(string source) =>
        SourceBadge.Text = $"积分来源：{source}";

    private void Apply(PlotViewSettings settings)
    {
        try
        {
            _controller.Apply(settings);
        }
        catch (IpceException exception)
        {
            MessageBox.Show(Window.GetWindow(this), exception.Message,
                "坐标轴设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PlotSurface_MouseMove(
        object sender,
        MouseEventArgs eventArgs) =>
        _controller.HandleMouseMove(eventArgs);

    private void PlotSurface_MouseLeave(
        object sender,
        MouseEventArgs eventArgs) =>
        _controller.ClearHover();
}
