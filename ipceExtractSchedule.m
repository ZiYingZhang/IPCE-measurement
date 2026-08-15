function extracted = ipceExtractSchedule(trace, schedule, ...
    tailAverageTime, subtractDark, darkTimeRange)
%IPCEEXTRACTSCHEDULE Extract steady current from variable dwell windows.
%   For anchor schedules, averaging begins at the confirmed reference time
%   (when the target wavelength is already present) and proceeds forward.
%   For fixed-delay schedules, the final tailAverageTime seconds are used.
%   darkTimeRange is normally [start_s, end_s]. A positive scalar is still
%   accepted for backward compatibility and selects that duration directly
%   before the first scheduled dwell window.

arguments
    trace table
    schedule table
    tailAverageTime (1, 1) double {mustBeFinite, mustBeNonnegative}
    subtractDark (1, 1) logical
    darkTimeRange (1, :) double {mustBeFinite}
end

traceVariables = ["Time_s", "Current_A"];
scheduleVariables = ["Wavelength_nm", "WindowStart_s", "WindowEnd_s"];
if ~all(ismember(traceVariables, string(trace.Properties.VariableNames)))
    error("IPCE:InvalidTrace", "trace 必须包含 Time_s 和 Current_A 两列。");
end
if ~all(ismember(scheduleVariables, string(schedule.Properties.VariableNames)))
    error("IPCE:InvalidSchedule", ...
        "schedule 必须包含波长、窗口开始和窗口结束时间。");
end

time = trace.Time_s(:);
current = trace.Current_A(:);
wavelengths = schedule.Wavelength_nm(:);
windowStartFull = schedule.WindowStart_s(:);
windowEnd = schedule.WindowEnd_s(:);
windowDuration = windowEnd - windowStartFull;
hasReferenceTime = ismember("ReferenceTime_s", ...
    string(schedule.Properties.VariableNames));
hasSource = ismember("AlignmentSource", ...
    string(schedule.Properties.VariableNames));

if any(~isfinite(windowDuration) | windowDuration <= 0)
    error("IPCE:InvalidSchedule", "时间调度中存在非正驻留窗口。");
end
if windowStartFull(1) < time(1) || ...
        windowEnd(end) > time(end) + 10 * eps(max(abs(time(end)), 1))
    error("IPCE:InsufficientCoverage", ...
        "调度需要 %.3f–%.3f s，但数据仅覆盖 %.3f–%.3f s。", ...
        windowStartFull(1), windowEnd(end), time(1), time(end));
end

darkMean = 0;
darkStandardError = 0;
darkSampleCount = 0;
darkWindowStart = NaN;
darkWindowEnd = NaN;
if subtractDark
    scalarDurationMode = isscalar(darkTimeRange);
    if scalarDurationMode
        if darkTimeRange <= 0
            error("IPCE:InvalidDarkRange", "暗电流窗口时长必须大于 0。");
        end
        darkWindowStart = windowStartFull(1) - darkTimeRange;
        darkWindowEnd = windowStartFull(1);
    elseif numel(darkTimeRange) == 2
        darkWindowStart = darkTimeRange(1);
        darkWindowEnd = darkTimeRange(2);
        if darkWindowEnd <= darkWindowStart
            error("IPCE:InvalidDarkRange", ...
                "暗电流区间终点必须晚于起点。请在界面输入或图上选择有效区间。");
        end
    else
        error("IPCE:InvalidDarkRange", ...
            "暗电流区间必须是 [起始时间, 终止时间]。");
    end
    if darkWindowStart < time(1) || darkWindowEnd > time(end)
        error("IPCE:DarkRangeOutsideTrace", ...
            "暗电流区间 %.4f–%.4f s 超出 i-t 数据范围 %.4f–%.4f s。", ...
            darkWindowStart, darkWindowEnd, time(1), time(end));
    end
    if scalarDurationMode
        darkMask = time >= darkWindowStart & time < darkWindowEnd;
    else
        darkMask = time >= darkWindowStart & time <= darkWindowEnd;
    end
    darkSampleCount = nnz(darkMask);
    if darkSampleCount < 2
        error("IPCE:InsufficientDarkData", ...
            "暗电流区间 %.4f–%.4f s 内没有足够的数据点。", ...
            darkWindowStart, darkWindowEnd);
    end
    darkMean = mean(current(darkMask), "omitnan");
    darkStandardError = std(current(darkMask), 0, "omitnan") / ...
        sqrt(darkSampleCount);
