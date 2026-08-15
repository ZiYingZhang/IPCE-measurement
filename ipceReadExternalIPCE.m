function ipce = ipceReadExternalIPCE(filePath)
%IPCEREADEXTERNALIPCE Read standalone wavelength/IPCE data.
%   The first two columns containing numeric data are interpreted as
%   wavelength in nm and IPCE in percent. Text headers are retained,
%   wavelengths are sorted, and duplicate wavelengths are averaged.

arguments
    filePath (1, 1) string
end

if ~isfile(filePath)
    error("IPCE:FileNotFound", "找不到外部 IPCE 文件：%s", filePath);
end

[~, ~, extension] = fileparts(filePath);
extension = lower(extension);
if ~any(extension == [".txt", ".csv", ".xls", ".xlsx"])
    error("IPCE:UnsupportedExternalIPCE", ...
        "外部 IPCE 仅支持 TXT、CSV、XLS 和 XLSX 文件。");
end

rawText = "";
if any(extension == [".txt", ".csv"])
    try
        rawText = string(fileread(filePath));
    catch
        rawText = "";
    end
end

try
    raw = readcell(filePath);
    [wavelength, ipcePercent, wavelengthHeader, ipceHeader] = ...
        firstTwoNumericCellColumns(raw);
catch cellException
    try
        matrix = readmatrix(filePath);
        [wavelength, ipcePercent] = firstTwoNumericColumns(matrix);
        [wavelengthHeader, ipceHeader] = textColumnHeaders(rawText);
    catch matrixException
        error("IPCE:ExternalIPCEImportFailed", ...
            "无法读取外部 IPCE 文件 %s。\n%s\n%s", ...
            filePath, cellException.message, matrixException.message);
    end
end

if (wavelengthHeader == "" || ipceHeader == "") && rawText ~= ""
    [textWavelengthHeader, textIPCEHeader] = textColumnHeaders(rawText);
    if wavelengthHeader == ""
        wavelengthHeader = textWavelengthHeader;
    end
    if ipceHeader == ""
        ipceHeader = textIPCEHeader;
    end
end

valid = isfinite(wavelength) & isfinite(ipcePercent) & wavelength > 0;
wavelength = wavelength(valid);
ipcePercent = ipcePercent(valid);
if numel(wavelength) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "外部 IPCE 文件中的有效波长/IPCE 数据少于两个点。");
end

[wavelength, order] = sort(wavelength);
ipcePercent = ipcePercent(order);
[uniqueWavelength, ~, group] = unique(wavelength);
if numel(uniqueWavelength) < numel(wavelength)
    ipcePercent = accumarray(group, ipcePercent, [], @mean);
    wavelength = uniqueWavelength;
end
if numel(wavelength) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "外部 IPCE 文件至少需要两个不同的波长。");
end

ipce = table(wavelength, ipcePercent, ...
    'VariableNames', {'Wavelength_nm', 'IPCE_percent'});
ipce.Properties.Description = "Externally measured IPCE";
ipce.Properties.UserData.SourceFile = char(filePath);
ipce.Properties.UserData.WavelengthHeader = char(wavelengthHeader);
ipce.Properties.UserData.IPCEHeader = char(ipceHeader);
ipce.Properties.UserData.WavelengthUnit = "nm";
ipce.Properties.UserData.IPCEUnit = "%";
end

function [first, second, firstHeader, secondHeader] = ...
        firstTwoNumericCellColumns(raw)
if isempty(raw) || size(raw, 2) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "外部 IPCE 文件至少需要两列数据。");
end

numericValues = nan(size(raw));
for columnIndex = 1:size(raw, 2)
    for rowIndex = 1:size(raw, 1)
        numericValues(rowIndex, columnIndex) = ...
            scalarCellToDouble(raw{rowIndex, columnIndex});
    end
end
counts = sum(isfinite(numericValues), 1);
columns = find(counts >= 2);
if numel(columns) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "未能识别两列有效的波长/IPCE 数值。");
end

firstColumn = columns(1);
secondColumn = columns(2);
first = numericValues(:, firstColumn);
second = numericValues(:, secondColumn);
firstRows = find(isfinite(first));
secondRows = find(isfinite(second));
firstHeader = nearestHeader(raw(:, firstColumn), firstRows(1));
secondHeader = nearestHeader(raw(:, secondColumn), secondRows(1));
end

function value = scalarCellToDouble(cellValue)
value = NaN;
if (isnumeric(cellValue) || islogical(cellValue)) && isscalar(cellValue)
    value = double(cellValue);
elseif ischar(cellValue) || isstring(cellValue)
    candidate = str2double(string(cellValue));
    if isfinite(candidate)
        value = candidate;
    end
end
end

function header = nearestHeader(column, firstNumericRow)
header = "";
for rowIndex = firstNumericRow - 1:-1:1
    value = column{rowIndex};
    if ischar(value) || isstring(value)
        candidate = strtrim(string(value));
        if candidate ~= "" && ~isfinite(str2double(candidate))
            header = candidate;
            return
        end
    end
end
end

function [first, second] = firstTwoNumericColumns(matrix)
if isempty(matrix) || size(matrix, 2) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "外部 IPCE 文件至少需要两列数值。");
end
counts = sum(isfinite(matrix), 1);
columns = find(counts >= 2);
if numel(columns) < 2
    error("IPCE:InvalidExternalIPCE", ...
        "未能识别两列有效的波长/IPCE 数值。");
end
first = matrix(:, columns(1));
second = matrix(:, columns(2));
end

function [wavelengthHeader, ipceHeader] = textColumnHeaders(rawText)
wavelengthHeader = "";
ipceHeader = "";
if rawText == ""
    return
end
lines = splitlines(rawText);
number = "[-+]?(?:\d+\.?\d*|\.\d+)(?:[eEdD][-+]?\d+)?";
for rowIndex = 1:numel(lines)
    line = strtrim(lines(rowIndex));
    if line == "" || ~isempty(regexp(char(line), ...
            char("^\s*" + number), "once"))
        continue
    end
    parts = string(regexp(char(line), "[,;\t]", "split"));
    parts = strtrim(parts);
    if numel(parts) >= 2
        wavelengthHeader = parts(1);
        ipceHeader = parts(2);
    end
end
end
