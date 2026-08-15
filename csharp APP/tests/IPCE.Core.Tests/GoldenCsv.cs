using System.Globalization;

namespace IPCE.Core.Tests;

internal static class GoldenCsv
{
    public static IReadOnlyList<IReadOnlyDictionary<string, double>> ReadNumeric(
        string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Golden",
            fileName);
        string[] lines = File.ReadAllLines(path);
        string[] headers = lines[0]
            .Split(',')
            .Select(header => header.Trim('"'))
            .ToArray();

        return lines.Skip(1)
            .Where(line => line.Length > 0)
            .Select(line =>
            {
                string[] values = line.Split(',');
                return (IReadOnlyDictionary<string, double>)headers
                    .Select((header, index) => new
                    {
                        Header = header,
                        Value = double.Parse(
                            values[index],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture),
                    })
                    .ToDictionary(item => item.Header, item => item.Value);
            })
            .ToArray();
    }
}
