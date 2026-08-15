using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Tables;

namespace IPCE.IO.Import;

public static class ExternalIpceReader
{
    public static ExternalIpceData Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new IpceException(
                "IPCE:FileNotFound",
                $"找不到外部 IPCE 文件：{path}");
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not ".txt" and not ".csv" and
            not ".xls" and not ".xlsx")
        {
            throw new IpceException(
                "IPCE:UnsupportedExternalIPCE",
                "外部 IPCE 导入仅支持 TXT、CSV、XLS 和 XLSX 文件。");
        }

        ExternalIpceInput input = extension is ".xls" or ".xlsx"
            ? ReadWorkbook(path)
            : ReadText(path);
        IpceValue[] points = input.Rows
            .Where(row => row.WavelengthNm > 0)
            .Select(row => new IpceValue(
                row.WavelengthNm,
                row.IpcePercent))
            .OrderBy(point => point.WavelengthNm)
            .GroupBy(point => point.WavelengthNm)
            .Select(group => new IpceValue(
                group.Key,
                group.Average(point => point.IpcePercent)))
            .ToArray();
        if (points.Length < 2)
        {
            throw new IpceException(
                "IPCE:InvalidExternalIPCE",
                "外部 IPCE 文件至少需要两个不同的有效波长。");
        }

        return new ExternalIpceData(
            points,
            input.WavelengthHeader,
            input.IpceHeader);
    }

    private static ExternalIpceInput ReadText(string path)
    {
        TabularData table = DelimitedTableReader.Read(path);
        return new ExternalIpceInput(
            table.NumericRows
                .Select(row => (row[0], row[1]))
                .ToArray(),
            table.Headers.ElementAtOrDefault(0) ?? "",
            table.Headers.ElementAtOrDefault(1) ?? "");
    }

    private static ExternalIpceInput ReadWorkbook(string path)
    {
        WorkbookSheetData sheet =
            NpoiWorkbookReader.ReadFirstSheet(path);
        int[] numericColumns = Enumerable
            .Range(0, sheet.ColumnCount)
            .Where(column =>
                CalibrationReader.CountNumeric(sheet, column) >= 2)
            .Take(2)
            .ToArray();
        if (numericColumns.Length < 2)
        {
            throw new IpceException(
                "IPCE:InvalidExternalIPCE",
                "未能识别出外部 IPCE 的波长列和 IPCE 列。");
        }

        int wavelengthColumn = numericColumns[0];
        int ipceColumn = numericColumns[1];
        (double WavelengthNm, double IpcePercent)[] rows =
            sheet.Rows
                .Select(row => (
                    Wavelength: CalibrationReader.GetNumber(
                        row,
                        wavelengthColumn),
                    Ipce: CalibrationReader.GetNumber(row, ipceColumn)))
                .Where(row =>
                    row.Wavelength.HasValue &&
                    row.Ipce.HasValue)
                .Select(row => (
                    row.Wavelength!.Value,
                    row.Ipce!.Value))
                .ToArray();
        return new ExternalIpceInput(
            rows,
            CalibrationReader.FindHeader(sheet, wavelengthColumn),
            CalibrationReader.FindHeader(sheet, ipceColumn));
    }

    private sealed record ExternalIpceInput(
        IReadOnlyList<(double WavelengthNm, double IpcePercent)> Rows,
        string WavelengthHeader,
        string IpceHeader);
}
