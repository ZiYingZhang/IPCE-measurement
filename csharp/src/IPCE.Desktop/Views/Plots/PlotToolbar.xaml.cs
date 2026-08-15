using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using IPCE.Core.Errors;
using IPCE.Desktop.Plotting;

namespace IPCE.Desktop.Views.Plots;

public partial class PlotToolbar : UserControl
{
    public PlotToolbar()
    {
        InitializeComponent();
    }

    public event EventHandler<PlotViewSettings>? ApplyRequested;

    public event EventHandler? ResetRequested;

    public event EventHandler? ShowAllRequested;

    public event EventHandler? SaveImageRequested;

    private void Apply_Click(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            ApplyRequested?.Invoke(
                this,
                new PlotViewSettings(
                    ParseOptional(MinimumXBox.Text),
                    ParseOptional(MaximumXBox.Text),
                    ParseOptional(MinimumYBox.Text),
                    ParseOptional(MaximumYBox.Text),
                    LogarithmicXBox.IsChecked == true,
                    LogarithmicYBox.IsChecked == true));
        }
        catch (IpceException exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "坐标轴设置",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs eventArgs)
    {
        Clear();
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Save_Click(object sender, RoutedEventArgs eventArgs) =>
        SaveImageRequested?.Invoke(this, EventArgs.Empty);

    private void ShowAll_Click(object sender, RoutedEventArgs eventArgs)
    {
        Clear();
        ShowAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Clear()
    {
        MinimumXBox.Text = "";
        MaximumXBox.Text = "";
        MinimumYBox.Text = "";
        MaximumYBox.Text = "";
        LogarithmicXBox.IsChecked = false;
        LogarithmicYBox.IsChecked = false;
    }

    private static double? ParseOptional(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if ((double.TryParse(
                 text,
                 NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.CurrentCulture,
                 out double value) ||
             double.TryParse(
                 text,
                 NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.InvariantCulture,
                 out value)) &&
            double.IsFinite(value))
        {
            return value;
        }

        throw new IpceException(
            "IPCE:InvalidAxisLimits",
            $"无法识别坐标轴数值：{text}");
    }
}
