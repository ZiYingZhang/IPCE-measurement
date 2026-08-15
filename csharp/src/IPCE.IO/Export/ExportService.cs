using System.Globalization;
using System.Text;
using IPCE.Core.Errors;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace IPCE.IO.Export;

public static class ExportService
{
    public static IReadOnlyList<string> Write(
        IReadOnlyList<ExportTable> tables,
        string outputPath,
        ExportFormat format)
    {
        ValidateRequest(tables, outputPath);
        string fullOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);
        if (directory is null)
        {
            throw new IpceException(
                "IPCE:ExportFailed",
                "无法确定导出目录。");
        }

        try
        {
            Directory.CreateDirectory(directory);
            string[] writtenPaths = format switch
            {
                ExportFormat.Xlsx =>
                    WriteSingle(
                        fullOutputPath,
                        stream => WriteWorkbook(stream, tables)),
                ExportFormat.Csv =>
                    WriteCsv(fullOutputPath, tables),
                ExportFormat.Mat =>
                    WriteSingle(
                        fullOutputPath,
                        stream => MatExportWriter.Write(stream, tables)),
                _ => throw new IpceException(
                    "IPCE:ExportFailed",
                    $"不支持的导出格式：{format}"),
            };
            VerifyWrittenFiles(writtenPaths);
            return Array.AsReadOnly(writtenPaths);
        }
        catch (IpceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new IpceException(
                "IPCE:ExportFailed",
                $"无法写入导出文件：{exception.Message}");
        }
    }

    private static void ValidateRequest(
        IReadOnlyList<ExportTable> tables,
        string outputPath)
    {
        if (tables is null || tables.Count == 0)
        {
            throw new IpceException(
                "IPCE:NoExportSelection",
                "请至少选择一项要导出的数据。");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new IpceException(
                "IPCE:ExportFailed",
                "请选择导出文件路径。");
        }

        if (tables
            .GroupBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new IpceException(
                "IPCE:DuplicateExportNames",
                "导出项目的名称不能重复。");
        }

        foreach (ExportTable table in tables)
        {
            ValidateFileComponent(table.Name);
            ValidateMatlabFieldName(table.Name);
            foreach (ExportColumn column in table.Columns)
            {
                ValidateMatlabFieldName(column.Name);
            }
        }
    }

    private static void ValidateFileComponent(string value)
    {
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new IpceException(
                "IPCE:InvalidExportTable",
                $"导出名称“{value}”包含文件名不允许的字符。");
        }
    }

    private static void ValidateMatlabFieldName(string value)
    {
        bool valid = value.Length <= 63
            && char.IsLetter(value[0])
            && value.All(character =>
                char.IsLetterOrDigit(character) || character == '_');
        if (!valid)
        {
            throw new IpceException(
                "IPCE:InvalidExportTable",
                $"导出名称“{value}”不是有效的 MATLAB 字段名。");
        }
    }

    private static string[] WriteSingle(
        string outputPath,
        Action<Stream> writer)
    {
        WriteAtomically(outputPath, writer);
        return [outputPath];
    }

    private static string[] WriteCsv(
        string outputPath,
        IReadOnlyList<ExportTable> tables)
    {
        string[] paths = tables.Count == 1
            ? [outputPath]
            : tables.Select(table =>
                BuildSuffixedPath(outputPath, table.Name)).ToArray();

        var staged = new List<(string Temporary, string Target)>();
        try
        {
            for (int index = 0; index < tables.Count; index++)
            {
                string temporary = BuildTemporaryPath(paths[index]);
                staged.Add((temporary, paths[index]));
                using var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                WriteCsvTable(stream, tables[index]);
                stream.Flush(flushToDisk: true);
            }

            foreach ((string temporary, string target) in staged)
            {
                File.Move(temporary, target, overwrite: true);
            }

            return paths;
        }
        finally
        {
            foreach ((string temporary, _) in staged)
            {
                TryDelete(temporary);
            }
        }
    }

    private static string BuildSuffixedPath(
        string outputPath,
        string tableName)
    {
        string directory = Path.GetDirectoryName(outputPath)!;
        string stem = Path.GetFileNameWithoutExtension(outputPath);
        string extension = Path.GetExtension(outputPath);
        return Path.Combine(
            directory,
            $"{stem}_{tableName}{extension}");
    }

    private static void WriteAtomically(
        string targetPath,
        Action<Stream> writer)
    {
        string temporaryPath = BuildTemporaryPath(targetPath);
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                writer(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static string BuildTemporaryPath(string targetPath)
    {
        string directory = Path.GetDirectoryName(targetPath)!;
        string fileName = Path.GetFileName(targetPath);
        return Path.Combine(
            directory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void WriteWorkbook(
        Stream stream,
        IReadOnlyList<ExportTable> tables)
    {
        using IWorkbook workbook = new XSSFWorkbook();
        foreach (ExportTable table in tables)
        {
            ISheet sheet = workbook.CreateSheet(table.Name);
            IRow header = sheet.CreateRow(0);
            for (int columnIndex = 0;
                columnIndex < table.Columns.Count;
                columnIndex++)
            {
                header.CreateCell(columnIndex)
                    .SetCellValue(table.Columns[columnIndex].Name);
            }

            for (int rowIndex = 0;
                rowIndex < table.RowCount;
                rowIndex++)
            {
                IRow row = sheet.CreateRow(rowIndex + 1);
                for (int columnIndex = 0;
                    columnIndex < table.Columns.Count;
                    columnIndex++)
                {
                    SetWorkbookCell(
                        row.CreateCell(columnIndex),
                        table.Columns[columnIndex],
                        rowIndex);
                }
            }
        }

        workbook.Write(stream, leaveOpen: true);
    }

    private static void SetWorkbookCell(
        ICell cell,
        ExportColumn column,
        int rowIndex)
    {
        object? value = column.Values[rowIndex];
        if (value is null)
        {
            cell.SetBlank();
        }
        else if (column.DataType == typeof(bool))
        {
            cell.SetCellValue((bool)value);
        }
        else if (column.DataType == typeof(string))
        {
            cell.SetCellValue((string)value);
        }
        else if (column.DataType == typeof(DateTime))
        {
            cell.SetCellValue((DateTime)value);
        }
        else
        {
            cell.SetCellValue(
                Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }
    }

    private static void WriteCsvTable(
        Stream stream,
        ExportTable table)
    {
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            leaveOpen: true)
        {
            NewLine = "\r\n",
        };
        writer.WriteLine(string.Join(
            ",",
            table.Columns.Select(column => QuoteCsv(column.Name))));
        for (int rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
        {
            writer.WriteLine(string.Join(
                ",",
                table.Columns.Select(column =>
                    QuoteCsv(FormatCsvValue(column, rowIndex)))));
        }

        writer.Flush();
    }

    private static string FormatCsvValue(
        ExportColumn column,
        int rowIndex)
    {
        object? value = column.Values[rowIndex];
        if (value is null)
        {
            return "";
        }

        if (column.DataType == typeof(bool))
        {
            return (bool)value ? "TRUE" : "FALSE";
        }

        if (column.DataType == typeof(DateTime))
        {
            return ((DateTime)value).ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        if (column.DataType == typeof(string))
        {
            return (string)value;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture)
            .ToString("R", CultureInfo.InvariantCulture);
    }

    private static string QuoteCsv(string value)
    {
        bool needsQuotes = value.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        return needsQuotes
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void VerifyWrittenFiles(
        IReadOnlyList<string> writtenPaths)
    {
        foreach (string path in writtenPaths)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                throw new IpceException(
                    "IPCE:ExportVerificationFailed",
                    $"导出文件未成功写入或为空：{path}");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup must not mask the export error.
        }
    }
}
