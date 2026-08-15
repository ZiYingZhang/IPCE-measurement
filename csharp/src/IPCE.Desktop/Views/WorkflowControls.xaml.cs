using System.Windows;
using System.Windows.Controls;
using IPCE.Desktop.Services;
using IPCE.Desktop.ViewModels;
using IPCE.IO.Export;

namespace IPCE.Desktop.Views;

public partial class WorkflowControls : UserControl
{
    private readonly IFileDialogService _dialogs;

    public WorkflowControls()
        : this(new FileDialogService())
    {
    }

    internal WorkflowControls(IFileDialogService dialogs)
    {
        _dialogs = dialogs;
        InitializeComponent();
    }

    private void BrowseCalibration_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            CalibrationPathBox,
            "选择标准硅校准文件",
            "Excel 工作簿|*.xlsx;*.xls|所有文件|*.*");

    private void BrowseSiliconTrace_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SiliconTracePathBox,
            "选择硅 i-t 文件",
            "文本数据|*.txt;*.csv|所有文件|*.*");

    private void BrowseSiliconAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SiliconAnchorPathBox,
            "选择硅时间锚点文件",
            "文本数据|*.txt;*.csv|所有文件|*.*");

    private void BrowseSampleTrace_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SampleTracePathBox,
            "选择样品 i-t 文件",
            "文本数据|*.txt;*.csv|所有文件|*.*");

    private void BrowseSampleAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SampleAnchorPathBox,
            "选择样品时间锚点文件",
            "文本数据|*.txt;*.csv|所有文件|*.*");

    private void BrowseExternalIpce_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            ExternalIpcePathBox,
            "选择外部 IPCE 文件",
            "IPCE 数据|*.txt;*.csv;*.xls;*.xlsx|所有文件|*.*");

    private void BrowseSpectrum_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SpectrumPathBox,
            "选择太阳光谱文件",
            "Excel 工作簿|*.xls;*.xlsx|所有文件|*.*");

    private void Export_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel main ||
            ExportFormatComboBox.SelectedValue is not ExportFormat format)
        {
            return;
        }

        (string filter, string extension) = format switch
        {
            ExportFormat.Xlsx =>
                ("Excel 工作簿|*.xlsx", ".xlsx"),
            ExportFormat.Csv =>
                ("CSV 文本|*.csv", ".csv"),
            ExportFormat.Mat =>
                ("MATLAB 数据|*.mat", ".mat"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format)),
        };
        string? path = _dialogs.SaveFile(
            "导出 IPCE 结果",
            filter,
            extension);
        if (path is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<string> written =
                main.Spectrum.ExportSelected(path, format);
            MessageBox.Show(
                string.Join(
                    Environment.NewLine,
                    written.Select(System.IO.Path.GetFullPath)),
                "导出完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "导出失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Browse(
        TextBox target,
        string title,
        string filter)
    {
        string? path = _dialogs.OpenFile(title, filter);
        if (path is not null)
        {
            target.Text = path;
        }
    }
}
