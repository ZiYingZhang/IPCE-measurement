using System.Windows;
using System.Windows.Controls;
using IPCE.Desktop.Localization;
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
            Localization["Dialog.SelectCalibration"],
            Localization["FileFilter.ExcelAll"]);

    private void BrowseSiliconTrace_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SiliconTracePathBox,
            Localization["Dialog.SelectSiliconTrace"],
            Localization["FileFilter.TextAll"]);

    private void BrowseSiliconAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SiliconAnchorPathBox,
            Localization["Dialog.SelectSiliconAnchors"],
            Localization["FileFilter.TextAll"]);

    private void BrowseSampleTrace_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SampleTracePathBox,
            Localization["Dialog.SelectSampleTrace"],
            Localization["FileFilter.TextAll"]);

    private void BrowseSampleAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SampleAnchorPathBox,
            Localization["Dialog.SelectSampleAnchors"],
            Localization["FileFilter.TextAll"]);

    private void BrowseExternalIpce_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            ExternalIpcePathBox,
            Localization["Dialog.SelectExternalIpce"],
            Localization["FileFilter.IpceAll"]);

    private void BrowseSpectrum_Click(
        object sender,
        RoutedEventArgs eventArgs) =>
        Browse(
            SpectrumPathBox,
            Localization["Dialog.SelectSpectrum"],
            Localization["FileFilter.SpectrumAll"]);

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
                (Localization["FileFilter.ExportXlsx"], ".xlsx"),
            ExportFormat.Csv =>
                (Localization["FileFilter.ExportCsv"], ".csv"),
            ExportFormat.Mat =>
                (Localization["FileFilter.ExportMat"], ".mat"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format)),
        };
        string? path = _dialogs.SaveFile(
            Localization["Dialog.ExportResults"],
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
                Localization["Dialog.ExportCompleted"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                new UserMessageLocalizer(Localization)
                    .Localize(exception),
                Localization["Dialog.ExportFailed"],
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

    private ILocalizationService Localization =>
        (DataContext as MainViewModel)?.Localization ??
        LocalizationService.Current;
}
