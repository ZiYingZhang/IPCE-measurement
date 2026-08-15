using IPCE.Core.Errors;

namespace IPCE.IO.Export;

public enum ExportFormat
{
    Xlsx,
    Csv,
    Mat,
}

public sealed record ExportColumn
{
    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(bool),
        typeof(string),
        typeof(DateTime),
    ];

    public ExportColumn(
        string name,
        Type dataType,
        IReadOnlyList<object?> values)
    {
        if (string.IsNullOrWhiteSpace(name)
            || dataType is null
            || values is null
            || !SupportedTypes.Contains(dataType))
        {
            throw InvalidTable(
                "导出列必须具有名称、受支持的数据类型和数据。");
        }

        object?[] copiedValues = values.ToArray();
        if (copiedValues.Any(value =>
            !IsCompatibleValue(value, dataType)))
        {
            throw InvalidTable(
                $"列“{name}”包含与声明类型不匹配的数据。");
        }

        Name = name.Trim();
        DataType = dataType;
        Values = Array.AsReadOnly(copiedValues);
    }

    public string Name { get; }

    public Type DataType { get; }

    public IReadOnlyList<object?> Values { get; }

    private static bool IsCompatibleValue(object? value, Type dataType)
    {
        if (value is null)
        {
            return true;
        }

        if (dataType == typeof(string))
        {
            return value is string;
        }

        if (dataType == typeof(bool))
        {
            return value is bool;
        }

        if (dataType == typeof(DateTime))
        {
            return value is DateTime;
        }

        return value is IConvertible;
    }

    private static IpceException InvalidTable(string message) =>
        new("IPCE:InvalidExportTable", message);
}

public sealed record ExportTable
{
    public ExportTable(
        string name,
        IReadOnlyList<ExportColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(name)
            || columns is null
            || columns.Count == 0)
        {
            throw InvalidTable("导出表必须具有名称和至少一列数据。");
        }

        ExportColumn[] copiedColumns = columns.ToArray();
        if (copiedColumns.Any(column => column is null))
        {
            throw InvalidTable("导出表不能包含空列。");
        }

        string[] duplicateNames = copiedColumns
            .GroupBy(column => column.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
        {
            throw InvalidTable("导出表不能包含同名列。");
        }

        if (copiedColumns.Any(column =>
            column.Name is "VariableNames" or "RowCount"))
        {
            throw InvalidTable(
                "列名不能使用保留字段 VariableNames 或 RowCount。");
        }

        int rowCount = copiedColumns[0].Values.Count;
        if (copiedColumns.Any(column =>
            column.Values.Count != rowCount))
        {
            throw InvalidTable("同一导出表中的所有列必须具有相同行数。");
        }

        Name = name.Trim();
        Columns = Array.AsReadOnly(copiedColumns);
        RowCount = rowCount;
    }

    public string Name { get; }

    public IReadOnlyList<ExportColumn> Columns { get; }

    public int RowCount { get; }

    private static IpceException InvalidTable(string message) =>
        new("IPCE:InvalidExportTable", message);
}
