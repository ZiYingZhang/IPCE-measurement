using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.State;

namespace IPCE.Desktop.Plotting;

public sealed record SpectrumPlotModels(
    PlotModel Irradiance,
    PlotModel SelectedIpce,
    PlotModel Cumulative);

public static class ResultPlotModelBuilder
{
    public static PlotModel BuildTrace(
        string title,
        TraceData? trace,
        IReadOnlyList<AnchorPoint>? anchors,
        bool subtractDark,
        double darkStartSeconds,
        double darkEndSeconds,
        SchedulePreview? preview,
        double averagingDurationSeconds,
        IReadOnlyList<TraceMeanResult> means,
        ResultStatus status)
    {
        ArgumentNullException.ThrowIfNull(means);
        ArgumentNullException.ThrowIfNull(status);
        List<PlotSeries> series = [];
        if (trace is not null)
        {
            series.Add(new PlotSeries(
                "原始轨迹",
                trace.TimeSeconds,
                trace.CurrentAmperes,
                PlotSeriesKind.Line,
                "#1976D2"));
            if (anchors is { Count: > 0 })
            {
                double[] times = anchors
                    .Select(anchor => anchor.ConfirmedTimeSeconds)
                    .ToArray();
                series.Add(new PlotSeries(
                    "确认锚点",
                    times,
                    times.Select(time =>
                        NearestCurrent(trace, time)).ToArray(),
                    PlotSeriesKind.Scatter,
                    "#EF6C00",
                    contributesToAutoRange: false));
            }
        }

        PlotBand[] bands = subtractDark &&
            double.IsFinite(darkStartSeconds) &&
            double.IsFinite(darkEndSeconds) &&
            darkEndSeconds > darkStartSeconds
                ?
                [
                    new PlotBand(
                        darkStartSeconds,
                        darkEndSeconds,
                        "暗电流区间",
                        "#607D8B",
                        0.28),
                ]
                : [];
        IReadOnlyList<PlotIntervalMarker> intervals =
            BuildIntervals(
                preview,
                averagingDurationSeconds,
                means,
                status);
        return new PlotModel(
            title,
            "时间 (s)",
            "电流 (A)",
            series,
            bands,
            "导入 i-t 数据后显示轨迹。",
            intervals);
    }

    public static SpectrumPlotModels BuildSpectrumIntegration(
        IReadOnlyList<SpectrumPoint>? spectrum,
        IReadOnlyList<IpceValue> selectedIpce,
        IntegrationResult? integration,
        double requestedMinimumNm,
        double requestedMaximumNm)
    {
        ArgumentNullException.ThrowIfNull(selectedIpce);
        PlotBand[] bands = double.IsFinite(requestedMinimumNm) &&
            double.IsFinite(requestedMaximumNm) &&
            requestedMaximumNm > requestedMinimumNm
                ?
                [
                    new PlotBand(
                        requestedMinimumNm,
                        requestedMaximumNm,
                        "积分范围",
                        "#90CAF9",
                        0.24),
                ]
                : [];
        PlotViewportPolicy focusPolicy = BuildFocusPolicy(
            spectrum,
            selectedIpce,
            requestedMinimumNm,
            requestedMaximumNm);
        PlotModel irradiance = new(
            "太阳光谱",
            "波长 (nm)",
            "辐照度 (W m⁻² nm⁻¹)",
            spectrum is { Count: > 0 }
                ?
                [
                    new PlotSeries(
                        "辐照度",
                        spectrum.Select(point =>
                            point.WavelengthNm).ToArray(),
                        spectrum.Select(point =>
                            point.IrradianceWattsPerSquareMetrePerNanometre)
                            .ToArray(),
                        PlotSeriesKind.Line,
                        "#F57C00"),
                ]
                : [],
            bands,
            "导入太阳光谱后显示。",
            viewportPolicy: focusPolicy);
        PlotModel ipce = new(
            "积分所用 IPCE",
            "波长 (nm)",
            "IPCE (%)",
            selectedIpce.Count > 0
                ?
                [
                    new PlotSeries(
                        "选定 IPCE",
                        selectedIpce.Select(point =>
                            point.WavelengthNm).ToArray(),
                        selectedIpce.Select(point =>
                            point.IpcePercent).ToArray(),
                        PlotSeriesKind.Line,
                        "#1976D2"),
                ]
                : [],
            bands,
            "选择可用 IPCE 后显示。",
            viewportPolicy: focusPolicy);
        IReadOnlyList<IntegrationCurvePoint>? curve =
            integration?.Curve;
        PlotViewportPolicy cumulativePolicy =
            curve is { Count: > 0 }
                ? new PlotViewportPolicy(
                    PreferredMinimumX:
                        curve.Min(point => point.WavelengthNm),
                    PreferredMaximumX:
                        curve.Max(point => point.WavelengthNm))
                : new PlotViewportPolicy();
        PlotModel cumulative = new(
            "累计电流密度",
            "波长 (nm)",
            "累计 Jsc (mA cm⁻²)",
            curve is { Count: > 0 }
                ?
                [
                    new PlotSeries(
                        "累计 Jsc",
                        curve.Select(point =>
                            point.WavelengthNm).ToArray(),
                        curve.Select(point =>
                            point.CumulativeCurrentDensityMilliamperePerSquareCentimetre)
                            .ToArray(),
                        PlotSeriesKind.Line,
                        "#558B2F"),
                ]
                : [],
            [],
            "运行光谱积分后显示累计曲线。",
            viewportPolicy: cumulativePolicy);
        return new SpectrumPlotModels(
            irradiance,
            ipce,
            cumulative);
    }

