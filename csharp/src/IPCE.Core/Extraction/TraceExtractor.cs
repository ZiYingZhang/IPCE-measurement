using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Extraction;

public static class TraceExtractor
{
    public static IReadOnlyList<ExtractedPoint> Extract(
        TraceData trace,
        IReadOnlyList<SchedulePoint> schedule,
        double averagingDurationSeconds,
        DarkCorrection darkCorrection)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(schedule);

        if (schedule.Count == 0 ||
            !double.IsFinite(averagingDurationSeconds) ||
            averagingDurationSeconds < 0)
        {
            throw new IpceException(
                "IPCE:InvalidSchedule",
                "调度不能为空，平均时长必须为有限的非负数。");
        }

        foreach (SchedulePoint point in schedule)
        {
            double duration = point.WindowEndSeconds - point.WindowStartSeconds;
            if (!double.IsFinite(duration) || duration <= 0)
            {
                throw new IpceException(
                    "IPCE:InvalidSchedule",
                    "时间调度中存在非正驻留窗口。");
            }
        }

        double traceStart = trace.TimeSeconds[0];
        double traceEnd = trace.TimeSeconds[^1];
        double endTolerance =
            10 * (double.BitIncrement(Math.Max(Math.Abs(traceEnd), 1)) -
                Math.Max(Math.Abs(traceEnd), 1));
        if (schedule[0].WindowStartSeconds < traceStart ||
            schedule[^1].WindowEndSeconds > traceEnd + endTolerance)
        {
            throw new IpceException(
                "IPCE:InsufficientCoverage",
                "时间调度超出 i-t 数据覆盖范围。");
        }

        (double darkMean, double darkStandardError) =
            CalculateDarkCorrection(trace, darkCorrection);

        ExtractedPoint[] result = new ExtractedPoint[schedule.Count];
        for (int pointIndex = 0; pointIndex < schedule.Count; pointIndex++)
        {
            SchedulePoint point = schedule[pointIndex];
            (double averageStart, double averageEnd) =
                AverageWindowResolver.Resolve(
                    point,
                    averagingDurationSeconds);

            List<double> samples = [];
            for (int traceIndex = 0;
                traceIndex < trace.TimeSeconds.Count;
                traceIndex++)
            {
                double time = trace.TimeSeconds[traceIndex];
                if (time >= averageStart && time < averageEnd)
                {
                    samples.Add(trace.CurrentAmperes[traceIndex]);
                }
            }

            if (samples.Count == 0)
            {
                throw new IpceException(
                    "IPCE:EmptyWindow",
                    $"波长 {point.WavelengthNm:g6} nm 的平均窗口内没有数据点。");
            }

            double meanCurrent = Mean(samples);
            double measurementStandardError =
                SampleStandardDeviation(samples, meanCurrent) /
                Math.Sqrt(samples.Count);
            double photoCurrent = meanCurrent - darkMean;
            double photoCurrentStandardError = Hypot(
                measurementStandardError, darkStandardError);

            result[pointIndex] = new ExtractedPoint(
                point.WavelengthNm,
                meanCurrent,
                photoCurrent,
                Math.Abs(photoCurrent),
                photoCurrentStandardError,
                samples.Count);
        }

        return Array.AsReadOnly(result);
    }

    private static (double Mean, double StandardError)
        CalculateDarkCorrection(
            TraceData trace,
            DarkCorrection correction)
    {
        if (!correction.Enabled)
        {
            return (0, 0);
        }

        if (!double.IsFinite(correction.StartSeconds) ||
            !double.IsFinite(correction.EndSeconds) ||
            correction.EndSeconds <= correction.StartSeconds)
        {
            throw new IpceException(
                "IPCE:InvalidDarkRange",
                "暗电流区间终点必须晚于起点。");
        }

        if (correction.StartSeconds < trace.TimeSeconds[0] ||
            correction.EndSeconds > trace.TimeSeconds[^1])
        {
            throw new IpceException(
                "IPCE:DarkRangeOutsideTrace",
                "暗电流区间超出 i-t 数据范围。");
        }

        List<double> samples = [];
        for (int index = 0; index < trace.TimeSeconds.Count; index++)
        {
            double time = trace.TimeSeconds[index];
            if (time >= correction.StartSeconds &&
                time <= correction.EndSeconds)
            {
                samples.Add(trace.CurrentAmperes[index]);
            }
        }

        if (samples.Count < 2)
        {
            throw new IpceException(
                "IPCE:InsufficientDarkData",
                "暗电流区间内没有足够的数据点。");
        }

        double mean = Mean(samples);
        double standardError =
            SampleStandardDeviation(samples, mean) / Math.Sqrt(samples.Count);
        return (mean, standardError);
    }

    private static double Mean(IReadOnlyList<double> values)
    {
        double sum = 0;
        for (int index = 0; index < values.Count; index++)
        {
            sum += values[index];
        }

        return sum / values.Count;
    }

    private static double SampleStandardDeviation(
        IReadOnlyList<double> values,
        double mean)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double sumOfSquares = 0;
        for (int index = 0; index < values.Count; index++)
        {
            double difference = values[index] - mean;
            sumOfSquares += difference * difference;
        }

        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private static double Hypot(double left, double right)
    {
        double maximum = Math.Max(Math.Abs(left), Math.Abs(right));
        if (maximum == 0)
        {
            return 0;
        }

        double scaledLeft = left / maximum;
        double scaledRight = right / maximum;
        return maximum * Math.Sqrt(
            scaledLeft * scaledLeft + scaledRight * scaledRight);
    }
}
