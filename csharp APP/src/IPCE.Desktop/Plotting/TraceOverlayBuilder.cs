using IPCE.Core.Errors;
using IPCE.Core.Extraction;

namespace IPCE.Desktop.Plotting;

public sealed record TraceMeanResult(
    double WavelengthNm,
    double MeanCurrentAmperes,
    int SampleCount);

public static class TraceOverlayBuilder
{
    public static IReadOnlyList<PlotIntervalMarker> BuildMeans(
        SchedulePreview? preview,
        double averagingDurationSeconds,
        IReadOnlyList<TraceMeanResult> means)
    {
        ArgumentNullException.ThrowIfNull(means);
        if (preview is null)
        {
            if (means.Count == 0)
            {
                return Array.Empty<PlotIntervalMarker>();
            }

            throw InvalidOverlay();
        }

        bool invalidMeans = means.Any(mean =>
            !double.IsFinite(mean.WavelengthNm) ||
            mean.WavelengthNm <= 0 ||
            !double.IsFinite(mean.MeanCurrentAmperes) ||
            mean.SampleCount <= 0);
        bool duplicateMeans = means
            .GroupBy(mean => mean.WavelengthNm)
            .Any(group => group.Count() > 1);
        bool duplicateSchedule = preview.Points
            .GroupBy(point => point.WavelengthNm)
            .Any(group => group.Count() > 1);
        if (invalidMeans ||
            duplicateMeans ||
            duplicateSchedule ||
            means.Count != preview.Points.Count)
        {
            throw InvalidOverlay();
        }

        Dictionary<double, TraceMeanResult> byWavelength =
            means.ToDictionary(mean => mean.WavelengthNm);
        List<PlotIntervalMarker> markers =
            new(preview.Points.Count);
        foreach (var point in preview.Points)
        {
            if (!byWavelength.TryGetValue(
                    point.WavelengthNm,
                    out TraceMeanResult? mean))
            {
                throw InvalidOverlay();
            }

            (double start, double end) =
                AverageWindowResolver.Resolve(
                    point,
                    averagingDurationSeconds);
            markers.Add(new PlotIntervalMarker(
                start,
                end,
                mean.MeanCurrentAmperes,
                "平均电流",
                "#EF6C00",
                $"波长：{mean.WavelengthNm:G8} nm\n" +
                $"平均窗口：{start:G8}–{end:G8} s\n" +
                $"平均电流：{mean.MeanCurrentAmperes:E6} A\n" +
                $"样本数：{mean.SampleCount}"));
        }

        return Array.AsReadOnly(markers.ToArray());
    }

    private static IpceException InvalidOverlay() =>
        new(
            "IPCE:InvalidTraceOverlay",
            "平均电流覆盖层与时间调度无法一一匹配。");
}
