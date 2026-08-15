function run_ipce_selftest
%RUN_IPCE_SELFTEST Verify import, window extraction, and IPCE calculations.

fprintf("Running IPCE self-test...\n");

paths = ipceRepositoryPaths();
assert(isfolder(paths.MatlabRoot));
assert(isfolder(paths.DefaultsRoot));
assert(isfolder(paths.ExamplesRoot));
assert(string(fileparts(mfilename("fullpath"))) == paths.MatlabRoot);

% 1) Verify the files supplied with this workspace.
defaults = ipceDefaultConfig();
defaultPaths = fullfile(paths.DefaultsRoot, [ ...
    defaults.CalibrationFile; ...
    defaults.SpectrumFile; ...
    defaults.SiliconTraceFile; ...
    defaults.SiliconAnchorFile]);
assert(all(isfile(defaultPaths)));

importedCalibration = ipceReadReference(defaultPaths(1));
assert(height(importedCalibration) >= 2);
assert(all(importedCalibration.Responsivity_A_per_W > 0));
fprintf("  Calibration import: %d points, %.6g-%.6g nm\n", ...
    height(importedCalibration), ...
    min(importedCalibration.Wavelength_nm), ...
    max(importedCalibration.Wavelength_nm));

importedTrace = ipceReadIT(defaultPaths(3));
assert(height(importedTrace) >= 2);
assert(all(diff(importedTrace.Time_s) >= 0));
assert(any(diff(importedTrace.Time_s) > 0));
fprintf("  i-t import: %d points, %.4g-%.4g s\n", ...
    height(importedTrace), importedTrace.Time_s(1), importedTrace.Time_s(end));

% 2) Verify i-t header units are converted to canonical seconds/amperes.
unitTracePath = fullfile(pwd, "IPCE_units_selftest.txt");
cleanupUnitTrace = onCleanup(@()deleteIfPresent(unitTracePath));
writelines(["Time/ms, Current/mA"; "0, 1"; "1000, 2"], unitTracePath);
unitTrace = ipceReadIT(string(unitTracePath));
assert(isequal(unitTrace.Time_s, [0; 1]));
assert(max(abs(unitTrace.Current_A - [1e-3; 2e-3])) < 1e-15);
assert(string(unitTrace.Properties.UserData.OriginalTimeUnit) == "ms");
assert(string(unitTrace.Properties.UserData.OriginalCurrentUnit) == "mA");
assert(contains(string(unitTrace.Properties.UserData.RawHeaderText), ...
    "Time/ms"));

writelines(["time, current"; "0, 1"; "1, 2"], unitTracePath);
assertErrorId(@()ipceReadIT(string(unitTracePath)), ...
    "IPCE:TraceUnitsRequired");
overrideTrace = ipceReadIT(string(unitTracePath), ...
    TimeUnit="min", CurrentUnit="uA");
assert(isequal(overrideTrace.Time_s, [0; 60]));
assert(max(abs(overrideTrace.Current_A - [1e-6; 2e-6])) < 1e-18);
writelines(["Time/min, Current/µA"; "0, 1"; "2, 3"], unitTracePath);
microTrace = ipceReadIT(string(unitTracePath));
assert(isequal(microTrace.Time_s, [0; 120]));
assert(max(abs(microTrace.Current_A - [1e-6; 3e-6])) < 1e-18);
fprintf("  i-t unit detection/conversion: passed\n");

% 3) Verify standalone two-column external IPCE import.
externalPath = fullfile(pwd, "IPCE_external_selftest.csv");
cleanupExternal = onCleanup(@()deleteIfPresent(externalPath));
writelines(["Wavelength/nm,IPCE/%"; "600,120"; "400,50"; ...
    "500,80"; "500,100"], externalPath);
