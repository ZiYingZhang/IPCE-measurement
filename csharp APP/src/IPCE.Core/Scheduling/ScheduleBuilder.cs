using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Numerics;

namespace IPCE.Core.Scheduling;

public static class ScheduleBuilder
{
    public static IReadOnlyList<SchedulePoint> Build(
        IReadOnlyList<double> wavelengthsNm,
        AlignmentMode mode,
        IReadOnlyList<AnchorPoint> anchors,
        double fixedStartTimeSeconds,
        double nominalDelaySeconds)
    {
        ArgumentNullException.ThrowIfNull(wavelengthsNm);
        ArgumentNullException.ThrowIfNull(anchors);

        double[] wavelengths = wavelengthsNm.ToArray();
        if (wavelengths.Length == 0)
        {
            throw new IpceException(
                "IPCE:EmptyWavelengths",
                "波长序列不能为空。");
        }

        if (wavelengths.Any(value => !double.IsFinite(value) || value <= 0) ||
            !double.IsFinite(fixedStartTimeSeconds) ||
            !double.IsFinite(nominalDelaySeconds) ||
            nominalDelaySeconds <= 0)
        {
            throw new IpceException(
                "IPCE:InvalidSchedule",
                "波长、开始时间和标称延时必须为有效数值。");
        }

        double[] referenceTimes;
        string source;

        switch (mode)
        {
            case AlignmentMode.FixedDelay:
                referenceTimes = new double[wavelengths.Length];
                for (int index = 0; index < wavelengths.Length; index++)
                {
                    referenceTimes[index] =
                        fixedStartTimeSeconds + index * nominalDelaySeconds;
                }

                return BuildFixedSchedule(
                    wavelengths, referenceTimes, nominalDelaySeconds);

            case AlignmentMode.Anchors:
                (referenceTimes, source) = BuildAnchorReferences(
                    wavelengths, anchors, nominalDelaySeconds);
                return BuildAnchorSchedule(
                    wavelengths,
                    referenceTimes,
                    source,
                    nominalDelaySeconds);

            default:
                throw new IpceException(
                    "IPCE:UnknownAlignmentMode",
                    $"未知的时间对齐模式：{mode}");
        }
    }

    private static IReadOnlyList<SchedulePoint> BuildFixedSchedule(
        IReadOnlyList<double> wavelengths,
        IReadOnlyList<double> referenceTimes,
        double nominalDelaySeconds)
    {
        SchedulePoint[] schedule = new SchedulePoint[wavelengths.Count];
        for (int index = 0; index < schedule.Length; index++)
        {
            double windowStart = referenceTimes[index];
            schedule[index] = new SchedulePoint(
                wavelengths[index],
                referenceTimes[index],
                windowStart,
                windowStart + nominalDelaySeconds,
                "fixed-delay");
        }

        return Array.AsReadOnly(schedule);
    }

    private static (double[] ReferenceTimes, string Source)
        BuildAnchorReferences(
            IReadOnlyList<double> wavelengths,
            IReadOnlyList<AnchorPoint> anchors,
            double nominalDelaySeconds)
    {
        AnchorPoint[] validAnchors = anchors
            .Where(anchor =>
                double.IsFinite(anchor.WavelengthNm) &&
                double.IsFinite(anchor.ConfirmedTimeSeconds))
            .OrderBy(anchor => anchor.WavelengthNm)
            .ToArray();

        if (validAnchors.Length == 0)
        {
            throw new IpceException(
                "IPCE:MissingAnchors",
                "锚点模式至少需要一组有效的波长–时间数据。");
        }

        if (validAnchors.Any(anchor => anchor.WavelengthNm <= 0))
        {
            throw new IpceException(
                "IPCE:InvalidAnchorFile",
                "锚点波长必须大于 0 nm。");
        }

        if (validAnchors
            .Zip(validAnchors.Skip(1), (left, right) =>
                left.WavelengthNm == right.WavelengthNm)
            .Any(isDuplicate => isDuplicate))
        {
            throw new IpceException(
                "IPCE:DuplicateAnchors",
                "锚点波长不能重复。");
        }

        double[] referenceTimes;
        string source;
        if (validAnchors.Length == 1)
        {
            referenceTimes = BuildSingleAnchorReferences(
                wavelengths, validAnchors[0], nominalDelaySeconds);
            source = "single-anchor+nominal-delay";
        }
        else
        {
            double[] anchorWavelengths = validAnchors
                .Select(anchor => anchor.WavelengthNm)
                .ToArray();
            double[] anchorTimes = validAnchors
                .Select(anchor => anchor.ConfirmedTimeSeconds)
                .ToArray();
            referenceTimes = Interpolation.Linear(
                anchorWavelengths,
                anchorTimes,
                wavelengths.ToArray(),
                allowExtrapolation: true);
            source = "piecewise-anchor";
        }

        if (referenceTimes
            .Zip(referenceTimes.Skip(1), (left, right) => right - left)
            .Any(interval => !double.IsFinite(interval) || interval <= 0))
        {
            throw new IpceException(
                "IPCE:NonMonotonicSchedule",
                "锚点生成的时间不是沿扫描方向严格递增。");
        }

        return (referenceTimes, source);
    }

