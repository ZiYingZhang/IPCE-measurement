using System.Globalization;
using System.Text;
using IPCE.Core.Errors;
using NPOI.SS.UserModel;

namespace IPCE.IO.Tables;

public readonly record struct WorkbookCellValue(
    string Text,
    double? NumericValue,
    DateTime? DateValue);

public sealed record WorkbookSheetData
{
    public WorkbookSheetData(
        string name,
        IReadOnlyList<IReadOnlyList<WorkbookCellValue>> rows)
    {
        Name = name;
        Rows = Array.AsReadOnly(rows
            .Select(row =>
                (IReadOnlyList<WorkbookCellValue>)Array.AsReadOnly(
                    row.ToArray()))
            .ToArray());
    }

    public string Name { get; }

    public IReadOnlyList<IReadOnlyList<WorkbookCellValue>> Rows { get; }

    public int ColumnCount =>
        Rows.Count == 0 ? 0 : Rows.Max(row => row.Count);
}

public static class NpoiWorkbookReader
{
    static NpoiWorkbookReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static IReadOnlyList<string> GetSheetNames(string path)
    {
        using IWorkbook workbook = OpenWorkbook(path);
        string[] names = Enumerable.Range(0, workbook.NumberOfSheets)
            .Select(workbook.GetSheetName)
            .ToArray();
        return Array.AsReadOnly(names);
    }

    public static WorkbookSheetData ReadSheet(
        string path,
        string sheetName)
    {
        using IWorkbook workbook = OpenWorkbook(path);
        string? actualName = Enumerable.Range(0, workbook.NumberOfSheets)
            .Select(workbook.GetSheetName)
            .FirstOrDefault(name =>
                string.Equals(
                    name,
                    sheetName,
                    StringComparison.OrdinalIgnoreCase));
        if (actualName is null)
        {
            throw new IpceException(
                "IPCE:WorkbookSheetNotFound",
                $"工作簿中没有名为“{sheetName}”的表格。");
        }

        ISheet sheet = workbook.GetSheet(actualName);
        IFormulaEvaluator evaluator =
            workbook.GetCreationHelper().CreateFormulaEvaluator();
        int maximumColumnCount = Enumerable
            .Range(sheet.FirstRowNum, sheet.LastRowNum - sheet.FirstRowNum + 1)
            .Select(sheet.GetRow)
            .Where(row => row is not null)
            .Select(row => (int)row!.LastCellNum)
            .DefaultIfEmpty(0)
            .Max();
        List<IReadOnlyList<WorkbookCellValue>> rows = [];
        for (int rowIndex = sheet.FirstRowNum;
            rowIndex <= sheet.LastRowNum;
            rowIndex++)
        {
            IRow? row = sheet.GetRow(rowIndex);
            WorkbookCellValue[] values =
                new WorkbookCellValue[maximumColumnCount];
            for (int columnIndex = 0;
                columnIndex < maximumColumnCount;
                columnIndex++)
            {
                values[columnIndex] = NormalizeCell(
                    row?.GetCell(
                        columnIndex,
                        MissingCellPolicy.RETURN_BLANK_AS_NULL),
                    evaluator);
            }

            rows.Add(Array.AsReadOnly(values));
        }

        return new WorkbookSheetData(actualName, rows);
    }

    public static WorkbookSheetData ReadFirstSheet(string path)
    {
        IReadOnlyList<string> names = GetSheetNames(path);
        if (names.Count == 0)
        {
            throw new IpceException(
                "IPCE:WorkbookSheetNotFound",
                "工作簿中没有工作表。");
        }

        return ReadSheet(path, names[0]);
    }

    private static IWorkbook OpenWorkbook(string path)
    {
        if (!File.Exists(path))
        {
            throw new IpceException(
                "IPCE:FileNotFound",
                $"找不到工作簿：{path}");
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            return WorkbookFactory.Create(stream);
        }
        catch (Exception exception) when (exception is not IpceException)
        {
            throw new IpceException(
                "IPCE:WorkbookImportFailed",
                $"无法读取工作簿：{exception.Message}");
        }
    }

    private static WorkbookCellValue NormalizeCell(
        ICell? cell,
        IFormulaEvaluator evaluator)
    {
        if (cell is null)
        {
            return new WorkbookCellValue("", null, null);
        }

        if (cell.CellType == CellType.Formula)
        {
            CellValue evaluated = evaluator.Evaluate(cell);
            return evaluated.CellType switch
            {
                CellType.Numeric => new WorkbookCellValue(
                    evaluated.NumberValue.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                    evaluated.NumberValue,
                    null),
                CellType.String => FromText(evaluated.StringValue),
                CellType.Boolean => new WorkbookCellValue(
                    evaluated.BooleanValue ? "TRUE" : "FALSE",
                    evaluated.BooleanValue ? 1 : 0,
                    null),
                _ => new WorkbookCellValue("", null, null),
            };
        }

        return cell.CellType switch
        {
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) =>
                FromDateCell(cell),
            CellType.Numeric => new WorkbookCellValue(
                cell.NumericCellValue.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                cell.NumericCellValue,
                null),
            CellType.String => FromText(cell.StringCellValue),
            CellType.Boolean => new WorkbookCellValue(
                cell.BooleanCellValue ? "TRUE" : "FALSE",
                cell.BooleanCellValue ? 1 : 0,
                null),
            _ => new WorkbookCellValue("", null, null),
        };
    }

    private static WorkbookCellValue FromDateCell(ICell cell)
    {
        DateTime? date = cell.DateCellValue;
        return date.HasValue
            ? new WorkbookCellValue(
                date.Value.ToString("O", CultureInfo.InvariantCulture),
                null,
                date.Value)
            : new WorkbookCellValue("", null, null);
    }

    private static WorkbookCellValue FromText(string text)
    {
        string trimmed = text.Trim();
        double? numeric = double.TryParse(
            trimmed,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out double value)
            ? value
            : null;
        return new WorkbookCellValue(trimmed, numeric, null);
    }
}