    private static IReadOnlyList<PlotIntervalMarker> BuildIntervals(
        SchedulePreview? preview,
        double averagingDurationSeconds,
        IReadOnlyList<TraceMeanResult> means,
        ResultStatus status)
    {
        if (preview is null || means.Count == 0)
        {
            return [];
        }

        IReadOnlyList<PlotIntervalMarker> intervals;
        try
        {
            intervals = TraceOverlayBuilder.BuildMeans(
                preview,
                averagingDurationSeconds,
                means);
        }
        catch (IpceException exception)
            when (exception.Code == "IPCE:InvalidTraceOverlay")
        {
            return [];
        }

        if (status.Freshness != ResultFreshness.Stale)
        {
            return intervals;
        }

        return intervals.Select(interval =>
            new PlotIntervalMarker(
                interval.MinimumX,
                interval.MaximumX,
                interval.Y,
                "平均电流（结果已过期）",
                interval.ColorHex,
                $"{interval.HoverDetails}\n状态：结果已过期"))
            .ToArray();
    }

    private static PlotViewportPolicy BuildFocusPolicy(
        IReadOnlyList<SpectrumPoint>? spectrum,
        IReadOnlyList<IpceValue> selectedIpce,
        double requestedMinimumNm,
        double requestedMaximumNm)
    {
        if (spectrum is not { Count: > 0 } ||
            selectedIpce.Count == 0)
        {
            return new PlotViewportPolicy();
        }

        double commonMinimum = Math.Max(
            spectrum.Min(point => point.WavelengthNm),
            selectedIpce.Min(point => point.WavelengthNm));
        double commonMaximum = Math.Min(
            spectrum.Max(point => point.WavelengthNm),
            selectedIpce.Max(point => point.WavelengthNm));
        double minimum = Math.Max(requestedMinimumNm, commonMinimum);
        double maximum = Math.Min(requestedMaximumNm, commonMaximum);
        return maximum > minimum
            ? new PlotViewportPolicy(
                PreferredMinimumX: minimum,
                PreferredMaximumX: maximum)
            : new PlotViewportPolicy();
    }

    private static double NearestCurrent(
        TraceData trace,
        double time)
    {
        int nearest = 0;
        double distance = Math.Abs(trace.TimeSeconds[0] - time);
        for (int index = 1;
             index < trace.TimeSeconds.Count;
             index++)
        {
            double candidate =
                Math.Abs(trace.TimeSeconds[index] - time);
            if (candidate < distance)
            {
                nearest = index;
                distance = candidate;
            }
        }

        return trace.CurrentAmperes[nearest];
    }
}