externalIPCE = ipceReadExternalIPCE(string(externalPath));
assert(isequal(externalIPCE.Wavelength_nm, [400; 500; 600]));
assert(isequal(externalIPCE.IPCE_percent, [50; 90; 120]));
assert(string(externalIPCE.Properties.UserData.WavelengthUnit) == "nm");
assert(string(externalIPCE.Properties.UserData.IPCEUnit) == "%");
assert(contains(string(externalIPCE.Properties.UserData.IPCEHeader), ...
    "IPCE"));
standaloneSpectrum = table((400:25:600)', ones(9, 1), ...
    'VariableNames', {'Wavelength_nm', 'Irradiance_W_m2_nm'});
[selectedExternal, selectedLabel] = ipceResolveIPCESource( ...
    table(), externalIPCE, "external");
assert(isequal(selectedExternal, externalIPCE));
assert(selectedLabel == "外部导入 IPCE");
assertErrorId(@()ipceResolveIPCESource( ...
    table(), table(), "external"), "IPCE:MissingExternalIPCE");
[standaloneSummary, standaloneCurve] = ipceIntegrateSpectrum( ...
    selectedExternal, standaloneSpectrum, 400, 600);
assert(isfinite(standaloneSummary.IntegratedCurrentDensity_mA_cm2));
assert(abs(standaloneCurve.CumulativeCurrentDensity_mA_cm2(end) - ...
    standaloneSummary.IntegratedCurrentDensity_mA_cm2) < 1e-12);
fprintf("  External IPCE import: passed\n");

% 4) Verify startup defaults and requested automatic-load files.
assert(defaults.CalibrationFile == ...
    "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx");
assert(defaults.SpectrumFile == "标准太阳能光谱数据.xls");
assert(defaults.SiliconTraceFile == ...
    "Si-i t [300 1100] nm-grating 2-filter.txt");
assert(defaults.SiliconAnchorFile == ...
    "Si-i t [300 1100] nm-grating 2-filter-time match.txt");
assert(defaults.SubtractDark);
assert(isequal(defaults.SiliconDarkRange_s, [0.1, 10]));
assert(isequal(defaults.SampleDarkRange_s, [50, 60]));
assert(all(isfile(defaultPaths)));
defaultAnchors = ipceReadAnchors(defaultPaths(4));
assert(size(defaultAnchors, 2) == 2);
assert(all(diff(defaultAnchors(:, 1)) > 0));
assert(all(isfinite(defaultAnchors), "all"));

resolverRoot = string(tempname(pwd));
primaryRoot = fullfile(resolverRoot, "primary");
fallbackRoot = fullfile(resolverRoot, "fallback");
mkdir(primaryRoot);
mkdir(fallbackRoot);
cleanupResolver = onCleanup(@()removeFolderIfPresent(resolverRoot));

exactName = "default.txt";
writelines("primary", fullfile(primaryRoot, exactName));
writelines("fallback", fullfile(fallbackRoot, exactName));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == string(fullfile(primaryRoot, exactName)));

delete(fullfile(primaryRoot, exactName));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == string(fullfile(fallbackRoot, exactName)));

delete(fullfile(fallbackRoot, exactName));
writelines("similar", fullfile(fallbackRoot, "similar.txt"));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == "");
fprintf("  Startup defaults and default anchors: passed\n");

% 5) Synthetic end-to-end calculation with known quantum efficiencies.
wavelength = [400; 500; 600];
responsivity = [0.20; 0.30; 0.40];
incidentPowerDensity = [10; 15; 12] * 1e-6;
siliconArea_cm2 = 0.36;
sampleArea_cm2 = 0.75;
siliconCollectedPower = incidentPowerDensity * siliconArea_cm2;
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
[lightResult, result] = ipceCalculate(calibration, siliconExtracted, ...
    sampleExtracted, siliconArea_cm2, sampleArea_cm2);

fprintf("  Synthetic power-density max abs error: %.6g W/cm^2\n", ...
    max(abs(lightResult.IncidentPowerDensity_W_cm2 - ...
    incidentPowerDensity)));
