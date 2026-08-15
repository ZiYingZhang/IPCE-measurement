function schedule = ipceBuildSchedule(wavelengths, alignmentMode, ...
    anchors, fixedStartTime, nominalDelay)
%IPCEBUILDSCHEDULE Build variable-duration wavelength dwell windows.
%   In "anchors" mode, anchor rows are [wavelength_nm, arrival_time_s].
%   Two or more anchors are connected piecewise-linearly and extrapolated
%   at both scan edges using the nearest anchor-pair slope. With one anchor,
%   nominalDelay is used to translate wavelength index into time.
%
%   Anchor time is a confirmed time at which the monochromator is already
%   outputting that wavelength. It is not assumed to be the exact switching
%   start. Dwell boundaries are placed halfway between consecutive
%   confirmed times.

arguments
    wavelengths (:, 1) double {mustBeFinite, mustBePositive}
    alignmentMode (1, 1) string
    anchors (:, 2) double = zeros(0, 2)
    fixedStartTime (1, 1) double {mustBeFinite} = 0
    nominalDelay (1, 1) double {mustBeFinite, mustBePositive} = 5
end

if isempty(wavelengths)
    error("IPCE:EmptyWavelengths", "波长序列不能为空。");
end

mode = lower(strtrim(alignmentMode));
pointCount = numel(wavelengths);

switch mode
    case {"fixed", "固定delay", "固定 delay"}
        windowStart = fixedStartTime + (0:pointCount - 1)' * nominalDelay;
        windowEnd = windowStart + nominalDelay;
        referenceTime = windowStart;
        source = repmat("fixed-delay", pointCount, 1);

    case {"anchors", "anchor", "锚点插值", "波长-时间锚点"}
        valid = all(isfinite(anchors), 2);
        anchors = anchors(valid, :);
        if isempty(anchors)
            error("IPCE:MissingAnchors", ...
                "锚点模式至少需要一组有效的“波长–时间”数据。");
        end

        [anchorWavelength, order] = sort(anchors(:, 1));
        anchorTime = anchors(order, 2);
        if any(diff(anchorWavelength) == 0)
            error("IPCE:DuplicateAnchors", "锚点波长不能重复。");
        end

        scanIndex = (0:pointCount - 1)';
        if numel(anchorWavelength) == 1
            [sortedWavelength, wavelengthOrder] = sort(wavelengths);
            sortedIndex = scanIndex(wavelengthOrder);
            anchorIndex = interp1(sortedWavelength, sortedIndex, ...
                anchorWavelength, "linear", "extrap");
            referenceTime = anchorTime + ...
                (scanIndex - anchorIndex) * nominalDelay;
            source = repmat("single-anchor+nominal-delay", pointCount, 1);
        else
            referenceTime = interp1(anchorWavelength, anchorTime, ...
                wavelengths, "linear", "extrap");
            source = repmat("piecewise-anchor", pointCount, 1);
        end

        if pointCount > 1
            intervals = diff(referenceTime);
            if any(~isfinite(intervals) | intervals <= 0)
                error("IPCE:NonMonotonicSchedule", ...
                    ["锚点生成的时间不是沿扫描方向严格递增。" ...
                    "请检查波长顺序和锚点时间。"]);
            end
            recentCount = min(5, numel(intervals));
            lastDuration = median(intervals(end - recentCount + 1:end));
            firstDuration = median(intervals(1:min(5, numel(intervals))));
            midpoints = (referenceTime(1:end - 1) + ...
                referenceTime(2:end)) / 2;
            windowStart = [referenceTime(1) - firstDuration / 2; midpoints];
            windowEnd = [midpoints; referenceTime(end) + lastDuration / 2];
        else
            firstDuration = nominalDelay;
            lastDuration = nominalDelay;
            windowStart = referenceTime - firstDuration / 2;
            windowEnd = referenceTime + lastDuration / 2;
        end

    otherwise
        error("IPCE:UnknownAlignmentMode", ...
            "未知的时间对齐模式：%s", alignmentMode);
end

windowDuration = windowEnd - windowStart;
if any(~isfinite(windowDuration) | windowDuration <= 0)
    error("IPCE:InvalidSchedule", "生成的波长驻留窗口包含非正时长。");
end

schedule = table(wavelengths, referenceTime, windowStart, windowEnd, ...
    windowDuration, source, ...
    'VariableNames', {'Wavelength_nm', 'ReferenceTime_s', ...
    'WindowStart_s', 'WindowEnd_s', 'WindowDuration_s', ...
    'AlignmentSource'});
schedule.Properties.UserData.AlignmentMode = char(mode);
schedule.Properties.UserData.AnchorPairs = anchors;
schedule.Properties.UserData.NominalDelay_s = nominalDelay;
end
