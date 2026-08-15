using IPCE.Core.Errors;
using IPCE.Core.Extraction;
using IPCE.Desktop.Localization;

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
        IReadOnlyList<TraceMeanResult> means,
        ILocalizationService? localization = null)
    {
        ILocalizationService text =
            localization ?? LocalizationService.Current;
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
                text["TraceOverlay.MeanCurrent"],
                "#EF6C00",
                text.Format(
                    "TraceOverlay.Hover",
                    mean.WavelengthNm,
                    start,
                    end,
                    mean.MeanCurrentAmperes,
                    mean.SampleCount)));
        }

        return Array.AsReadOnly(markers.ToArray());
    }

    private static IpceException InvalidOverlay() =>
        new(
            "IPCE:InvalidTraceOverlay",
            "平均电流覆盖层与时间调度无法一一匹配。");
}
