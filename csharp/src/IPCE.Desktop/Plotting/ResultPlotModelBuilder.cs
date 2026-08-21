using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
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
        ResultStatus status,
        ILocalizationService? localization = null)
    {
        ILocalizationService text =
            localization ?? LocalizationService.Current;
        ArgumentNullException.ThrowIfNull(means);
        ArgumentNullException.ThrowIfNull(status);
        List<PlotSeries> series = [];
        if (trace is not null)
        {
            series.Add(new PlotSeries(
                text["Plot.RawTrace"],
                trace.TimeSeconds,
                trace.CurrentAmperes,
                PlotSeriesKind.Line,
                "#1976D2",
                id: "raw-trace"));
            if (anchors is { Count: > 0 })
            {
                double[] times = anchors
                    .Select(anchor => anchor.ConfirmedTimeSeconds)
                    .ToArray();
                series.Add(new PlotSeries(
                    text["Plot.ConfirmedAnchors"],
                    times,
                    times.Select(time =>
                        NearestCurrent(trace, time)).ToArray(),
                    PlotSeriesKind.Scatter,
                    "#EF6C00",
                    contributesToAutoRange: false,
                    id: "confirmed-anchors"));
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
                        text["Plot.DarkCurrentRange"],
                        PlotTheme.RangeFillColorHex,
                        PlotTheme.RangeFillOpacity),
                ]
                : [];
        IReadOnlyList<PlotIntervalMarker> intervals =
            BuildIntervals(
                preview,
                averagingDurationSeconds,
                means,
                status,
                text);
        return new PlotModel(
            title,
            text["Plot.TimeAxis"],
            text["Plot.CurrentAxis"],
            series,
            bands,
            text["Plot.EmptyTrace"],
            intervals);
    }

    public static SpectrumPlotModels BuildSpectrumIntegration(
        IReadOnlyList<SpectrumPoint>? spectrum,
        IReadOnlyList<IpceValue> selectedIpce,
        IntegrationResult? integration,
        double requestedMinimumNm,
        double requestedMaximumNm,
        ILocalizationService? localization = null)
    {
        ILocalizationService text =
            localization ?? LocalizationService.Current;
        ArgumentNullException.ThrowIfNull(selectedIpce);
        PlotBand[] bands = double.IsFinite(requestedMinimumNm) &&
            double.IsFinite(requestedMaximumNm) &&
            requestedMaximumNm > requestedMinimumNm
                ?
                [
                    new PlotBand(
                        requestedMinimumNm,
                        requestedMaximumNm,
                        text["Plot.IntegrationRange"],
                        PlotTheme.RangeFillColorHex,
                        PlotTheme.RangeFillOpacity),
                ]
                : [];
        PlotViewportPolicy focusPolicy = BuildFocusPolicy(
            spectrum,
            selectedIpce,
            requestedMinimumNm,
            requestedMaximumNm);
        PlotModel irradiance = new(
            text["Plot.SolarSpectrum"],
            text["Plot.WavelengthAxis"],
            text["Plot.IrradianceAxis"],
            spectrum is { Count: > 0 }
                ?
                [
                    new PlotSeries(
                        text["Plot.IrradianceSeries"],
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
            text["Plot.EmptySpectrum"],
            viewportPolicy: focusPolicy);
        PlotModel ipce = new(
            text["Plot.SelectedIpceTitle"],
            text["Plot.WavelengthAxis"],
            "IPCE (%)",
            selectedIpce.Count > 0
                ?
                [
                    new PlotSeries(
                        text["Plot.SelectedIpceSeries"],
                        selectedIpce.Select(point =>
                            point.WavelengthNm).ToArray(),
                        selectedIpce.Select(point =>
                            point.IpcePercent).ToArray(),
                        PlotSeriesKind.Line,
                        "#1976D2"),
                ]
                : [],
            bands,
            text["Plot.EmptySelectedIpce"],
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
            text["Plot.CumulativeTitle"],
            text["Plot.WavelengthAxis"],
            text["Plot.CumulativeAxis"],
            curve is { Count: > 0 }
                ?
                [
                    new PlotSeries(
                        text["Plot.CumulativeSeries"],
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
            text["Plot.EmptyCumulative"],
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
        ResultStatus status,
        ILocalizationService localization)
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
                means,
                localization);
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
                localization["TraceOverlay.StaleMeanCurrent"],
                interval.ColorHex,
                localization.Format(
                    "TraceOverlay.StaleHover",
                    interval.HoverDetails)))
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
