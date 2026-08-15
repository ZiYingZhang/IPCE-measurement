function trace = ipceReadIT(filePath, options)
%IPCEREADIT Read an i-t trace and normalize it to seconds/amperes.
%   Column-header units are detected for common CHI text, CSV, and
%   spreadsheet exports. If units are absent, pass TimeUnit and CurrentUnit
%   explicitly. Repeated displayed timestamps are retained because some CHI
%   exports reduce time precision while still writing valid current samples.

arguments
    filePath (1, 1) string
    options.TimeUnit (1, 1) string = ""
    options.CurrentUnit (1, 1) string = ""
end

if ~isfile(filePath)
    error("IPCE:FileNotFound", "找不到 i-t 文件：%s", filePath);
end

[~, ~, extension] = fileparts(filePath);
extension = lower(extension);
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
    [time, current, timeHeader, currentHeader, firstNumericRow] = ...
        firstTwoNumericCellColumns(raw);
    rawHeaderText = headerTextFromCells(raw, firstNumericRow);
catch cellException
    try
        matrix = readmatrix(filePath);
        [time, current] = firstTwoNumericColumns(matrix);
        [timeHeader, currentHeader] = textColumnHeaders(rawText);
        rawHeaderText = headerBeforeNumericData(rawText);
    catch matrixException
        try
            [time, current] = numericColumnsFromText(rawText);
            [timeHeader, currentHeader] = textColumnHeaders(rawText);
            rawHeaderText = headerBeforeNumericData(rawText);
        catch
            error("IPCE:TraceImportFailed", ...
                "无法读取 i-t 文件 %s。\n%s\n%s", filePath, ...
                cellException.message, matrixException.message);
        end
    end
end

if rawHeaderText == "" && rawText ~= ""
    rawHeaderText = headerBeforeNumericData(rawText);
end
if (timeHeader == "" || currentHeader == "") && rawText ~= ""
    [textTimeHeader, textCurrentHeader] = textColumnHeaders(rawText);
    if timeHeader == ""
        timeHeader = textTimeHeader;
    end
    if currentHeader == ""
        currentHeader = textCurrentHeader;
    end
end

if options.TimeUnit == ""
    timeUnit = detectTimeUnit(timeHeader);
else
    timeUnit = validateTimeUnit(options.TimeUnit);
end
if options.CurrentUnit == ""
    currentUnit = detectCurrentUnit(currentHeader);
else
    currentUnit = validateCurrentUnit(options.CurrentUnit);
end
if timeUnit == "" || currentUnit == ""
    error("IPCE:TraceUnitsRequired", ...
        "无法从 i-t 表头识别时间或电流单位。时间列“%s”，电流列“%s”。请在导入时明确选择单位。", ...
        timeHeader, currentHeader);
end

timeFactor = timeToSecondsFactor(timeUnit);
currentFactor = currentToAmperesFactor(currentUnit);
time = time * timeFactor;
current = current * currentFactor;

valid = isfinite(time) & isfinite(current);
time = time(valid);
current = current(valid);
if numel(time) < 2
    error("IPCE:InvalidTrace", "i-t 文件中有效的时间/电流数据少于两个点。");
end

[time, order] = sort(time);
current = current(order);
if any(diff(time) < 0)
    error("IPCE:InvalidTrace", "i-t 文件的时间列必须可整理为非递减序列。");
end

trace = table(time, current, ...
    'VariableNames', {'Time_s', 'Current_A'});
trace.Properties.Description = "Amperometric i-t trace (canonical s/A)";
trace.Properties.UserData.SourceFile = char(filePath);
trace.Properties.UserData.RawHeaderText = char(rawHeaderText);
trace.Properties.UserData.OriginalTimeHeader = char(timeHeader);
trace.Properties.UserData.OriginalCurrentHeader = char(currentHeader);
trace.Properties.UserData.OriginalTimeUnit = char(timeUnit);
trace.Properties.UserData.OriginalCurrentUnit = char(currentUnit);
trace.Properties.UserData.TimeToSecondsFactor = timeFactor;
trace.Properties.UserData.CurrentToAmperesFactor = currentFactor;
positiveIntervals = diff(time);
positiveIntervals = positiveIntervals(positiveIntervals > 0);
if isempty(positiveIntervals)
    trace.Properties.UserData.SampleInterval_s = NaN;
else
    trace.Properties.UserData.SampleInterval_s = median(positiveIntervals);
end
end

function [first, second, firstHeader, secondHeader, firstNumericRow] = ...
        firstTwoNumericCellColumns(raw)
if isempty(raw) || size(raw, 2) < 2
    error("IPCE:InvalidTrace", "i-t 文件至少需要两列数据。");
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
    error("IPCE:InvalidTrace", "未能识别出 i-t 文件的时间列和电流列。");
end

firstColumn = columns(1);
secondColumn = columns(2);
first = numericValues(:, firstColumn);
second = numericValues(:, secondColumn);
firstRows = find(isfinite(first));
secondRows = find(isfinite(second));
firstNumericRow = min([firstRows(1), secondRows(1)]);
firstHeader = nearestHeader(raw(:, firstColumn), firstRows(1));
secondHeader = nearestHeader(raw(:, secondColumn), secondRows(1));
end

function value = scalarCellToDouble(cellValue)
value = NaN;
if (isnumeric(cellValue) || islogical(cellValue)) && isscalar(cellValue)
    value = double(cellValue);
elseif ischar(cellValue) || isstring(cellValue)
    candidate = str2double(replace(string(cellValue), ["D", "d"], "e"));
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

