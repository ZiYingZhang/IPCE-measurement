function export_csharp_baseline
%EXPORT_CSHARP_BASELINE Export deterministic MATLAB results for C# tests.

toolDirectory = string(fileparts(mfilename("fullpath")));
csharpDirectory = string(fileparts(toolDirectory));
repositoryRoot = string(fileparts(csharpDirectory));
matlabRoot = fullfile(repositoryRoot, "matlab");
addpath(matlabRoot);
paths = ipceRepositoryPaths();
outputDirectory = fullfile(csharpDirectory, "tests", "TestData", "Golden");

originalDirectory = string(pwd);
restoreDirectory = onCleanup(@()cd(originalDirectory));
cd(matlabRoot);

run_ipce_selftest;

if ~isfolder(outputDirectory)
    mkdir(outputDirectory);
end

defaults = ipceDefaultConfig();
calibration = ipceReadReference(fullfile( ...
    paths.DefaultsRoot, defaults.CalibrationFile));
siliconTrace = ipceReadIT(fullfile( ...
    paths.DefaultsRoot, defaults.SiliconTraceFile));
anchors = ipceReadAnchors(fullfile( ...
    paths.DefaultsRoot, defaults.SiliconAnchorFile));
wavelengths = (300:5:1100)';
schedule = ipceBuildSchedule(wavelengths, "anchors", anchors, 50, 8);
siliconExtracted = ipceExtractSchedule(siliconTrace, schedule, 4, ...
    defaults.SubtractDark, defaults.SiliconDarkRange_s);
[defaultPowerDensity, ~] = ipceCalculate( ...
    calibration, siliconExtracted, table(), 0.36, 1);

[syntheticSampleIpce, integrationSummary, integrationCurve] = ...
    buildSyntheticBaselines();

baselineTables = struct( ...
    "Name", { ...
        "default_silicon_extracted.csv", ...
        "default_power_density.csv", ...
        "synthetic_sample_ipce.csv", ...
        "integration_summary.csv", ...
        "integration_curve.csv"}, ...
    "Data", { ...
        siliconExtracted, ...
        defaultPowerDensity, ...
        syntheticSampleIpce, ...
        integrationSummary, ...
        integrationCurve});

baselineManifest = repmat(struct( ...
    "name", "", "rows", 0, "sha256", ""), numel(baselineTables), 1);
for tableIndex = 1:numel(baselineTables)
    outputPath = fullfile(outputDirectory, baselineTables(tableIndex).Name);
    writePreciseCsv(baselineTables(tableIndex).Data, outputPath);
    baselineManifest(tableIndex).name = baselineTables(tableIndex).Name;
    baselineManifest(tableIndex).rows = height(baselineTables(tableIndex).Data);
    baselineManifest(tableIndex).sha256 = sha256File(outputPath);
end

[~, csharpFolderName] = fileparts(csharpDirectory);
sourcePaths = [
    fullfile("data", "defaults", defaults.CalibrationFile)
    fullfile("data", "defaults", defaults.SpectrumFile)
    fullfile("data", "defaults", defaults.SiliconTraceFile)
    fullfile("data", "defaults", defaults.SiliconAnchorFile)
    fullfile(csharpFolderName, "tools", "export_csharp_baseline.m")
    fullfile("matlab", "run_ipce_selftest.m")
];
sourceManifest = repmat(struct( ...
    "name", "", "sha256", ""), numel(sourcePaths), 1);
for sourceIndex = 1:numel(sourcePaths)
    sourceManifest(sourceIndex).name = sourcePaths(sourceIndex);
    sourceManifest(sourceIndex).sha256 = sha256File( ...
        fullfile(repositoryRoot, sourcePaths(sourceIndex)));
end

manifest = struct();
manifest.schemaVersion = 1;
manifest.generatedUtc = string(datetime("now", ...
    "TimeZone", "UTC", "Format", "yyyy-MM-dd'T'HH:mm:ss.SSSXXX"));
manifest.matlabRelease = string(version("-release"));
manifest.generator = fullfile( ...
    csharpFolderName, "tools", "export_csharp_baseline.m");
manifest.sources = sourceManifest;
manifest.baselines = baselineManifest;
manifest.parameters = struct( ...
    "defaultWavelengthStartNm", 300, ...
    "defaultWavelengthStepNm", 5, ...
    "defaultWavelengthEndNm", 1100, ...
    "siliconTailAverageSeconds", 4, ...
    "siliconAreaSquareCentimetres", 0.36, ...
    "syntheticExpectedIpcePercent", [20, 50, 80], ...
    "integrationMinimumWavelengthNm", 400, ...
    "integrationMaximumWavelengthNm", 600);

manifestPath = fullfile(outputDirectory, "manifest.json");
fileId = fopen(manifestPath, "w", "n", "UTF-8");
if fileId < 0
    error("IPCE:BaselineWriteFailed", ...
        "Cannot open baseline manifest for writing: %s", manifestPath);
end
closeManifest = onCleanup(@()fclose(fileId));
fwrite(fileId, jsonencode(manifest, PrettyPrint=true), "char");
clear closeManifest;

