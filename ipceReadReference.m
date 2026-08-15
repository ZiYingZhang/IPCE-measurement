function calibration = ipceReadReference(filePath)
%IPCEREADREFERENCE Read wavelength/responsivity data from a spreadsheet.
%   calibration = ipceReadReference(filePath) returns a table with the
%   variables Wavelength_nm and Responsivity_A_per_W. Column names are
%   detected from common Chinese/English labels; otherwise the first two
%   sufficiently numeric columns are used.

arguments
    filePath (1, 1) string
end

if ~isfile(filePath)
    error("IPCE:FileNotFound", "找不到标探校准文件：%s", filePath);
end

try
    source = readtable(filePath, "VariableNamingRule", "preserve");
catch exception
    error("IPCE:ReferenceImportFailed", ...
        "无法读取标探校准文件 %s。\n%s", filePath, exception.message);
end

if height(source) < 2 || width(source) < 2
    error("IPCE:InvalidReference", "标探校准文件至少需要两列、两个数据点。");
end

numericData = nan(height(source), width(source));
numericCounts = zeros(1, width(source));
for columnIndex = 1:width(source)
    values = source{:, columnIndex};
    if isnumeric(values) || islogical(values)
        converted = double(values);
    else
        converted = str2double(string(values));
    end

    if ~isvector(converted)
        continue
    end

    converted = converted(:);
    numericData(:, columnIndex) = converted;
    numericCounts(columnIndex) = nnz(isfinite(converted));
end

minimumNumericRows = max(2, ceil(0.5 * height(source)));
candidateColumns = find(numericCounts >= minimumNumericRows);
if numel(candidateColumns) < 2
    error("IPCE:InvalidReference", ...
        "未能在标探校准文件中识别出波长和响应度两列数值。");
end

names = lower(string(source.Properties.VariableNames));
wavelengthColumn = find(contains(names, ["波长", "wavelength", "lambda", "nm"]), 1);
responsivityColumn = find(contains(names, ...
    ["响应度", "responsivity", "response", "a/w", "a per w"]), 1);

if isempty(wavelengthColumn) || ~ismember(wavelengthColumn, candidateColumns)
    wavelengthColumn = candidateColumns(1);
end
if isempty(responsivityColumn) || ...
        ~ismember(responsivityColumn, candidateColumns) || ...
        responsivityColumn == wavelengthColumn
    remaining = candidateColumns(candidateColumns ~= wavelengthColumn);
    responsivityColumn = remaining(1);
end

wavelength = numericData(:, wavelengthColumn);
responsivity = numericData(:, responsivityColumn);
valid = isfinite(wavelength) & isfinite(responsivity) & ...
    wavelength > 0 & responsivity > 0;

if nnz(valid) < 2
    error("IPCE:InvalidReference", ...
        "标探校准文件中的有效正波长/正响应度数据少于两个点。");
end

wavelength = wavelength(valid);
responsivity = responsivity(valid);
[wavelength, order] = sort(wavelength);
responsivity = responsivity(order);

[uniqueWavelength, ~, group] = unique(wavelength);
if numel(uniqueWavelength) < numel(wavelength)
    responsivity = accumarray(group, responsivity, [], @mean);
    wavelength = uniqueWavelength;
end

calibration = table(wavelength, responsivity, ...
    'VariableNames', {'Wavelength_nm', 'Responsivity_A_per_W'});
calibration.Properties.Description = "Silicon reference detector calibration";
calibration.Properties.UserData.SourceFile = char(filePath);
end
