using System.Globalization;
using MatFileHandler;

namespace IPCE.IO.Export;

internal static class MatExportWriter
{
    public static void Write(
        Stream stream,
        IReadOnlyList<ExportTable> tables)
    {
        var builder = new DataBuilder();
        IStructureArray root = builder.NewStructureArray(
            tables.Select(table => table.Name),
            [1, 1]);

        foreach (ExportTable table in tables)
        {
            root[table.Name, 0, 0] = BuildTable(builder, table);
        }

        IVariable variable =
            builder.NewVariable("exportData", root, isGlobal: false);
        IMatFile file = builder.NewFile([variable]);
        var writer = new MatFileWriter(new LeaveOpenStream(stream));
        writer.Write(file);
    }

    private static IStructureArray BuildTable(
        DataBuilder builder,
        ExportTable table)
    {
        string[] fields =
        [
            "VariableNames",
            .. table.Columns.Select(column => column.Name),
            "RowCount",
        ];
        IStructureArray result =
            builder.NewStructureArray(fields, [1, 1]);

        ICellArray variableNames =
            builder.NewCellArray([1, table.Columns.Count]);
        for (int columnIndex = 0;
            columnIndex < table.Columns.Count;
            columnIndex++)
        {
            variableNames[0, columnIndex] =
                builder.NewCharArray(table.Columns[columnIndex].Name);
        }

        result["VariableNames", 0, 0] = variableNames;
        foreach (ExportColumn column in table.Columns)
        {
            result[column.Name, 0, 0] =
                BuildColumn(builder, column, table.RowCount);
        }

        result["RowCount", 0, 0] = builder.NewArray(
            new[] { Convert.ToDouble(table.RowCount) },
            [1, 1]);
        return result;
    }

    private static IArray BuildColumn(
        DataBuilder builder,
        ExportColumn column,
        int rowCount)
    {
        int[] dimensions = [rowCount, 1];
        if (column.DataType == typeof(bool))
        {
            bool[] values = column.Values
                .Select(value => value is not null && (bool)value)
                .ToArray();
            return builder.NewArray(values, dimensions);
        }

        if (column.DataType == typeof(string))
        {
            ICellArray values = builder.NewCellArray(dimensions);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                values[rowIndex, 0] = builder.NewCharArray(
                    column.Values[rowIndex]?.ToString() ?? "");
            }

            return values;
        }

        if (column.DataType == typeof(DateTime))
        {
            ICellArray values = builder.NewCellArray(dimensions);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                string text = column.Values[rowIndex] is DateTime date
                    ? date.ToString("O", CultureInfo.InvariantCulture)
                    : "";
                values[rowIndex, 0] = builder.NewCharArray(text);
            }

            return values;
        }

        double[] numericValues = column.Values
            .Select(value => value is null
                ? double.NaN
                : Convert.ToDouble(value, CultureInfo.InvariantCulture))
            .ToArray();
        return builder.NewArray(numericValues, dimensions);
    }

    private sealed class LeaveOpenStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            inner.SetLength(value);

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Flush();
            }

            base.Dispose(disposing);
        }
    }
}
