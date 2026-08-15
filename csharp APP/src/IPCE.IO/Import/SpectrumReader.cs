using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Tables;

namespace IPCE.IO.Import;

public sealed record SpectrumColumn(
    int ColumnIndex,
    string Header,
    string DisplayName,
    int NumericValueCount);

public static class SpectrumReader
{
    public static IReadOnlyList<string> DiscoverSheets(string path) =>
        Array.AsReadOnly(
            NpoiWorkbookReader.GetSheetNames(path).ToArray());

    public static IReadOnlyList<SpectrumColumn> DiscoverColumns(
        string path,
        string sheetName = "Spectra")
    {
        WorkbookSheetData sheet = ReadSpectrumSheet(path, sheetName);
        SpectrumColumn[] columns = Enumerable.Range(0, sheet.ColumnCount)
            .Select(column => new
            {
                Column = column,
                Count = CalibrationReader.CountNumeric(sheet, column),
            })
            .Where(item => item.Count >= 2)
            .Select(item =>
            {
                string header = CalibrationReader.FindHeader(
                    sheet,
                    item.Column);
                if (header.Length == 0)
                {
                    header = $"第 {item.Column + 1} 列";
                }

                return new SpectrumColumn(
                    item.Column + 1,
                    header,
                    $"[{ExcelColumnName(item.Column + 1)}] {header}",
                    item.Count);
            })
            .ToArray();
        if (columns.Length == 0)
        {
            throw new IpceException(
                "IPCE:NoNumericSpectrumColumns",
                "所选表格中没有至少包含两个数值的列。");
        }

        return Array.AsReadOnly(columns);
    }

    public static IReadOnlyList<SpectrumPoint> Read(
        string path,
        string sheetName = "Spectra",
        int wavelengthColumn = 1,
        int irradianceColumn = 3)
    {
        WorkbookSheetData sheet = ReadSpectrumSheet(path, sheetName);
        int wavelengthIndex = wavelengthColumn - 1;
        int irradianceIndex = irradianceColumn - 1;
        if (wavelengthIndex < 0 ||
            irradianceIndex < 0 ||
            wavelengthIndex >= sheet.ColumnCount ||
            irradianceIndex >= sheet.ColumnCount)
        {
            throw new IpceException(
                "IPCE:SpectrumColumnMissing",
                "所选光谱列不存在。");
        }

        SpectrumPoint[] points = sheet.Rows
            .Where(row =>
                CalibrationReader.GetNumber(row, wavelengthIndex) is > 0 &&
                CalibrationReader.GetNumber(row, irradianceIndex) is >= 0)
            .Select(row => new SpectrumPoint(
                CalibrationReader.GetNumber(row, wavelengthIndex)!.Value,
                CalibrationReader.GetNumber(row, irradianceIndex)!.Value))
            .OrderBy(point => point.WavelengthNm)
            .GroupBy(point => point.WavelengthNm)
            .Select(group => new SpectrumPoint(
                group.Key,
                group.Average(point =>
                    point.IrradianceWattsPerSquareMetrePerNanometre)))
            .ToArray();
        if (points.Length < 2)
        {
            throw new IpceException(
                "IPCE:InvalidSpectrum",
                "有效的波长/光谱辐照度数据少于两个点。");
        }

        return Array.AsReadOnly(points);
    }

    private static WorkbookSheetData ReadSpectrumSheet(
        string path,
        string sheetName)
    {
        IReadOnlyList<string> names =
            NpoiWorkbookReader.GetSheetNames(path);
        string? actualName = names.FirstOrDefault(name =>
            string.Equals(
                name,
                sheetName,
                StringComparison.OrdinalIgnoreCase));
        if (actualName is null)
        {
            throw new IpceException(
                "IPCE:SpectrumSheetNotFound",
                $"工作簿中没有名为“{sheetName}”的表格。");
        }

        try
        {
            return NpoiWorkbookReader.ReadSheet(path, actualName);
        }
        catch (IpceException exception)
        {
            throw exception.Code == "IPCE:FileNotFound"
                ? exception
                : new IpceException(
                    "IPCE:SpectrumImportFailed",
                    exception.Message);
        }
    }

    private static string ExcelColumnName(int index)
    {
        string name = "";
        while (index > 0)
        {
            int remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }

        return name;
    }
}