fprintf("C# baseline exported to %s\n", outputDirectory);
for tableIndex = 1:numel(baselineManifest)
    fprintf("  %s: %d rows, SHA-256 %s\n", ...
        baselineManifest(tableIndex).name, ...
        baselineManifest(tableIndex).rows, ...
        baselineManifest(tableIndex).sha256);
end
fprintf("  manifest.json: SHA-256 %s\n", sha256File(manifestPath));
end

function [sampleIpce, integrationSummary, integrationCurve] = ...
        buildSyntheticBaselines()
wavelength = [400; 500; 600];
responsivity = [0.20; 0.30; 0.40];
incidentPowerDensity = [10; 15; 12] * 1e-6;
siliconArea_cm2 = 0.36;
sampleArea_cm2 = 0.75;
expectedFraction = [0.20; 0.50; 0.80];
calibration = table(wavelength, responsivity, ...
    'VariableNames', {'Wavelength_nm', 'Responsivity_A_per_W'});

startTime = 2;
dwellTime = 1;
tailAverageTime = 0.4;
darkWindowTime = 1;
time = (0:0.01:5)';
siliconDark = -2e-9;
sampleDark = 3e-9;
siliconCurrent = siliconDark * ones(size(time));
sampleCurrent = sampleDark * ones(size(time));
constant_eV_nm = 1239.8419843320026;
siliconCollectedPower = incidentPowerDensity * siliconArea_cm2;
samplePhotocurrentDensity = incidentPowerDensity .* expectedFraction .* ...
    wavelength ./ constant_eV_nm;
samplePhotocurrent = samplePhotocurrentDensity * sampleArea_cm2;

for pointIndex = 1:numel(wavelength)
    mask = time >= startTime + (pointIndex - 1) * dwellTime & ...
        time < startTime + pointIndex * dwellTime;
    siliconCurrent(mask) = siliconDark - ...
        responsivity(pointIndex) * siliconCollectedPower(pointIndex);
    sampleCurrent(mask) = sampleDark + samplePhotocurrent(pointIndex);
end

siliconTrace = table(time, siliconCurrent, ...
    'VariableNames', {'Time_s', 'Current_A'});
sampleTrace = table(time, sampleCurrent, ...
    'VariableNames', {'Time_s', 'Current_A'});
siliconExtracted = ipceExtractScan(siliconTrace, wavelength, startTime, ...
    dwellTime, tailAverageTime, true, darkWindowTime);
sampleExtracted = ipceExtractScan(sampleTrace, wavelength, startTime, ...
    dwellTime, tailAverageTime, true, darkWindowTime);
[~, sampleIpce] = ipceCalculate(calibration, siliconExtracted, ...
    sampleExtracted, siliconArea_cm2, sampleArea_cm2);

syntheticSpectrum = table((400:25:600)', ones(9, 1), ...
    'VariableNames', {'Wavelength_nm', 'Irradiance_W_m2_nm'});
syntheticIpce = table(wavelength, 100 * ones(size(wavelength)), ...
    'VariableNames', {'Wavelength_nm', 'IPCE_percent'});
[integrationSummary, integrationCurve] = ipceIntegrateSpectrum( ...
    syntheticIpce, syntheticSpectrum, 400, 600);
end

function writePreciseCsv(data, outputPath)
fileId = fopen(outputPath, "w", "n", "UTF-8");
if fileId < 0
    error("IPCE:BaselineWriteFailed", ...
        "Cannot open baseline CSV for writing: %s", outputPath);
end
closeFile = onCleanup(@()fclose(fileId));

headers = string(data.Properties.VariableNames);
for columnIndex = 1:numel(headers)
    if columnIndex > 1
        fprintf(fileId, ",");
    end
    fprintf(fileId, "%s", escapeCsvText(headers(columnIndex)));
end
fprintf(fileId, "\n");

cells = table2cell(data);
for rowIndex = 1:size(cells, 1)
    for columnIndex = 1:size(cells, 2)
        if columnIndex > 1
            fprintf(fileId, ",");
        end
        value = cells{rowIndex, columnIndex};
        if isnumeric(value) && isscalar(value)
            fprintf(fileId, "%.17g", value);
        elseif islogical(value) && isscalar(value)
            fprintf(fileId, "%d", value);
        elseif ischar(value) || (isstring(value) && isscalar(value))
            fprintf(fileId, "%s", escapeCsvText(string(value)));
        else
            error("IPCE:BaselineWriteFailed", ...
                "Unsupported CSV value at row %d, column %d.", ...
                rowIndex, columnIndex);
        end
    end
    fprintf(fileId, "\n");
end
clear closeFile;
end

function escaped = escapeCsvText(value)
escaped = """" + replace(string(value), """", """""") + """";
end

function digest = sha256File(filePath)
fileId = fopen(filePath, "r");
if fileId < 0
    error("IPCE:BaselineHashFailed", ...
        "Cannot open file for hashing: %s", filePath);
end
closeFile = onCleanup(@()fclose(fileId));
bytes = fread(fileId, Inf, "*uint8");
clear closeFile;

messageDigest = java.security.MessageDigest.getInstance("SHA-256");
messageDigest.update(bytes);
digestBytes = typecast(messageDigest.digest(), "uint8");
digest = lower(string(reshape(dec2hex(digestBytes, 2).', 1, [])));
end
