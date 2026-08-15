function spectrum = ipceReadSpectrum(filePath, sheetName, ...
    wavelengthColumn, irradianceColumn)
%IPCEREADSPECTRUM Read wavelength and spectral irradiance from Excel.
%   Irradiance is expected in W m^-2 nm^-1. Column indices are one-based.

arguments
    filePath (1, 1) string
    sheetName (1, 1) string = "Spectra"
    wavelengthColumn (1, 1) double {mustBeInteger, mustBePositive} = 1
    irradianceColumn (1, 1) double {mustBeInteger, mustBePositive} = 3
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
    error("IPCE:SpectrumImportFailed", ...
        "无法读取光谱表格“%s”。\n%s", sheetName, exception.message);
end

requiredColumn = max(wavelengthColumn, irradianceColumn);
if size(raw, 2) < requiredColumn
    error("IPCE:SpectrumColumnMissing", ...
        "表格只有 %d 列，无法读取第 %d 列。", size(raw, 2), requiredColumn);
end

wavelength = cellColumnToDouble(raw(:, wavelengthColumn));
irradiance = cellColumnToDouble(raw(:, irradianceColumn));
valid = isfinite(wavelength) & isfinite(irradiance) & ...
    wavelength > 0 & irradiance >= 0;
wavelength = wavelength(valid);
irradiance = irradiance(valid);

if numel(wavelength) < 2
    error("IPCE:InvalidSpectrum", ...
        "所选列中有效的波长/光谱辐照度数据少于两个点。");
end

[wavelength, order] = sort(wavelength);
irradiance = irradiance(order);
[uniqueWavelength, ~, group] = unique(wavelength);
if numel(uniqueWavelength) < numel(wavelength)
    irradiance = accumarray(group, irradiance, [], @mean);
    wavelength = uniqueWavelength;
end

spectrum = table(wavelength, irradiance, ...
    'VariableNames', {'Wavelength_nm', 'Irradiance_W_m2_nm'});
spectrum.Properties.Description = "Spectral irradiance";
spectrum.Properties.UserData.SourceFile = char(filePath);
spectrum.Properties.UserData.SheetName = char(sheetName);
spectrum.Properties.UserData.WavelengthColumn = wavelengthColumn;
spectrum.Properties.UserData.IrradianceColumn = irradianceColumn;
end

function values = cellColumnToDouble(column)
values = nan(numel(column), 1);
for rowIndex = 1:numel(column)
    value = column{rowIndex};
    if isnumeric(value) || islogical(value)
        if isscalar(value)
            values(rowIndex) = double(value);
        end
    elseif ischar(value) || isstring(value)
        values(rowIndex) = str2double(string(value));
    end
end
end