fprintf("  Synthetic IPCE max abs error: %.6g percentage points\n", ...
    max(abs(result.IPCE_percent - 100 * expectedFraction)));
assert(max(abs(lightResult.IncidentPowerDensity_W_cm2 - ...
    incidentPowerDensity)) < 1e-14);
assert(all(lightResult.SiliconIlluminatedArea_cm2 == siliconArea_cm2));
assert(max(abs(result.SamplePhotocurrentDensity_A_cm2 - ...
    samplePhotocurrentDensity)) < 1e-14);
assert(all(result.SampleIlluminatedArea_cm2 == sampleArea_cm2));
assert(max(abs(result.IPCE_percent - 100 * expectedFraction)) < 1e-9);
fprintf("  Synthetic calculation: passed (20%%, 50%%, 80%%)\n");

fprintf("  Detector/sample area normalization: passed\n");

% 6) Verify an explicitly selected dark-current time range.
syntheticSchedule = ipceBuildSchedule(wavelength, "fixed", ...
    zeros(0, 2), startTime, dwellTime);
explicitDarkRange = [0.25, 1.25];
explicitDarkExtracted = ipceExtractSchedule(siliconTrace, ...
    syntheticSchedule, tailAverageTime, true, explicitDarkRange);
assert(abs(explicitDarkExtracted.Properties.UserData.DarkCurrent_A - ...
    siliconDark) < 1e-18);
assert(explicitDarkExtracted.Properties.UserData.DarkSampleCount > 2);
assert(explicitDarkExtracted.Properties.UserData.DarkWindowStart_s == ...
    explicitDarkRange(1));
assert(explicitDarkExtracted.Properties.UserData.DarkWindowEnd_s == ...
    explicitDarkRange(2));
fprintf("  Explicit dark-current range: passed\n");

% 7) Verify two-column wavelength-time anchor import.
anchorImportPath = fullfile(pwd, "IPCE_anchor_selftest.txt");
if isfile(anchorImportPath)
    delete(anchorImportPath);
end
writetable(array2table([370, 127; 400, 168; 500, 333], ...
    'VariableNames', {'Wavelength_nm', 'ConfirmedTime_s'}), ...
    anchorImportPath, "Delimiter", "\t");
importedAnchors = ipceReadAnchors(string(anchorImportPath));
assert(isequal(importedAnchors, [370, 127; 400, 168; 500, 333]));
delete(anchorImportPath);
fprintf("  Two-column anchor import: passed\n");

% 8) Verify independent sample wavelength grid and power-density interpolation.
sampleWavelength2 = [450; 550];
powerDensityAtSample2 = interp1(wavelength, incidentPowerDensity, ...
    sampleWavelength2, "pchip");
expectedFraction2 = [0.30; 0.70];
samplePhoto2 = powerDensityAtSample2 .* expectedFraction2 .* ...
    sampleWavelength2 / constant_eV_nm * sampleArea_cm2;
sampleExtracted2 = table(sampleWavelength2, samplePhoto2, samplePhoto2, ...
    abs(samplePhoto2), zeros(2, 1), repmat(20, 2, 1), ...
    'VariableNames', {'Wavelength_nm', 'MeanCurrent_A', ...
    'PhotoCurrent_A', 'AbsPhotoCurrent_A', 'PhotoCurrentSE_A', ...
    'SampleCount'});
[~, interpolatedResult] = ipceCalculate(calibration, ...
    siliconExtracted, sampleExtracted2, siliconArea_cm2, sampleArea_cm2);
assert(max(abs(interpolatedResult.IPCE_percent - ...
    100 * expectedFraction2)) < 1e-9);
assert(all(interpolatedResult.PowerDensityInterpolated));
fprintf("  Independent sample wavelength grid: passed\n");

% 9) Verify spectrum interpolation/integration against an analytic result.
syntheticSpectrum = table((400:25:600)', ones(9, 1), ...
    'VariableNames', {'Wavelength_nm', 'Irradiance_W_m2_nm'});
