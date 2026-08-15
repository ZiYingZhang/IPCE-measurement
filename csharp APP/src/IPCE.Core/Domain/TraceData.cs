using IPCE.Core.Errors;

namespace IPCE.Core.Domain;

public sealed record TraceMetadata(
    string TimeHeader,
    string CurrentHeader,
    string OriginalTimeUnit,
    string OriginalCurrentUnit,
    double TimeToSecondsFactor,
    double CurrentToAmperesFactor,
    string RawHeaderText)
{
    public static TraceMetadata Unknown { get; } =
        new("", "", "", "", 1, 1, "");
}

public sealed record TraceData
{
    public TraceData(
        IReadOnlyList<double> timeSeconds,
        IReadOnlyList<double> currentAmperes,
        TraceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(timeSeconds);
        ArgumentNullException.ThrowIfNull(currentAmperes);
        ArgumentNullException.ThrowIfNull(metadata);

        double[] times = timeSeconds.ToArray();
        double[] currents = currentAmperes.ToArray();
        bool hasPositiveTimeIncrement = false;
        bool invalidTimeOrder = false;

        for (int index = 1; index < times.Length; index++)
        {
            double increment = times[index] - times[index - 1];
            invalidTimeOrder |= increment < 0;
            hasPositiveTimeIncrement |= increment > 0;
        }

        if (times.Length < 2 ||
            times.Length != currents.Length ||
            times.Any(value => !double.IsFinite(value)) ||
            currents.Any(value => !double.IsFinite(value)) ||
            invalidTimeOrder ||
            !hasPositiveTimeIncrement)
        {
            throw new IpceException(
                "IPCE:InvalidTrace",
                "i-t 数据必须包含至少两个有限、按时间非递减排列的数据点。");
        }

        TimeSeconds = Array.AsReadOnly(times);
        CurrentAmperes = Array.AsReadOnly(currents);
        Metadata = metadata;
    }

    public IReadOnlyList<double> TimeSeconds { get; }

    public IReadOnlyList<double> CurrentAmperes { get; }

    public TraceMetadata Metadata { get; }
}
