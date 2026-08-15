using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Scheduling;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Plotting;

public sealed record CoveragePreview(
    double DataMinimum,
    double DataMaximum,
    double RequestedMinimum,
    double RequestedMaximum,
    bool IsWithinCoverage,
    string Message);

public sealed record SchedulePreview(
    IReadOnlyList<SchedulePoint> Points,
    IReadOnlyList<AnchorPoint> Anchors,
    CoveragePreview Coverage);

public static class WorkflowPreviewBuilder
{
    public static SchedulePreview BuildSchedule(
        TraceData trace,
        IReadOnlyList<double> wavelengths,
        AlignmentMode mode,
        IReadOnlyList<AnchorPoint> anchors,
        double fixedStartTimeSeconds,
        double nominalDelaySeconds,
        ILocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(wavelengths);
        ArgumentNullException.ThrowIfNull(anchors);
        IReadOnlyList<SchedulePoint> schedule =
            ScheduleBuilder.Build(
                wavelengths,
                mode,
                anchors,
                fixedStartTimeSeconds,
                nominalDelaySeconds);
        double dataMinimum = trace.TimeSeconds[0];
        double dataMaximum = trace.TimeSeconds[^1];
        double requestedMinimum = schedule[0].WindowStartSeconds;
        double requestedMaximum = schedule[^1].WindowEndSeconds;
        return new SchedulePreview(
            Array.AsReadOnly(schedule.ToArray()),
            Array.AsReadOnly(anchors.ToArray()),
            BuildCoverage(
                dataMinimum,
                dataMaximum,
                requestedMinimum,
                requestedMaximum,
                "s",
                localization ?? LocalizationService.Current));
    }

    public static CoveragePreview BuildIntegrationCoverage(
        IReadOnlyList<IpceValue> ipce,
        IReadOnlyList<SpectrumPoint> spectrum,
        double requestedMinimumNm,
        double requestedMaximumNm,
        ILocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(ipce);
        ArgumentNullException.ThrowIfNull(spectrum);
        if (ipce.Count == 0 || spectrum.Count == 0)
        {
            throw new IpceException(
                "IPCE:InvalidPreview",
                "IPCE 与光谱必须包含数据才能预览覆盖范围。");
        }

        double dataMinimum = Math.Max(
            ipce.Min(point => point.WavelengthNm),
            spectrum.Min(point => point.WavelengthNm));
        double dataMaximum = Math.Min(
            ipce.Max(point => point.WavelengthNm),
            spectrum.Max(point => point.WavelengthNm));
        if (dataMaximum <= dataMinimum)
        {
            throw new IpceException(
                "IPCE:InvalidPreview",
                "IPCE 与光谱没有共同波长范围。");
        }

        return BuildCoverage(
            dataMinimum,
            dataMaximum,
            requestedMinimumNm,
            requestedMaximumNm,
            "nm",
            localization ?? LocalizationService.Current);
    }

    private static CoveragePreview BuildCoverage(
        double dataMinimum,
        double dataMaximum,
        double requestedMinimum,
        double requestedMaximum,
        string unit,
        ILocalizationService localization)
    {
        bool finite = double.IsFinite(dataMinimum) &&
            double.IsFinite(dataMaximum) &&
            double.IsFinite(requestedMinimum) &&
            double.IsFinite(requestedMaximum);
        if (!finite || dataMaximum <= dataMinimum ||
            requestedMaximum <= requestedMinimum)
        {
            throw new IpceException(
                "IPCE:InvalidPreview",
                "覆盖范围必须为有限且严格递增的数值。");
        }

        bool covered = requestedMinimum >= dataMinimum &&
            requestedMaximum <= dataMaximum;
        string prefix = localization.Format(
            "Coverage.Range",
            dataMinimum,
            dataMaximum,
            unit,
            requestedMinimum,
            requestedMaximum);
        string message;
        if (covered)
        {
            message = localization.Format("Coverage.Complete", prefix);
        }
        else
        {
            double left = Math.Max(0, dataMinimum - requestedMinimum);
            double right = Math.Max(0, requestedMaximum - dataMaximum);
            string exceeded = left > 0 && right > 0
                ? localization.Format(
                    "Coverage.ExceededBoth",
                    left,
                    unit,
                    right)
                : left > 0
                    ? localization.Format(
                        "Coverage.Exceeded",
                        left,
                        unit)
                    : localization.Format(
                        "Coverage.Exceeded",
                        right,
                        unit);
            message = localization.Format(
                "Coverage.Incomplete",
                prefix,
                exceeded);
        }

        return new CoveragePreview(
            dataMinimum,
            dataMaximum,
            requestedMinimum,
            requestedMaximum,
            covered,
            message);
    }
}