syntheticIPCE = table([400; 500; 600], [100; 100; 100], ...
    'VariableNames', {'Wavelength_nm', 'IPCE_percent'});
[integrationSummary, integrationCurve] = ipceIntegrateSpectrum( ...
    syntheticIPCE, syntheticSpectrum, 400, 600);
planck = 6.62607015e-34;
speedOfLight = 299792458;
elementaryCharge = 1.602176634e-19;
expectedCurrent_A_m2 = elementaryCharge / (planck * speedOfLight) * ...
    1e-9 * 0.5 * (600^2 - 400^2);
expectedCurrent_mA_cm2 = 0.1 * expectedCurrent_A_m2;
assert(abs(integrationSummary.IntegratedCurrentDensity_mA_cm2 - ...
    expectedCurrent_mA_cm2) < 1e-12);
assert(abs(integrationCurve.CumulativeCurrentDensity_mA_cm2(end) - ...
    expectedCurrent_mA_cm2) < 1e-12);
assert(all(diff(integrationCurve.CumulativeCurrentDensity_mA_cm2) >= 0));
fprintf("  Spectrum integration analytic check: passed\n");

spectrumPath = defaultPaths(2);
spectrumColumns = ipceReadSpectrumHeaders(spectrumPath, "Spectra");
assert(any(spectrumColumns.ColumnIndex == 1 & ...
    contains(lower(spectrumColumns.Header), "wavelength")));
assert(any(spectrumColumns.ColumnIndex == 3 & ...
    contains(lower(spectrumColumns.Header), "global tilt")));
importedSpectrum = ipceReadSpectrum(spectrumPath, "Spectra", 1, 3);
assert(height(importedSpectrum) > 100);
assert(all(importedSpectrum.Irradiance_W_m2_nm >= 0));
fprintf("  Solar spectrum header selection/import: %d points, %.4g-%.4g nm\n", ...
    height(importedSpectrum), min(importedSpectrum.Wavelength_nm), ...
    max(importedSpectrum.Wavelength_nm));

% 10) Verify the supplied trace can be segmented with the current GUI defaults.
wavelengths = (300:5:1100)';
siliconAnchors = [370, 127; 400, 168; 500, 333; 885, 965];
schedule = ipceBuildSchedule(wavelengths, "anchors", ...
    siliconAnchors, 50, 8);
assert(abs(schedule.ReferenceTime_s(wavelengths == 370) - 127) < 1e-12);
assert(abs(schedule.ReferenceTime_s(wavelengths == 400) - 168) < 1e-12);
assert(abs(schedule.ReferenceTime_s(wavelengths == 500) - 333) < 1e-12);
assert(abs(schedule.ReferenceTime_s(wavelengths == 885) - 965) < 1e-12);
assert(all(diff(schedule.ReferenceTime_s) > 0));
extracted = ipceExtractSchedule(importedTrace, schedule, 4, false, 5);
[lightResult, ~] = ipceCalculate(importedCalibration, extracted, ...
    table(), 0.36, 1);
assert(height(lightResult) == 161);
assert(all(isfinite(lightResult.IncidentPowerDensity_W_cm2)));
assert(all(lightResult.IncidentPowerDensity_W_cm2 > 0));
fprintf("  Supplied data/anchor alignment: passed, %.3f-%.3f s, power density %.4g-%.4g uW/cm^2\n", ...
    schedule.WindowStart_s(1), schedule.WindowEnd_s(end), ...
    min(lightResult.IncidentPowerDensity_W_cm2) * 1e6, ...
    max(lightResult.IncidentPowerDensity_W_cm2) * 1e6);

% 11) Verify standalone external-IPCE post-processing export.
standaloneExportPath = fullfile(pwd, ...
    "IPCE_external_export_selftest.xlsx");
cleanupStandaloneExport = onCleanup( ...
    @()deleteIfPresent(standaloneExportPath));
