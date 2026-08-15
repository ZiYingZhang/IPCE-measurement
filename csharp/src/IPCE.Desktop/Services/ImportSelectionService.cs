using System.Windows;
using System.Windows.Controls;
using IPCE.Desktop.Import;
using IPCE.Desktop.Localization;
using IPCE.IO.Import;

namespace IPCE.Desktop.Services;

public interface IImportSelectionService
{
    UnitOverrides? SelectTraceUnits(
        TraceImportInspection inspection);

    SpectrumImportSelection? SelectSpectrum(
        IReadOnlyList<string> sheets,
        Func<string, IReadOnlyList<SpectrumColumn>> discoverColumns,
        SpectrumImportSelection? suggested) => null;
}

public sealed class ImportSelectionService : IImportSelectionService
{
    private static readonly string[] TimeUnits =
        ["s", "ms", "min", "h"];
    private static readonly string[] CurrentUnits =
        ["A", "mA", "uA", "nA", "pA"];
    private readonly ILocalizationService _localization;

    public ImportSelectionService(
        ILocalizationService? localization = null) =>
        _localization = localization ?? LocalizationService.Current;

    public UnitOverrides? SelectTraceUnits(
        TraceImportInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var timeUnits = new ComboBox
        {
            ItemsSource = TimeUnits,
            SelectedItem = Suggested(
                inspection.DetectedTimeUnit,
                TimeUnits,
                "s"),
            Margin = new Thickness(4),
        };
        var currentUnits = new ComboBox
        {
            ItemsSource = CurrentUnits,
            SelectedItem = Suggested(
                inspection.DetectedCurrentUnit,
                CurrentUnits,
                "A"),
            Margin = new Thickness(4),
        };
        var confirm = new Button
        {
            Content = _localization["Common.Confirm"],
            IsDefault = true,
            MinWidth = 80,
        };
        var cancel = new Button
        {
            Content = _localization["Common.Cancel"],
            IsCancel = true,
            MinWidth = 80,
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);

        var content = new StackPanel
        {
            Margin = new Thickness(14),
        };
        content.Children.Add(new TextBlock
        {
            Text = _localization["Import.UnitsRequired"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        content.Children.Add(new TextBlock
        {
            Text = _localization.Format(
                "Import.TimeColumn",
                inspection.TimeHeader),
        });
        content.Children.Add(timeUnits);
        content.Children.Add(new TextBlock
        {
            Text = _localization.Format(
                "Import.CurrentColumn",
                inspection.CurrentHeader),
        });
        content.Children.Add(currentUnits);
        content.Children.Add(buttons);

        var dialog = new Window
        {
            Title = _localization["Import.SelectTraceUnits"],
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 390,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = Application.Current?.MainWindow,
        };
        confirm.Click += (_, _) => dialog.DialogResult = true;

        bool? accepted = dialog.ShowDialog();
        if (accepted != true)
        {
            return null;
        }

        return new UnitOverrides(
            (string)timeUnits.SelectedItem,
            (string)currentUnits.SelectedItem);
    }

    public SpectrumImportSelection? SelectSpectrum(
        IReadOnlyList<string> sheets,
        Func<string, IReadOnlyList<SpectrumColumn>> discoverColumns,
        SpectrumImportSelection? suggested)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(discoverColumns);
        var sheetBox = new ComboBox
        {
            ItemsSource = sheets,
            SelectedItem = suggested?.SheetName ?? sheets.FirstOrDefault(),
            Margin = new Thickness(4),
        };
        var wavelengthBox = NewColumnBox();
        var irradianceBox = NewColumnBox();
        IReadOnlyList<SpectrumColumn> columns = [];

        void LoadColumns()
        {
            if (sheetBox.SelectedItem is not string sheet)
            {
                columns = [];
                return;
            }

            columns = discoverColumns(sheet);
            ColumnOption[] options = columns
                .Select(column => new ColumnOption(
                    column,
                    _localization.Format(
                        "Import.NumericValues",
                        column.DisplayName,
                        column.NumericValueCount)))
                .ToArray();
            wavelengthBox.ItemsSource = options;
            irradianceBox.ItemsSource = options;
            int wavelengthIndex = suggested?.SheetName == sheet
                ? Array.FindIndex(
                    options,
                    option => option.Column.ColumnIndex ==
                        suggested.WavelengthColumn)
                : 0;
            int irradianceIndex = suggested?.SheetName == sheet
                ? Array.FindIndex(
                    options,
                    option => option.Column.ColumnIndex ==
                        suggested.IrradianceColumn)
                : Math.Min(1, options.Length - 1);
            wavelengthBox.SelectedIndex =
                wavelengthIndex >= 0 ? wavelengthIndex : 0;
            irradianceBox.SelectedIndex =
                irradianceIndex >= 0
                    ? irradianceIndex
                    : Math.Min(1, options.Length - 1);
        }

        sheetBox.SelectionChanged += (_, _) => LoadColumns();
        LoadColumns();
        var confirm = new Button
        {
            Content = _localization["Common.Confirm"],
            IsDefault = true,
            MinWidth = 80,
        };
        var cancel = new Button
        {
            Content = _localization["Common.Cancel"],
            IsCancel = true,
            MinWidth = 80,
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        var content = new StackPanel
        {
            Margin = new Thickness(14),
        };
        content.Children.Add(new TextBlock
        {
            Text = _localization["Import.Worksheet"],
        });
        content.Children.Add(sheetBox);
        content.Children.Add(new TextBlock
        {
            Text = _localization["Import.WavelengthColumn"],
        });
        content.Children.Add(wavelengthBox);
        content.Children.Add(new TextBlock
        {
            Text = _localization["Import.IrradianceColumn"],
        });
        content.Children.Add(irradianceBox);
        content.Children.Add(buttons);
        var dialog = new Window
        {
            Title = _localization["Import.SelectSpectrumColumns"],
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 480,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = Application.Current?.MainWindow,
        };
        confirm.Click += (_, _) =>
        {
            if (wavelengthBox.SelectedItem is not ColumnOption wavelength ||
                irradianceBox.SelectedItem is not ColumnOption irradiance)
            {
                MessageBox.Show(
                    dialog,
                    _localization["Import.SelectTwoNumericColumns"],
                    dialog.Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (wavelength.Column.ColumnIndex ==
                irradiance.Column.ColumnIndex)
            {
                MessageBox.Show(
                    dialog,
                    _localization["Import.ColumnsMustDiffer"],
                    dialog.Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true ||
            sheetBox.SelectedItem is not string selectedSheet ||
            wavelengthBox.SelectedItem is not ColumnOption selectedWavelength ||
            irradianceBox.SelectedItem is not ColumnOption selectedIrradiance)
        {
            return null;
        }

        return new SpectrumImportSelection(
            selectedSheet,
            selectedWavelength.Column.ColumnIndex,
            selectedIrradiance.Column.ColumnIndex);
    }

    private static ComboBox NewColumnBox() =>
        new()
        {
            DisplayMemberPath = nameof(ColumnOption.Label),
            Margin = new Thickness(4),
        };

    private static string Suggested(
        string detected,
        IReadOnlyList<string> supported,
        string fallback) =>
        supported.FirstOrDefault(unit =>
            string.Equals(
                unit,
                detected,
                StringComparison.OrdinalIgnoreCase)) ??
        fallback;

    private sealed record ColumnOption(
        SpectrumColumn Column,
        string Label);
}
