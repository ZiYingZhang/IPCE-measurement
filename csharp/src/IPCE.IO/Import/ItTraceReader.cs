using System.Text.RegularExpressions;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Tables;

namespace IPCE.IO.Import;

public sealed record UnitOverrides(
    string TimeUnit,
    string CurrentUnit);

public static class ItTraceReader
{
    public static TraceImportInspection Inspect(string path)
    {
        TabularData table = DelimitedTableReader.Read(path);
        return Inspect(table);
    }

    public static TraceData Read(
        string path,
        UnitOverrides? overrides = null)
    {
        TabularData table = DelimitedTableReader.Read(path);
        if (table.NumericRows.Count < 2)
        {
            throw new IpceException(
                "IPCE:InvalidTrace",
                "i-t 文件中有效的时间/电流数据少于两个点。");
        }

        TraceImportInspection inspection = Inspect(table);
        string timeHeader = inspection.TimeHeader;
        string currentHeader = inspection.CurrentHeader;
        string timeUnit = overrides is null
            ? inspection.DetectedTimeUnit
            : ValidateTimeUnit(overrides.TimeUnit);
        string currentUnit = overrides is null
            ? inspection.DetectedCurrentUnit
            : ValidateCurrentUnit(overrides.CurrentUnit);
        if (timeUnit.Length == 0 || currentUnit.Length == 0)
        {
            throw new IpceException(
                "IPCE:TraceUnitsRequired",
                "无法从 i-t 表头识别时间或电流单位；请明确选择单位。");
        }

        double timeFactor = TimeToSecondsFactor(timeUnit);
        double currentFactor = CurrentToAmperesFactor(currentUnit);
        (double Time, double Current)[] samples = table.NumericRows
            .Select(row => (
                row[0] * timeFactor,
                row[1] * currentFactor))
            .Where(sample =>
                double.IsFinite(sample.Item1) &&
                double.IsFinite(sample.Item2))
            .OrderBy(sample => sample.Item1)
            .ToArray();
        var metadata = new TraceMetadata(
            timeHeader,
            currentHeader,
            timeUnit,
            currentUnit,
            timeFactor,
            currentFactor,
            table.RawHeaderText);
        return new TraceData(
            samples.Select(sample => sample.Time).ToArray(),
            samples.Select(sample => sample.Current).ToArray(),
            metadata);
    }

    private static TraceImportInspection Inspect(TabularData table)
    {
        string timeHeader =
            table.Headers.ElementAtOrDefault(0) ?? "";
        string currentHeader =
            table.Headers.ElementAtOrDefault(1) ?? "";
        return new TraceImportInspection(
            timeHeader,
            currentHeader,
            DetectTimeUnit(timeHeader),
            DetectCurrentUnit(currentHeader));
    }

    private static string DetectTimeUnit(string header)
    {
        foreach (string alias in new[] { "min", "ms", "second", "sec", "s", "h" })
        {
            if (ContainsUnitToken(header, alias))
            {
                return alias;
            }
        }

        return "";
    }

    private static string DetectCurrentUnit(string header)
    {
        string normalized = header.Replace('µ', 'u').Replace('μ', 'u');
        foreach (string alias in new[] { "pA", "nA", "uA", "mA", "A" })
        {
            if (ContainsUnitToken(normalized, alias))
            {
                return alias;
            }
        }

        return "";
    }

    private static bool ContainsUnitToken(string text, string unit)
    {
        return Regex.IsMatch(
            text,
            $@"(^|[^A-Za-z]){Regex.Escape(unit)}($|[^A-Za-z])",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
    }

    private static string ValidateTimeUnit(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "s" or "sec" or "second" or "seconds" => "s",
            "ms" => "ms",
            "min" or "minute" or "minutes" => "min",
            "h" or "hr" or "hour" or "hours" => "h",
            _ => throw new IpceException(
                "IPCE:UnsupportedTimeUnit",
                $"不支持的时间单位：{value}。"),
        };
    }

    private static string ValidateCurrentUnit(string value)
    {
        string normalized = value.Trim()
            .Replace('µ', 'u')
            .Replace('μ', 'u')
            .ToLowerInvariant();
        return normalized switch
        {
            "a" => "A",
            "ma" => "mA",
            "ua" => "uA",
            "na" => "nA",
            "pa" => "pA",
            _ => throw new IpceException(
                "IPCE:UnsupportedCurrentUnit",
                $"不支持的电流单位：{value}。"),
        };
    }

    private static double TimeToSecondsFactor(string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "s" or "sec" or "second" => 1,
            "ms" => 1e-3,
            "min" => 60,
            "h" => 3600,
            _ => throw new IpceException(
                "IPCE:UnsupportedTimeUnit",
                $"不支持的时间单位：{unit}。"),
        };
    }

    private static double CurrentToAmperesFactor(string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "a" => 1,
            "ma" => 1e-3,
            "ua" => 1e-6,
            "na" => 1e-9,
            "pa" => 1e-12,
            _ => throw new IpceException(
                "IPCE:UnsupportedCurrentUnit",
                $"不支持的电流单位：{unit}。"),
        };
    }
}
