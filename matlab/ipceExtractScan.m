function extracted = ipceExtractScan(trace, wavelengths, startTime, ...
    dwellTime, tailAverageTime, subtractDark, darkWindowTime)
%IPCEEXTRACTSCAN Extract one steady-state current value per wavelength.
%   Compatibility wrapper for a strictly fixed-delay schedule.

arguments
    trace table
    wavelengths (:, 1) double {mustBeFinite, mustBePositive}
    startTime (1, 1) double {mustBeFinite}
    dwellTime (1, 1) double {mustBeFinite, mustBePositive}
    tailAverageTime (1, 1) double {mustBeFinite, mustBeNonnegative}
    subtractDark (1, 1) logical
    darkWindowTime (1, 1) double {mustBeFinite, mustBePositive}
end

pointCount = numel(wavelengths);
windowStart = startTime + (0:pointCount - 1)' * dwellTime;
windowEnd = windowStart + dwellTime;
windowDuration = repmat(dwellTime, pointCount, 1);
source = repmat("fixed-delay", pointCount, 1);
schedule = table(wavelengths, windowStart, windowEnd, windowDuration, source, ...
    'VariableNames', {'Wavelength_nm', 'WindowStart_s', 'WindowEnd_s', ...
    'WindowDuration_s', 'AlignmentSource'});
extracted = ipceExtractSchedule(trace, schedule, tailAverageTime, ...
    subtractDark, darkWindowTime);
extracted.Properties.UserData.DwellTime_s = dwellTime;
end
