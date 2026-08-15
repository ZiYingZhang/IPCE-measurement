using System.Globalization;
using System.Text.RegularExpressions;
using IPCE.Core.Errors;

namespace IPCE.IO.Tables;

public static partial class DelimitedTableReader
{
    private const NumberStyles NumericStyles =
        NumberStyles.Float | NumberStyles.AllowThousands;

    public static TabularData Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new IpceException(
                "IPCE:FileNotFound",
                $"找不到文件：{path}");
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new IpceException(
                "IPCE:TableImportFailed",
                $"无法读取文本文件：{exception.Message}");
        }

        var parsedLines = lines
            .Select(line => new ParsedLine(line, SplitCells(line)))
            .ToArray();
        List<IReadOnlyList<double>> rows = [];
        int firstDataLine = -1;
        for (int lineIndex = 0; lineIndex < parsedLines.Length; lineIndex++)
        {
            double[] numericValues = parsedLines[lineIndex].Cells
                .Select(cell => TryParseNumber(cell, out double value)
                    ? value
                    : double.NaN)
                .Where(double.IsFinite)
                .ToArray();
            if (numericValues.Length < 2)
            {
                continue;
            }

            firstDataLine = firstDataLine < 0 ? lineIndex : firstDataLine;
            rows.Add(Array.AsReadOnly(numericValues[..2]));
        }

        string[] headers = ["", ""];
        if (firstDataLine >= 0)
        {
            for (int lineIndex = firstDataLine - 1;
                lineIndex >= 0;
                lineIndex--)
            {
                string[] candidates = parsedLines[lineIndex].Cells
                    .Where(cell => !string.IsNullOrWhiteSpace(cell))
                    .ToArray();
                if (candidates.Length < 2 ||
                    TryParseNumber(candidates[0], out _) ||
                    TryParseNumber(candidates[1], out _))
                {
                    continue;
                }

                headers = [candidates[0].Trim(), candidates[1].Trim()];
                break;
            }
        }

        string rawHeaderText = firstDataLine <= 0
            ? ""
            : string.Join(Environment.NewLine, lines[..firstDataLine]);
        return new TabularData(headers, rows, rawHeaderText);
    }

    private static string[] SplitCells(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }

        if (line.Contains('\t'))
        {
            return line.Split('\t', StringSplitOptions.TrimEntries);
        }

        if (line.Contains(';'))
        {
            return line.Split(';', StringSplitOptions.TrimEntries);
        }

        string[] whitespaceCells = WhitespacePattern()
            .Split(line.Trim())
            .Where(cell => cell.Length > 0)
            .ToArray();
        if (whitespaceCells.Length >= 2 &&
            whitespaceCells.All(cell => TryParseNumber(cell, out _)))
        {
            return whitespaceCells;
        }

        if (line.Contains(','))
        {
            return line.Split(',', StringSplitOptions.TrimEntries);
        }

        return whitespaceCells;
    }

    private static bool TryParseNumber(string text, out double value)
    {
        string normalized = text.Trim()
            .Replace('D', 'E')
            .Replace('d', 'e');
        CultureInfo current = CultureInfo.CurrentCulture;
        string currentDecimal =
            current.NumberFormat.NumberDecimalSeparator;
        bool preferCurrent =
            currentDecimal != "." &&
            normalized.Contains(currentDecimal, StringComparison.Ordinal);

        if (preferCurrent &&
            double.TryParse(normalized, NumericStyles, current, out value))
        {
            return double.IsFinite(value);
        }

        if (double.TryParse(
                normalized,
                NumericStyles,
                CultureInfo.InvariantCulture,
                out value))
        {
            return double.IsFinite(value);
        }

        return double.TryParse(
                normalized,
                NumericStyles,
                current,
                out value) &&
            double.IsFinite(value);
    }

    private sealed record ParsedLine(string Raw, string[] Cells);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