function headerText = headerTextFromCells(raw, firstNumericRow)
parts = strings(0, 1);
for rowIndex = 1:max(firstNumericRow - 1, 0)
    for columnIndex = 1:size(raw, 2)
        value = raw{rowIndex, columnIndex};
        if ischar(value) || isstring(value)
            candidate = strtrim(string(value));
            if candidate ~= "" && ~isfinite(str2double(candidate))
                parts(end + 1, 1) = candidate; %#ok<AGROW>
            end
        end
    end
end
headerText = strjoin(parts, newline);
end

function [timeHeader, currentHeader] = textColumnHeaders(rawText)
timeHeader = "";
currentHeader = "";
if rawText == ""
    return
end
lines = splitlines(rawText);
for rowIndex = numel(lines):-1:1
    line = strtrim(lines(rowIndex));
    if ~contains(lower(line), "time") || ...
            ~contains(lower(line), "current")
        continue
    end
    parts = string(regexp(char(line), "[,;\t]", "split"));
    parts = strtrim(parts);
    if numel(parts) >= 2
        timeHeader = parts(1);
        currentHeader = parts(2);
        return
    end
end
end

function headerText = headerBeforeNumericData(rawText)
headerText = "";
if rawText == ""
    return
end
lines = splitlines(rawText);
number = "[-+]?(?:\d+\.?\d*|\.\d+)(?:[eEdD][-+]?\d+)?";
dataLine = "^\s*" + number + "\s*(?:[,;\t]|\s+)\s*" + ...
    number + "\s*$";
firstDataRow = find(~cellfun(@isempty, ...
    regexp(cellstr(lines), char(dataLine), "once")), 1);
if isempty(firstDataRow)
    headerLines = lines;
else
    headerLines = lines(1:firstDataRow - 1);
end
headerText = strjoin(headerLines, newline);
end

function [time, current] = numericColumnsFromText(rawText)
if rawText == ""
    error("IPCE:InvalidTrace", "文本文件为空。");
end
number = "[-+]?(?:\d+\.?\d*|\.\d+)(?:[eEdD][-+]?\d+)?";
expression = "(?m)^\s*(" + number + ...
    ")\s*(?:[,;\t]|\s+)\s*(" + number + ")\s*$";
tokens = regexp(char(rawText), char(expression), "tokens");
if numel(tokens) < 2
    error("IPCE:InvalidTrace", "文本文件中有效数据少于两个点。");
end
time = zeros(numel(tokens), 1);
current = zeros(numel(tokens), 1);
for rowIndex = 1:numel(tokens)
    time(rowIndex) = parseNumber(tokens{rowIndex}{1});
    current(rowIndex) = parseNumber(tokens{rowIndex}{2});
end
end

function value = parseNumber(textValue)
value = str2double(regexprep(textValue, "[dD]", "e"));
end

function [first, second] = firstTwoNumericColumns(matrix)
if isempty(matrix) || size(matrix, 2) < 2
    error("IPCE:InvalidTrace", "i-t 文件至少需要两列数值。");
end
counts = sum(isfinite(matrix), 1);
columns = find(counts >= 2);
if numel(columns) < 2
    error("IPCE:InvalidTrace", "未能识别出 i-t 文件的时间列和电流列。");
end
first = matrix(:, columns(1));
second = matrix(:, columns(2));
end

function unit = detectTimeUnit(headerText)
unit = "";
aliases = ["min", "ms", "second", "sec", "s", "h"];
for alias = aliases
    if containsUnitToken(headerText, alias)
        unit = alias;
        return
    end
end
end

function unit = detectCurrentUnit(headerText)
unit = "";
normalizedHeader = replace(headerText, ["µ", "μ"], "u");
aliases = ["pA", "nA", "uA", "mA", "A"];
for alias = aliases
    if containsUnitToken(normalizedHeader, alias)
        unit = alias;
        return
    end
end
end

function matched = containsUnitToken(textValue, unit)
if textValue == ""
    matched = false;
    return
end
escapedUnit = regexptranslate("escape", char(unit));
expression = "(?i)(^|[^A-Za-z])" + string(escapedUnit) + ...
    "($|[^A-Za-z])";
matched = ~isempty(regexp(char(textValue), char(expression), "once"));
end

function unit = validateTimeUnit(value)
value = strtrim(lower(value));
switch value
    case {"s", "sec", "second", "seconds"}
        unit = "s";
    case "ms"
        unit = "ms";
    case {"min", "minute", "minutes"}
        unit = "min";
    case {"h", "hr", "hour", "hours"}
        unit = "h";
    otherwise
        error("IPCE:UnsupportedTimeUnit", ...
            "不支持的时间单位：%s。", value);
end
end

function unit = validateCurrentUnit(value)
value = replace(strtrim(value), ["µ", "μ"], "u");
switch lower(value)
    case "a"
        unit = "A";
    case "ma"
        unit = "mA";
    case "ua"
        unit = "uA";
    case "na"
        unit = "nA";
    case "pa"
        unit = "pA";
    otherwise
        error("IPCE:UnsupportedCurrentUnit", ...
            "不支持的电流单位：%s。", value);
end
end

function factor = timeToSecondsFactor(unit)
switch lower(unit)
    case {"s", "sec", "second"}
        factor = 1;
    case "ms"
        factor = 1e-3;
    case "min"
        factor = 60;
    case "h"
        factor = 3600;
    otherwise
        error("IPCE:UnsupportedTimeUnit", ...
            "不支持的时间单位：%s。", unit);
end
end

function factor = currentToAmperesFactor(unit)
normalized = lower(replace(unit, ["µ", "μ"], "u"));
switch normalized
    case "a"
        factor = 1;
    case "ma"
        factor = 1e-3;
    case "ua"
        factor = 1e-6;
    case "na"
        factor = 1e-9;
    case "pa"
        factor = 1e-12;
    otherwise
        error("IPCE:UnsupportedCurrentUnit", ...
            "不支持的电流单位：%s。", unit);
end
end
