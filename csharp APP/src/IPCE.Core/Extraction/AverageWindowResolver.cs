using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Extraction;

public static class AverageWindowResolver
{
    public static (double Start, double End) Resolve(
        SchedulePoint point,
        double averagingDurationSeconds)
    {
        double duration =
            point.WindowEndSeconds - point.WindowStartSeconds;
        if (!double.IsFinite(averagingDurationSeconds) ||
            averagingDurationSeconds < 0 ||
            !double.IsFinite(duration) ||
            duration <= 0)
        {
            throw new IpceException(
                "IPCE:InvalidSchedule",
                "平均时长必须为有限的非负数，驻留窗口必须具有正时长。");
        }

        bool anchorBased = point.AlignmentSource != "fixed-delay";
        if (anchorBased)
        {
            double availableTime =
                point.WindowEndSeconds - point.ReferenceTimeSeconds;
            if (!double.IsFinite(availableTime) ||
                availableTime <= 0 ||
                point.ReferenceTimeSeconds < point.WindowStartSeconds)
            {
                throw new IpceException(
                    "IPCE:InvalidSchedule",
                    $"波长 {point.WavelengthNm:g6} nm 的确认时间不在驻留窗口内。");
            }

            double effectiveDuration = averagingDurationSeconds == 0
                ? availableTime
                : Math.Min(averagingDurationSeconds, availableTime);
            return (
                point.ReferenceTimeSeconds,
                point.ReferenceTimeSeconds + effectiveDuration);
        }

        double fixedDuration = averagingDurationSeconds == 0
            ? duration
            : Math.Min(averagingDurationSeconds, duration);
        return (
            point.WindowEndSeconds - fixedDuration,
            point.WindowEndSeconds);
    }
}