standaloneItems = ipceBuildPostprocessExportItems( ...
    externalIPCE, standaloneSummary, standaloneCurve, true, true);
assert(isequal(string({standaloneItems.Name}), ...
    ["ExternalIPCE", "SpectrumSummary", "SpectrumCurve"]));
ipceWriteExport(standaloneItems, string(standaloneExportPath), "xlsx");
standaloneSheets = sheetnames(standaloneExportPath);
assert(all(ismember(["ExternalIPCE", "SpectrumSummary", ...
    "SpectrumCurve"], standaloneSheets)));
fprintf("  Standalone external-IPCE export: passed\n");

% 12) Verify multi-sheet XLSX export and on-disk existence.
exportPath = fullfile(pwd, "IPCE_export_selftest.xlsx");
if isfile(exportPath)
    delete(exportPath);
end
exportItems = struct( ...
    "Name", {"SiPowerDensity", "SampleIPCE", "SpectrumCurve"}, ...
    "Data", {lightResult, result, integrationCurve});
writtenPaths = ipceWriteExport(exportItems, string(exportPath), "xlsx");
assert(numel(writtenPaths) == 1 && isfile(writtenPaths(1)));
exportSheets = sheetnames(writtenPaths(1));
assert(all(ismember( ...
    ["SiPowerDensity", "SampleIPCE", "SpectrumCurve"], exportSheets)));
exportedSpectrumCurve = readtable(writtenPaths(1), "Sheet", "SpectrumCurve");
assert(ismember("CumulativeCurrentDensity_mA_cm2", ...
    string(exportedSpectrumCurve.Properties.VariableNames)));
delete(writtenPaths(1));
fprintf("  XLSX export and verification: passed\n");

% 13) Verify the portable-package manifest.
packageConfig = ipcePortablePackageConfig();
assert(packageConfig.ReleaseName == "IPCEApp_R2023b_Windows_x64");
assert(packageConfig.ArchiveName == ...
    "IPCEApp_R2023b_Windows_x64.zip");
assert(packageConfig.ExecutableName == "IPCEApp.exe");
assert(numel(packageConfig.DefaultFiles) == 4);
assert(all(isfile(packageConfig.DefaultFiles)));
assert(isfile(packageConfig.PortableReadme));
fprintf("  Portable package manifest: passed\n");

% 14) Verify bilingual catalog integrity and safe preference behavior.
englishCatalog = ipceLanguageCatalog("en-US");
chineseCatalog = ipceLanguageCatalog("zh-CN");
assert(englishCatalog.Language == "en-US");
assert(chineseCatalog.Language == "zh-CN");
assert(isequal(englishCatalog.Keys, chineseCatalog.Keys));
assert(numel(englishCatalog.Keys) >= 8);
assert(all(strlength(englishCatalog.Values) > 0));
assert(all(strlength(chineseCatalog.Values) > 0));
assert(ipceLanguageCatalog("fr-FR", "App.Title") == ...
    "IPCE Measurement and Analysis");
assert(ipceLanguageCatalog("zh-CN", "App.Title") == ...
    "IPCE 测量与分析");
assert(ipceLanguageCatalog("en-US", "Missing.Key") == ...
    "[Missing.Key]");

preferenceFolder = tempname;
mkdir(preferenceFolder);
cleanupPreference = onCleanup( ...
    @()removeFolderIfPresent(preferenceFolder));
preferencePath = fullfile(preferenceFolder, "settings.json");
assert(isscalar(ipceSystemLocale(@()"zh-CN")));
assert(ipceSystemLocale(@()"zh-CN") == "zh-CN");
assert(ipceSystemLocale(@()"en-GB") == "en-GB");
assert(isscalar(ipceSystemLocale()) && strlength(ipceSystemLocale()) > 0);
assert(ipceLanguagePreference( ...
    "resolve", preferencePath, "zh-TW") == "zh-CN");
assert(ipceLanguagePreference( ...
    "resolve", preferencePath, "de-DE") == "en-US");
