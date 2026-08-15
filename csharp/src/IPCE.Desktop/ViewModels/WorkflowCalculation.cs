using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Desktop.ViewModels;

internal static class WorkflowCalculation
{
    public static IReadOnlyList<double> BuildWavelengths(
        double startNanometres,
        double endNanometres,
        double stepNanometres)
    {
        if (!double.IsFinite(startNanometres) ||
            !double.IsFinite(endNanometres) ||
            !double.IsFinite(stepNanometres) ||
            startNanometres <= 0 ||
            endNanometres < startNanometres ||
            stepNanometres <= 0)
        {
            throw new IpceException(
                "IPCE:InvalidWavelengthGrid",
                "波长起点和步长必须为正数，终点不得小于起点。");
        }

        double rawIntervals =
            (endNanometres - startNanometres) / stepNanometres;
        int intervals = checked((int)Math.Round(rawIntervals));
        double tolerance = Math.Max(
            1e-9,
            Math.Abs(rawIntervals) * 1e-12);
        if (Math.Abs(rawIntervals - intervals) > tolerance)
        {
            throw new IpceException(
                "IPCE:InvalidWavelengthGrid",
                "波长范围必须能被步长整除。");
        }

        return Array.AsReadOnly(Enumerable.Range(0, intervals + 1)
            .Select(index => startNanometres + index * stepNanometres)
            .ToArray());
    }

    public static IReadOnlyList<AnchorPoint> AnchorsOrEmpty(
        AlignmentMode mode,
        IReadOnlyList<AnchorPoint>? anchors)
    {
        if (mode == AlignmentMode.Anchors &&
            (anchors is null || anchors.Count == 0))
        {
            throw new IpceException(
                "IPCE:MissingAnchors",
                "锚点对齐模式至少需要一个时间锚点。");
        }

        return anchors ?? [];
    }
}