end

pointCount = height(schedule);
averageWindowStart = zeros(pointCount, 1);
sampleWindowEnd = zeros(pointCount, 1);
meanTime = zeros(pointCount, 1);
meanCurrent = zeros(pointCount, 1);
currentStandardDeviation = zeros(pointCount, 1);
photoCurrent = zeros(pointCount, 1);
photoCurrentStandardError = zeros(pointCount, 1);
sampleCount = zeros(pointCount, 1);

for pointIndex = 1:pointCount
    anchorBased = false;
    if hasReferenceTime && hasSource
        anchorBased = schedule.AlignmentSource(pointIndex) ~= "fixed-delay";
    end

    if anchorBased
        confirmedTime = schedule.ReferenceTime_s(pointIndex);
        availableTime = windowEnd(pointIndex) - confirmedTime;
        if availableTime <= 0
            error("IPCE:InvalidSchedule", ...
                "波长 %.6g nm 的确认时间不在驻留窗口内。", ...
                wavelengths(pointIndex));
        end
        if tailAverageTime == 0
            effectiveAverageTime = availableTime;
        else
            effectiveAverageTime = min(tailAverageTime, availableTime);
        end
        averageWindowStart(pointIndex) = confirmedTime;
        averageWindowEnd = confirmedTime + effectiveAverageTime;
    else
        if tailAverageTime == 0
            effectiveAverageTime = windowDuration(pointIndex);
        else
            effectiveAverageTime = min(tailAverageTime, windowDuration(pointIndex));
        end
        averageWindowStart(pointIndex) = ...
            windowEnd(pointIndex) - effectiveAverageTime;
        averageWindowEnd = windowEnd(pointIndex);
    end
    mask = time >= averageWindowStart(pointIndex) & ...
        time < averageWindowEnd;
    sampleWindowEnd(pointIndex) = averageWindowEnd;

    sampleCount(pointIndex) = nnz(mask);
    if sampleCount(pointIndex) < 1
        error("IPCE:EmptyWindow", ...
            "波长 %.6g nm 的平均窗口内没有数据点。", ...
            wavelengths(pointIndex));
    end

    meanTime(pointIndex) = mean(time(mask));
    meanCurrent(pointIndex) = mean(current(mask), "omitnan");
    currentStandardDeviation(pointIndex) = std(current(mask), 0, "omitnan");
    measurementStandardError = currentStandardDeviation(pointIndex) / ...
        sqrt(sampleCount(pointIndex));
    photoCurrent(pointIndex) = meanCurrent(pointIndex) - darkMean;
    photoCurrentStandardError(pointIndex) = hypot( ...
        measurementStandardError, darkStandardError);
end

extracted = table(wavelengths, averageWindowStart, sampleWindowEnd, ...
    windowStartFull, windowDuration, meanTime, meanCurrent, ...
    currentStandardDeviation, photoCurrent, abs(photoCurrent), ...
    photoCurrentStandardError, sampleCount, ...
    'VariableNames', {'Wavelength_nm', 'WindowStart_s', 'WindowEnd_s', ...
    'DwellStart_s', 'DwellDuration_s', 'MeanTime_s', 'MeanCurrent_A', ...
    'CurrentStd_A', 'PhotoCurrent_A', 'AbsPhotoCurrent_A', ...
    'PhotoCurrentSE_A', 'SampleCount'});
extracted.Properties.UserData.DarkCurrent_A = darkMean;
extracted.Properties.UserData.DarkSampleCount = darkSampleCount;
extracted.Properties.UserData.DarkWindowStart_s = darkWindowStart;
extracted.Properties.UserData.DarkWindowEnd_s = darkWindowEnd;
extracted.Properties.UserData.TailAverageTime_s = tailAverageTime;
end
