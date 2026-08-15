using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPCE.Core.Errors;
using IPCE.Core.Domain;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Views.Plots;

public partial class SpectrumIntegrationPlotView : UserControl
{
    private readonly PlotInteractionController _irradianceController;
    private readonly PlotInteractionController _ipceController;
    private readonly PlotInteractionController _cumulativeController;

    public SpectrumIntegrationPlotView()
    {
        InitializeComponent();
        _irradianceController = new PlotInteractionController(
            IrradiancePlot,
            IrradianceHoverText,
            IrradianceClippedText,
            () => Localization);
        _ipceController = new PlotInteractionController(
            SelectedIpcePlot,
            SelectedIpceHoverText,
            SelectedIpceClippedText,
            () => Localization);
        _cumulativeController = new PlotInteractionController(
            CumulativePlot,
            CumulativeHoverText,
            CumulativeClippedText,
            () => Localization);
        WireToolbar(
            IrradianceToolbar,
            _irradianceController,
            IrradiancePlot);
        WireToolbar(
            SelectedIpceToolbar,
            _ipceController,
            SelectedIpcePlot);
        WireToolbar(
            CumulativeToolbar,
            _cumulativeController,
            CumulativePlot);
    }

    public ScottPlot.WPF.WpfPlot IrradiancePlotControl => IrradiancePlot;

    public ScottPlot.WPF.WpfPlot IpcePlotControl => SelectedIpcePlot;

    public ScottPlot.WPF.WpfPlot CumulativePlotControl => CumulativePlot;

    public void Render(
        PlotModel irradiance,
        PlotModel selectedIpce,
        PlotModel cumulative,
        IntegrationSummary? summary)
    {
        _irradianceController.Render(irradiance);
        _ipceController.Render(selectedIpce);
        _cumulativeController.Render(cumulative);
        SummaryText.Text = summary is null
            ? Localization["Summary.NotCalculated"]
            : Localization.Format(
                "Summary.CurrentDensityValue",
                summary.IntegratedCurrentDensityMilliamperePerSquareCentimetre);
    }

    private void WireToolbar(
        PlotToolbar toolbar,
        PlotInteractionController controller,
        ScottPlot.WPF.WpfPlot plot)
    {
        toolbar.ApplyRequested += (_, settings) =>
            Apply(controller, settings);
        toolbar.ResetRequested += (_, _) => controller.Reset();
        toolbar.ShowAllRequested += (_, _) => controller.ShowAll();
        toolbar.SaveImageRequested += (_, _) =>
            PlotViewSaveHelper.Save(plot, this);
    }

    private void Apply(
        PlotInteractionController controller,
        PlotViewSettings settings)
    {
        try
        {
            controller.Apply(settings);
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

    private void Plot_MouseMove(
        object sender,
        MouseEventArgs eventArgs) =>
        ControllerFor(sender).HandleMouseMove(eventArgs);

    private void Plot_MouseLeave(
        object sender,
        MouseEventArgs eventArgs) =>
        ControllerFor(sender).ClearHover();

    private PlotInteractionController ControllerFor(object sender)
    {
        if (ReferenceEquals(sender, IrradiancePlot))
        {
            return _irradianceController;
        }

        if (ReferenceEquals(sender, SelectedIpcePlot))
        {
            return _ipceController;
        }

        return _cumulativeController;
    }

    private ILocalizationService Localization =>
        (DataContext as MainViewModel)?.Localization ??
        LocalizationService.Current;
}
