using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Services;
using IPCE.IO.Import;

namespace IPCE.Desktop.Import;

public sealed record SpectrumImportSelection(
    string SheetName,
    int WavelengthColumn,
    int IrradianceColumn);

public sealed record SpectrumImportResult(
    IReadOnlyList<SpectrumPoint> Points,
    SpectrumImportSelection Selection,
    string WavelengthHeader,
    string IrradianceHeader);

public sealed class SpectrumImportCoordinator
{
    private readonly IImportSelectionService _selections;

    public SpectrumImportCoordinator(
        IImportSelectionService selections)
    {
        _selections = selections ??
            throw new ArgumentNullException(nameof(selections));
    }

    public async Task<SpectrumImportResult?> ReadAsync(string path)
    {
        IReadOnlyList<string> sheets = await Task.Run(
            () => SpectrumReader.DiscoverSheets(path));
        SpectrumImportSelection? suggested =
            BuildSuggestion(path, sheets);
        SpectrumImportSelection? selection =
            _selections.SelectSpectrum(
                sheets,
                sheet => SpectrumReader.DiscoverColumns(path, sheet),
                suggested);
        if (selection is null)
        {
            return null;
        }

        if (selection.WavelengthColumn ==
                selection.IrradianceColumn)
        {
            throw new IpceException(
                "IPCE:InvalidSpectrumSelection",
                "波长列和辐照度列不能相同。");
        }

        IReadOnlyList<SpectrumColumn> columns =
            SpectrumReader.DiscoverColumns(
                path,
                selection.SheetName);
        SpectrumColumn? wavelength = columns.FirstOrDefault(
            column =>
                column.ColumnIndex == selection.WavelengthColumn);
        SpectrumColumn? irradiance = columns.FirstOrDefault(
            column =>
                column.ColumnIndex == selection.IrradianceColumn);
        if (wavelength is null || irradiance is null)
        {
            throw new IpceException(
                "IPCE:InvalidSpectrumSelection",
                "所选光谱列不存在或数值不足。");
        }

        IReadOnlyList<SpectrumPoint> points = await Task.Run(
            () => SpectrumReader.Read(
                path,
                selection.SheetName,
                selection.WavelengthColumn,
                selection.IrradianceColumn));
        return new SpectrumImportResult(
            points,
            selection,
            wavelength.Header,
            irradiance.Header);
    }

    private static SpectrumImportSelection? BuildSuggestion(
        string path,
        IReadOnlyList<string> sheets)
    {
        if (sheets.Count == 0)
        {
            return null;
        }

        string sheet = sheets.FirstOrDefault(name =>
            string.Equals(
                name,
                "Spectra",
                StringComparison.OrdinalIgnoreCase)) ?? sheets[0];
        try
        {
            IReadOnlyList<SpectrumColumn> columns =
                SpectrumReader.DiscoverColumns(path, sheet);
            if (string.Equals(
                    sheet,
                    "Spectra",
                    StringComparison.OrdinalIgnoreCase) &&
                columns.Any(column => column.ColumnIndex == 1) &&
                columns.Any(column => column.ColumnIndex == 3))
            {
                return new SpectrumImportSelection(sheet, 1, 3);
            }

            return columns.Count >= 2
                ? new SpectrumImportSelection(
                    sheet,
                    columns[0].ColumnIndex,
                    columns[1].ColumnIndex)
                : null;
        }
        catch (IpceException)
        {
            return null;
        }
    }
}
