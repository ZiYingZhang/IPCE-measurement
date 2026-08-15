using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Calculation;

public static class IpceSourceResolver
{
    public static IReadOnlyList<IpceValue> Resolve(
        IReadOnlyList<IpcePoint>? calculated,
        ExternalIpceData? external,
        IpceSource source)
    {
        switch (source)
        {
            case IpceSource.Calculated:
                if (calculated is null || calculated.Count == 0)
                {
                    throw new IpceException(
                        "IPCE:MissingCalculatedIPCE",
                        "尚未得到可用于积分的计算 IPCE。");
                }

                return Array.AsReadOnly(calculated
                    .Select(point =>
                        new IpceValue(point.WavelengthNm, point.IpcePercent))
                    .ToArray());

            case IpceSource.External:
                if (external is null)
                {
                    throw new IpceException(
                        "IPCE:MissingExternalIPCE",
                        "尚未导入可用于积分的外部 IPCE。");
                }

                return Array.AsReadOnly(external.Points.ToArray());

            default:
                throw new IpceException(
                    "IPCE:UnknownIPCESource",
                    $"未知的 IPCE 来源：{source}");
        }
    }
}