ipceLanguagePreference("save", preferencePath, "en-US");
assert(ipceLanguagePreference("load", preferencePath) == "en-US");
savedPreference = jsondecode(fileread(preferencePath));
assert(isfield(savedPreference, "Language"));
assert(~isfield(savedPreference, "language"));
assert(ipceLanguagePreference( ...
    "resolve", preferencePath, "zh-CN") == "en-US");
writelines('{"language":"zh-CN"}', preferencePath);
assert(ipceLanguagePreference("load", preferencePath) == "zh-CN");
invalidPreferences = [ ...
    "{""Language"" : [""en-US"",""zh-CN""]}"; ...
    "{""Language"" : null}"; ...
    "{""Language"" : {""name"":""en-US""}}"; ...
    "{""Language"" : [""en-US"",42]}" ...
    ];
for preferenceIndex = 1:numel(invalidPreferences)
    writelines(invalidPreferences(preferenceIndex), preferencePath);
    assert(ipceLanguagePreference("load", preferencePath) == "");
end
writelines("not json", preferencePath);
assert(ipceLanguagePreference("load", preferencePath) == "");
assert(ipceLanguagePreference( ...
    "resolve", preferencePath, "zh-CN") == "zh-CN");
fprintf("  Bilingual catalog and language preference: passed\n");

runtimeFormats = [ ...
    "导出成功：%s"; ...
    "已读取标探响应度：%d 点，%.6g–%.6g nm。"; ...
    "请先导入标准光谱数据。"; ...
    "无法完成计算"; ...
    "光谱积分完成（%s）：%.6g–%.6g nm，J = %.6g mA cm^{-2}。" ...
    ];
for formatIndex = 1:numel(runtimeFormats)
    translatedFormat = ipceLocalizeLiteral("en-US", runtimeFormats(formatIndex));
    assert(isempty(regexp(translatedFormat, '[\x{4e00}-\x{9fff}]', 'once')));
end
assert(ipceLocalizeLiteral("en-US", "导出成功：%s") == ...
    "Export successful: %s");
assert(ipceLocalizeLiteral("en-US", "无法完成计算") == ...
    "Unable to complete calculation");
assert(ipceLocalizeLiteral("en-US", "这是未登记文案") == ...
    "[Missing English localization]");
localizedTraceError = ipceLocalizeException("en-US", ...
    "IPCE:InvalidTrace", "i-t 文件中有效数据少于两个点。");
assert(localizedTraceError == ...
    "The i-t data are invalid or contain too few usable points.");
unknownLocalizedError = ipceLocalizeException("en-US", ...
    "IPCE:FutureError", "未来错误文案");
assert(contains(unknownLocalizedError, "IPCE:FutureError"));
assert(isempty(regexp(unknownLocalizedError, ...
    '[\x{4e00}-\x{9fff}]', 'once')));
fprintf("  Runtime message localization: passed\n");

% 15) Verify the real UI switches language without recreating state.
localizationApp = IPCEApp;
cleanupLocalizationApp = onCleanup(@()closeIfValid(localizationApp));
drawnow;
hooks = localizationApp.UserData;
assert(isfield(hooks, "SetLanguage"));
assert(isfield(hooks, "StateSignature"));
assert(isfield(hooks, "VisibleTexts"));
assert(isfield(hooks, "SetStatusForTest"));
assert(isfield(hooks, "SetDynamicSurfaceForTest"));
assert(isfield(hooks, "DynamicSurfaceSnapshot"));
assert(isfield(hooks, "OpenAnchorDialogForTest"));
assert(isfield(hooks, "OpenExportDialogForTest"));
assert(isfield(hooks, "PopulateExternalWorkflowForTest"));
assert(isfield(hooks, "PlotTexts"));
beforeLanguageSwitch = hooks.StateSignature();
hooks.SetLanguage("en-US");
hooks.SetStatusForTest("导出成功：%s", "result.xlsx");
drawnow;
assert(localizationApp.Name == "IPCE Measurement and Analysis");
englishVisible = string(hooks.VisibleTexts());
englishVisible(englishVisible == "中文") = [];
assert(isempty(regexp(join(englishVisible, newline), ...
    '[\x{4e00}-\x{9fff}]', 'once')));
