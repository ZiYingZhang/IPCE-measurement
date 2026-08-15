using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.IO.Tables;

namespace IPCE.IO.Import;

public static class AnchorReader
{
    public static IReadOnlyList<AnchorPoint> Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new IpceException(
                "IPCE:AnchorFileNotFound",
                $"未找到锚点文件：{path}");
        }

        TabularData table = DelimitedTableReader.Read(path);
        AnchorPoint[] anchors = table.NumericRows
            .Select(row => new AnchorPoint(row[0], row[1]))
            .ToArray();
        if (anchors.Length == 0 ||
            anchors.Any(anchor =>
                !double.IsFinite(anchor.WavelengthNm) ||
                !double.IsFinite(anchor.ConfirmedTimeSeconds) ||
                anchor.WavelengthNm <= 0) ||
            anchors
                .GroupBy(anchor => anchor.WavelengthNm)
                .Any(group => group.Skip(1).Any()))
        {
            throw new IpceException(
                "IPCE:InvalidAnchorFile",
                "锚点必须包含有限数值、正波长，并且锚点波长不能重复。");
        }

        return Array.AsReadOnly(anchors);
    }
}
