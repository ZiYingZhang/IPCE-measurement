function anchors = ipceReadAnchors(filePath)
%IPCEREADANCHORS Read two-column wavelength-time anchor data.
%   The first numeric column is wavelength in nm and the second is the
%   confirmed time in seconds. Text headers and comma/tab delimiters are
%   allowed.

arguments
    filePath (1, 1) string
end

if ~isfile(filePath)
    error("IPCE:AnchorFileNotFound", "未找到锚点文件：%s", filePath);
end

try
    numericData = readmatrix(filePath);
catch exception
    error("IPCE:AnchorImportFailed", ...
        "无法读取锚点文件：%s", exception.message);
end

if size(numericData, 2) < 2
    error("IPCE:InvalidAnchorFile", ...
        "锚点文件至少需要两列：波长 (nm)、确认时间 (s)。");
end

anchors = double(numericData(:, 1:2));
anchors = anchors(all(isfinite(anchors), 2), :);
if isempty(anchors)
    error("IPCE:InvalidAnchorFile", ...
        "锚点文件前两列中没有有效的数值行。");
end
if any(anchors(:, 1) <= 0)
    error("IPCE:InvalidAnchorFile", "锚点波长必须大于 0 nm。");
end
if numel(unique(anchors(:, 1))) ~= size(anchors, 1)
    error("IPCE:InvalidAnchorFile", "锚点文件中存在重复波长。");
end
end