assert(~contains(join(englishVisible, newline), ...
    "[Missing English localization]"));
assert(any(englishVisible == "Export successful: result.xlsx"));
assertNoHanExceptChineseLabel(string(hooks.PlotTexts()));
anchorDialog = hooks.OpenAnchorDialogForTest("silicon", 12.5, 1e-6);
drawnow;
assert(string(anchorDialog.Name) == "Confirm new anchor");
assertNoHanExceptChineseLabel(collectFigureTexts(anchorDialog));
close(anchorDialog);
exportDialog = hooks.OpenExportDialogForTest();
drawnow;
assert(string(exportDialog.Name) == "Select export content");
assertNoHanExceptChineseLabel(collectFigureTexts(exportDialog));
close(exportDialog);
assert(isequaln(beforeLanguageSwitch, hooks.StateSignature()));
hooks.SetLanguage("zh-CN");
drawnow;
assert(localizationApp.Name == "IPCE 测量与分析");
assert(any(string(hooks.VisibleTexts()) == "导出成功：result.xlsx"));
assert(isequaln(beforeLanguageSwitch, hooks.StateSignature()));
hooks.SetDynamicSurfaceForTest( ...
    "C:\data\calibration.xlsx", ["Lambda", "Irradiance"], [1, 3]);
dynamicSurfaceBefore = hooks.DynamicSurfaceSnapshot();
hooks.PopulateExternalWorkflowForTest();
externalWorkflowBefore = hooks.StateSignature();
hooks.SetLanguage("en-US");
drawnow;
assert(localizationApp.Name == "IPCE Measurement and Analysis");
assert(isequaln(dynamicSurfaceBefore, hooks.DynamicSurfaceSnapshot()));
assert(isequaln(externalWorkflowBefore, hooks.StateSignature()));
assertNoHanExceptChineseLabel(string(hooks.PlotTexts()));
close(localizationApp);
clear cleanupLocalizationApp
fprintf("  Live bilingual UI state preservation: passed\n");

fprintf("All IPCE self-tests passed.\n");
end

function assertErrorId(operation, expectedIdentifier)
try
    operation();
catch exception
    assert(string(exception.identifier) == string(expectedIdentifier), ...
        "Expected %s, got %s: %s", expectedIdentifier, ...
        exception.identifier, exception.message);
    return
end
error("IPCE:ExpectedErrorNotThrown", ...
    "Expected error %s was not thrown.", expectedIdentifier);
end

function deleteIfPresent(filePath)
if isfile(filePath)
    delete(filePath);
end
end

function removeFolderIfPresent(folderPath)
if isfolder(folderPath)
    rmdir(folderPath, "s");
end
end

function closeIfValid(figureHandle)
if isvalid(figureHandle)
    close(figureHandle);
end
end

function texts = collectFigureTexts(figureHandle)
texts = string(figureHandle.Name);
handles = findall(figureHandle);
for handleIndex = 1:numel(handles)
    handle = handles(handleIndex);
    if isprop(handle, "Text")
        try
            value = handle.Text;
            if ischar(value)
                texts(end + 1, 1) = string(value); %#ok<AGROW>
            elseif isstring(value) || iscell(value)
                texts = [texts; string(value(:))]; %#ok<AGROW>
            end
        catch
        end
    end
end
end

function assertNoHanExceptChineseLabel(texts)
texts = string(texts);
texts(texts == "中文") = [];
assert(isempty(regexp(join(texts, newline), ...
    '[\x{4e00}-\x{9fff}]', 'once')));
assert(~contains(join(texts, newline), "[Missing English localization]"));
end
