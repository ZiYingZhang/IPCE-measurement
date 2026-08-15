using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Tables;

namespace IPCE.IO.Import;

public static class CalibrationReader
{
    public static CalibrationData Read(string path)
    {
        WorkbookSheetData sheet;
        try
        {
            sheet = NpoiWorkbookReader.ReadFirstSheet(path);
        }
        catch (IpceException exception)
        {
            throw exception.Code == "IPCE:FileNotFound"
                ? exception
                : new IpceException(
                    "IPCE:ReferenceImportFailed",
                    exception.Message);
        }

        int[] candidateColumns = Enumerable.Range(0, sheet.ColumnCount)
            .Where(column => CountNumeric(sheet, column) >= 2)
            .ToArray();
        if (candidateColumns.Length < 2)
        {
            throw new IpceException(
                "IPCE:InvalidReference",
                "未能识别出波长和响应度两列数值。");
        }

        int wavelengthColumn = FindHeaderColumn(
            sheet,
            candidateColumns,
            ["波长", "wavelength", "lambda", "nm"]);
        if (wavelengthColumn < 0)
        {
            wavelengthColumn = candidateColumns[0];
        }

        int responsivityColumn = FindHeaderColumn(
            sheet,
            candidateColumns,
            ["响应度", "responsivity", "response", "a/w", "a per w"]);
        if (responsivityColumn < 0 ||
            responsivityColumn == wavelengthColumn)
        {
            responsivityColumn = candidateColumns.First(
                column => column != wavelengthColumn);
        }

        CalibrationPoint[] points = sheet.Rows
            .Where(row =>
                GetNumber(row, wavelengthColumn) is > 0 &&
                GetNumber(row, responsivityColumn) is > 0)
            .Select(row => new CalibrationPoint(
                GetNumber(row, wavelengthColumn)!.Value,
                GetNumber(row, responsivityColumn)!.Value))
            .OrderBy(point => point.WavelengthNm)
            .GroupBy(point => point.WavelengthNm)
            .Select(group => new CalibrationPoint(
                group.Key,
                group.Average(point =>
                    point.ResponsivityAmperesPerWatt)))
            .ToArray();
        if (points.Length < 2)
        {
            throw new IpceException(
                "IPCE:InvalidReference",
                "有效正波长/正响应度数据少于两个点。");
        }

        return new CalibrationData(points);
    }

    private static int FindHeaderColumn(
        WorkbookSheetData sheet,
        IReadOnlyList<int> candidates,
        IReadOnlyList<string> aliases)
    {
        foreach (int column in candidates)
        {
            string header = FindHeader(sheet, column).ToLowerInvariant();
            if (aliases.Any(alias =>
                header.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return column;
            }
        }

        return -1;
    }

    internal static string FindHeader(
        WorkbookSheetData sheet,
        int column)
    {
        int firstNumericRow = Enumerable.Range(0, sheet.Rows.Count)
            .FirstOrDefault(
                row => GetNumber(sheet.Rows[row], column).HasValue,
                -1);
        for (int row = firstNumericRow - 1; row >= 0; row--)
        {
            if (column >= sheet.Rows[row].Count)
            {
                continue;
            }

            string text = sheet.Rows[row][column].Text.Trim();
            if (text.Length > 0 &&
                !sheet.Rows[row][column].NumericValue.HasValue)
            {
                return text;
            }
        }

        return "";
    }

    internal static double? GetNumber(
        IReadOnlyList<WorkbookCellValue> row,
        int column)
    {
        return column < row.Count ? row[column].NumericValue : null;
    }

    internal static int CountNumeric(
        WorkbookSheetData sheet,
        int column)
    {
        return sheet.Rows.Count(row => GetNumber(row, column).HasValue);
    }
}
