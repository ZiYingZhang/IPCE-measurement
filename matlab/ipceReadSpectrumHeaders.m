function columns = ipceReadSpectrumHeaders(filePath, sheetName)
%IPCEREADSPECTRUMHEADERS Discover numeric spectrum columns and their headers.
%   The returned table contains one row per column with at least two numeric
%   values. DisplayName combines the Excel-style column letter and the last
%   text header found before the numeric data begin.

arguments
    filePath (1, 1) string
    sheetName (1, 1) string = "Spectra"
end

if ~isfile(filePath)
    error("IPCE:FileNotFound", "找不到光谱文件：%s", filePath);
end

[~, ~, extension] = fileparts(filePath);
isTextFile = any(strcmpi(extension, [".csv", ".txt"]));
try
    if isTextFile
        raw = readcell(filePath);
        sheetName = "";
    else
        availableSheets = sheetnames(filePath);
        matchedSheet = find(strcmpi(availableSheets, sheetName), 1);
        if isempty(matchedSheet)
            error("IPCE:SpectrumSheetNotFound", ...
                "工作簿中没有名为“%s”的表格。可用表格：%s", ...
                sheetName, strjoin(availableSheets, ", "));
        end
        sheetName = availableSheets(matchedSheet);
        raw = readcell(filePath, "Sheet", sheetName);
    end
catch exception
    if strcmp(exception.identifier, "IPCE:SpectrumSheetNotFound")
        rethrow(exception);
    end
    error("IPCE:SpectrumHeaderImportFailed", ...
        "无法读取光谱表头：%s", exception.message);
end

maximumColumnCount = size(raw, 2);
columnIndex = zeros(maximumColumnCount, 1);
header = strings(maximumColumnCount, 1);
displayName = strings(maximumColumnCount, 1);
numericValueCount = zeros(maximumColumnCount, 1);
validColumnCount = 0;

for index = 1:size(raw, 2)
    numericValues = cellColumnToDouble(raw(:, index));
    numericRows = find(isfinite(numericValues));
    if numel(numericRows) < 2
        continue
    end

    firstNumericRow = numericRows(1);
    headerText = "";
    if firstNumericRow > 1
        for row = firstNumericRow - 1:-1:1
            value = raw{row, index};
            if ischar(value) || isstring(value)
                candidate = strtrim(string(value));
                if candidate ~= "" && ~isfinite(str2double(candidate))
                    headerText = candidate;
                    break
                end
            end
        end
    end
    if headerText == ""
        headerText = "第 " + index + " 列";
    end

    validColumnCount = validColumnCount + 1;
    columnIndex(validColumnCount) = index;
    header(validColumnCount) = headerText;
    displayName(validColumnCount) = ...
        "[" + excelColumnName(index) + "] " + headerText;
    numericValueCount(validColumnCount) = numel(numericRows);
end

if validColumnCount == 0
    error("IPCE:NoNumericSpectrumColumns", ...
        "所选表格中没有至少包含两个数值的列。");
end
columnIndex = columnIndex(1:validColumnCount);
header = header(1:validColumnCount);
displayName = displayName(1:validColumnCount);
numericValueCount = numericValueCount(1:validColumnCount);

columns = table(columnIndex, header, displayName, numericValueCount, ...
    'VariableNames', {'ColumnIndex', 'Header', 'DisplayName', ...
    'NumericValueCount'});
columns.Properties.UserData.SheetName = char(sheetName);
end

function values = cellColumnToDouble(column)
values = nan(numel(column), 1);
for rowIndex = 1:numel(column)
    value = column{rowIndex};
    if (isnumeric(value) || islogical(value)) && isscalar(value)
        values(rowIndex) = double(value);
    elseif ischar(value) || isstring(value)
        values(rowIndex) = str2double(string(value));
    end
end
end

function name = excelColumnName(index)
name = "";
while index > 0
    remainder = mod(index - 1, 26);
    name = char(double('A') + remainder) + name;
    index = floor((index - 1) / 26);
end
end