    private static double[] BuildSingleAnchorReferences(
        IReadOnlyList<double> wavelengths,
        AnchorPoint anchor,
        double nominalDelaySeconds)
    {
        if (wavelengths.Count == 1)
        {
            return [anchor.ConfirmedTimeSeconds];
        }

        (double Wavelength, double Index)[] ordered = wavelengths
            .Select((wavelength, index) => (wavelength, (double)index))
            .OrderBy(pair => pair.wavelength)
            .Select(pair => (pair.wavelength, pair.Item2))
            .ToArray();
        double[] sortedWavelengths = ordered
            .Select(pair => pair.Wavelength)
            .ToArray();
        double[] sortedIndices = ordered
            .Select(pair => pair.Index)
            .ToArray();
        double anchorIndex = Interpolation.Linear(
            sortedWavelengths,
            sortedIndices,
            [anchor.WavelengthNm],
            allowExtrapolation: true)[0];

        double[] references = new double[wavelengths.Count];
        for (int index = 0; index < references.Length; index++)
        {
            references[index] =
                anchor.ConfirmedTimeSeconds +
                (index - anchorIndex) * nominalDelaySeconds;
        }

        return references;
    }

    private static IReadOnlyList<SchedulePoint> BuildAnchorSchedule(
        IReadOnlyList<double> wavelengths,
        IReadOnlyList<double> referenceTimes,
        string source,
        double nominalDelaySeconds)
    {
        int pointCount = wavelengths.Count;
        double[] windowStarts = new double[pointCount];
        double[] windowEnds = new double[pointCount];

        if (pointCount == 1)
        {
            windowStarts[0] = referenceTimes[0] - nominalDelaySeconds / 2;
            windowEnds[0] = referenceTimes[0] + nominalDelaySeconds / 2;
        }
        else
        {
            double[] intervals = referenceTimes
                .Zip(referenceTimes.Skip(1), (left, right) => right - left)
                .ToArray();
            double firstDuration = Median(
                intervals.Take(Math.Min(5, intervals.Length)));
            double lastDuration = Median(intervals.Skip(
                Math.Max(
                    0,
                    intervals.Length - Math.Min(5, intervals.Length))));

            windowStarts[0] = referenceTimes[0] - firstDuration / 2;
            windowEnds[^1] = referenceTimes[^1] + lastDuration / 2;
            for (int index = 0; index < pointCount - 1; index++)
            {
                double midpoint =
                    (referenceTimes[index] + referenceTimes[index + 1]) / 2;
                windowEnds[index] = midpoint;
                windowStarts[index + 1] = midpoint;
            }
        }

        SchedulePoint[] schedule = new SchedulePoint[pointCount];
        for (int index = 0; index < pointCount; index++)
        {
            double duration = windowEnds[index] - windowStarts[index];
            if (!double.IsFinite(duration) || duration <= 0)
            {
                throw new IpceException(
                    "IPCE:InvalidSchedule",
                    "生成的波长驻留窗口包含非正时长。");
            }

            schedule[index] = new SchedulePoint(
                wavelengths[index],
                referenceTimes[index],
                windowStarts[index],
                windowEnds[index],
                source);
        }

        return Array.AsReadOnly(schedule);
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.Order().ToArray();
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2;
    }
}
