using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Views.Plots;

public partial class SchedulePlotView : UserControl
{
    private readonly PlotInteractionController _controller;

    public SchedulePlotView()
    {
        InitializeComponent();
        _controller = new PlotInteractionController(
            PlotSurface,
            HoverText,
            ClippedText,
            () => Localization);
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
        EmptyMessage.Text = model.Series.Count == 0
            ? model.EmptyMessage
            : "";
        _controller.Render(model);
    }

    private void Apply(PlotViewSettings settings)
    {
        try
        {
            _controller.Apply(settings);
        }
        catch (IpceException exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                new UserMessageLocalizer(Localization).Localize(exception),
                Localization["Dialog.AxisSettings"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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

    private ILocalizationService Localization =>
        (DataContext as MainViewModel)?.Localization ??
        LocalizationService.Current;
}
