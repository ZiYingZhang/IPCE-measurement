# IPCE Import and Post-processing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add trace-unit normalization, requested startup defaults, and a standalone external-IPCE spectrum-integration/export workflow.

**Architecture:** Keep all calculation functions on canonical units (`s`, `A`, `nm`, `%`) and place file-format normalization in dedicated readers. Keep calculated and imported IPCE in separate application state fields; the existing integration function consumes whichever canonical table the user selects.

**Tech Stack:** MATLAB programmatic UI (`uifigure`), MATLAB tables, `readcell`/`readmatrix`, existing calculation/export functions, `run_ipce_selftest`.

## Global Constraints

- Internal i-t units remain seconds and amperes in variables `Time_s` and `Current_A`.
- Time-alignment files remain fixed at wavelength `nm` and time `s`.
- External IPCE files use wavelength `nm` in the first numeric column and IPCE `%` in the second numeric column.
- Spectrum irradiance remains `W m^-2 nm^-1`; integrated current density remains `mA cm^-2`.
- Do not extrapolate IPCE or spectrum beyond their common wavelength coverage.
- Do not clip finite external IPCE values to `0–100%`.
- External post-processing must work without calibration, silicon trace, sample trace, or anchors.
- The directory is not currently a Git repository; do not initialize one or claim commits were created.

---

### Task 1: Normalize i-t units and preserve source metadata

**Files:**
- Modify: `ipceReadIT.m`
- Modify: `run_ipce_selftest.m`

**Interfaces:**
- Consumes: `ipceReadIT(filePath, TimeUnit=..., CurrentUnit=...)`.
- Produces: canonical table with `Time_s`, `Current_A` plus trace source/unit metadata in `Properties.UserData`.

- [ ] **Step 1: Add failing self-tests for recognized and missing units**

Add temporary text fixtures for these behaviors before changing the reader:

```matlab
unitTracePath = fullfile(pwd, "IPCE_units_selftest.txt");
cleanupUnitTrace = onCleanup(@()deleteIfPresent(unitTracePath));
writelines(["Time/ms, Current/mA"; "0, 1"; "1000, 2"], unitTracePath);
unitTrace = ipceReadIT(string(unitTracePath));
assert(isequal(unitTrace.Time_s, [0; 1]));
assert(max(abs(unitTrace.Current_A - [1e-3; 2e-3])) < 1e-15);
assert(unitTrace.Properties.UserData.OriginalTimeUnit == "ms");
assert(unitTrace.Properties.UserData.OriginalCurrentUnit == "mA");
assert(contains(unitTrace.Properties.UserData.RawHeaderText, "Time/ms"));

writelines(["time, current"; "0, 1"; "1, 2"], unitTracePath);
assertErrorId(@()ipceReadIT(string(unitTracePath)), ...
    "IPCE:TraceUnitsRequired");
overrideTrace = ipceReadIT(string(unitTracePath), ...
    TimeUnit="min", CurrentUnit="uA");
assert(isequal(overrideTrace.Time_s, [0; 60]));
assert(max(abs(overrideTrace.Current_A - [1e-6; 2e-6])) < 1e-18);
```

Add local helpers at the end of the self-test:

```matlab
function assertErrorId(operation, expectedIdentifier)
try
    operation();
catch exception
    assert(exception.identifier == expectedIdentifier);
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
```

- [ ] **Step 2: Run the self-test and verify RED**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: failure because the current reader neither detects/converts `ms/mA`
nor accepts `TimeUnit` and `CurrentUnit`.

- [ ] **Step 3: Implement header extraction, unit parsing, and conversion**

Change the public arguments to:

```matlab
arguments
    filePath (1, 1) string
    options.TimeUnit (1, 1) string = ""
    options.CurrentUnit (1, 1) string = ""
end
```

Read textual files with `readcell` so the numeric columns and nearest text
headers can be identified together. For spreadsheet/CSV input, use the same
cell-based column discovery. Retain the numeric-regex fallback for unusual CHI
text exports, but parse the raw text header before the numeric block.

Use focused local helpers with these contracts:

```matlab
function [first, second, firstHeader, secondHeader] = ...
    firstTwoNumericCellColumns(raw)
function unit = detectTimeUnit(headerText)
function unit = detectCurrentUnit(headerText)
function factor = timeToSecondsFactor(unit)
function factor = currentToAmperesFactor(unit)
```

Recognize:

```matlab
timeUnits = ["s", "sec", "second", "ms", "min", "h"];
currentUnits = ["A", "mA", "uA", "µA", "μA", "nA", "pA"];
```

Normalize aliases (`sec`/`second` to `s`, `µA`/`μA` to `uA`) but save the
recognized source spelling. If either unit remains empty, throw:

```matlab
error("IPCE:TraceUnitsRequired", ...
    "无法从 i-t 表头识别时间或电流单位。时间列“%s”，电流列“%s”。", ...
    firstHeader, secondHeader);
```

Set the agreed metadata fields:

```matlab
trace.Properties.UserData.SourceFile = char(filePath);
trace.Properties.UserData.RawHeaderText = char(rawHeaderText);
trace.Properties.UserData.OriginalTimeHeader = char(firstHeader);
trace.Properties.UserData.OriginalCurrentHeader = char(secondHeader);
trace.Properties.UserData.OriginalTimeUnit = char(timeUnit);
trace.Properties.UserData.OriginalCurrentUnit = char(currentUnit);
trace.Properties.UserData.TimeToSecondsFactor = timeFactor;
trace.Properties.UserData.CurrentToAmperesFactor = currentFactor;
trace.Properties.UserData.SampleInterval_s = sampleInterval;
```

- [ ] **Step 4: Run the self-test and verify GREEN**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: the new unit checks pass, and all existing import/calculation checks
remain green.

---

### Task 2: Read external two-column IPCE data

**Files:**
- Create: `ipceReadExternalIPCE.m`
- Modify: `run_ipce_selftest.m`

**Interfaces:**
- Consumes: a `.txt`, `.csv`, `.xls`, or `.xlsx` path.
- Produces: table with `Wavelength_nm`, `IPCE_percent` and source/header metadata.

- [ ] **Step 1: Add failing tests for sorting, duplicate averaging, and values above 100%**

Add:

```matlab
externalPath = fullfile(pwd, "IPCE_external_selftest.csv");
cleanupExternal = onCleanup(@()deleteIfPresent(externalPath));
writelines(["Wavelength/nm,IPCE/%"; "600,120"; "400,50"; ...
    "500,80"; "500,100"], externalPath);
externalIPCE = ipceReadExternalIPCE(string(externalPath));
assert(isequal(externalIPCE.Wavelength_nm, [400; 500; 600]));
assert(isequal(externalIPCE.IPCE_percent, [50; 90; 120]));
assert(externalIPCE.Properties.UserData.WavelengthUnit == "nm");
assert(externalIPCE.Properties.UserData.IPCEUnit == "%");
assert(contains(externalIPCE.Properties.UserData.IPCEHeader, "IPCE"));
```

- [ ] **Step 2: Run the self-test and verify RED**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: failure with undefined function `ipceReadExternalIPCE`.

- [ ] **Step 3: Implement the dedicated reader**

Implement:

```matlab
function ipce = ipceReadExternalIPCE(filePath)
arguments
    filePath (1, 1) string
end
```

Use `readcell`, choose the first two columns containing at least two finite
numeric values, preserve the nearest text header before numeric data, filter
nonpositive wavelengths and nonfinite IPCE, sort, then average duplicates:

```matlab
[wavelength, order] = sort(wavelength);
ipcePercent = ipcePercent(order);
[wavelength, ~, group] = unique(wavelength);
ipcePercent = accumarray(group, ipcePercent, [], @mean);
```

Require two distinct wavelengths and store:

```matlab
ipce.Properties.UserData.SourceFile
ipce.Properties.UserData.WavelengthHeader
ipce.Properties.UserData.IPCEHeader
ipce.Properties.UserData.WavelengthUnit = "nm"
ipce.Properties.UserData.IPCEUnit = "%"
```

- [ ] **Step 4: Run the self-test and verify GREEN**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: external import checks and all earlier checks pass.

---

### Task 3: Make startup defaults explicit and testable

**Files:**
- Create: `ipceDefaultConfig.m`
- Modify: `IPCEApp.m`
- Modify: `run_ipce_selftest.m`

**Interfaces:**
- Produces: scalar configuration struct consumed by `IPCEApp`.

- [ ] **Step 1: Add failing default-configuration tests**

Add:

```matlab
defaults = ipceDefaultConfig();
assert(defaults.SiliconTraceFile == ...
    "Si-i t [300 1100] nm-grating 2-filter.txt");
assert(defaults.SiliconAnchorFile == ...
    "Si-i t [300 1100] nm-grating 2-filter-time match.txt");
assert(defaults.SubtractDark);
assert(isequal(defaults.SiliconDarkRange_s, [0.1, 10]));
assert(isequal(defaults.SampleDarkRange_s, [50, 60]));
```

Also read the real files and validate the anchor table:

```matlab
assert(isfile(defaults.SiliconTraceFile));
assert(isfile(defaults.SiliconAnchorFile));
defaultAnchors = ipceReadAnchors(defaults.SiliconAnchorFile);
assert(size(defaultAnchors, 2) == 2);
assert(all(diff(defaultAnchors(:, 1)) > 0));
assert(all(isfinite(defaultAnchors), "all"));
```

- [ ] **Step 2: Run the self-test and verify RED**

Expected: undefined function `ipceDefaultConfig`.

- [ ] **Step 3: Implement defaults and consume them in the UI**

Create:

```matlab
function defaults = ipceDefaultConfig
defaults = struct( ...
    "SiliconTraceFile", ...
    "Si-i t [300 1100] nm-grating 2-filter.txt", ...
    "SiliconAnchorFile", ...
    "Si-i t [300 1100] nm-grating 2-filter-time match.txt", ...
    "SubtractDark", true, ...
    "SiliconDarkRange_s", [0.1, 10], ...
    "SampleDarkRange_s", [50, 60]);
end
```

At app creation, assign `defaults = ipceDefaultConfig();`. Initialize UI values
from the struct:

```matlab
darkCheckBox.Value = defaults.SubtractDark;
siliconDarkStartField.Value = defaults.SiliconDarkRange_s(1);
siliconDarkEndField.Value = defaults.SiliconDarkRange_s(2);
sampleDarkStartField.Value = defaults.SampleDarkRange_s(1);
sampleDarkEndField.Value = defaults.SampleDarkRange_s(2);
```

In `autoLoadWorkspaceFiles`, load the exact trace path and exact anchor path.
For anchors:

```matlab
if isfile(defaults.SiliconAnchorFile)
    siliconAnchorTable.Data = ipceReadAnchors(defaults.SiliconAnchorFile);
    state.siliconAnchorRow = 1;
end
```

Collect nonfatal automatic-load messages and show one status summary without
overwriting useful trace/unit information.

- [ ] **Step 4: Add GUI fallback for unknown trace units**

Change `loadSilicon` and `loadSample` to call a helper that retries only
`IPCE:TraceUnitsRequired`. Use two `uiconfirm` prompts with explicit cancel
options:

```matlab
timeUnit = uiconfirm(appFigure, ...
    "无法识别时间单位，请选择。", "选择时间单位", ...
    "Options", ["s", "ms", "min", "h", "取消"], ...
    "DefaultOption", "s", "CancelOption", "取消");
currentUnit = uiconfirm(appFigure, ...
    "无法识别电流单位，请选择。", "选择电流单位", ...
    "Options", ["A", "mA", "uA", "nA", "pA", "取消"], ...
    "DefaultOption", "A", "CancelOption", "取消");
```

Cancel returns an empty table and leaves the old state unchanged. Successful
import status includes source units and canonical units.

- [ ] **Step 5: Run self-tests and a UI construction smoke test**

Run:

```powershell
matlab -batch "run_ipce_selftest; app=IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: all assertions pass and the app constructs/closes without an error.

---

### Task 4: Add selectable external-IPCE spectrum integration

**Files:**
- Modify: `IPCEApp.m`
- Modify: `run_ipce_selftest.m`

**Interfaces:**
- State adds: `externalIPCE`, `externalIPCEFile`, `spectrumIPCESource`.
- UI chooses either `state.ipceResult` or `state.externalIPCE`.

- [ ] **Step 1: Add a failing independent-integration test**

After reading the external fixture, call the existing integration function
without constructing any measurement data:

```matlab
standaloneSpectrum = table((400:25:600)', ones(9, 1), ...
    'VariableNames', {'Wavelength_nm', 'Irradiance_W_m2_nm'});
[standaloneSummary, standaloneCurve] = ipceIntegrateSpectrum( ...
    externalIPCE, standaloneSpectrum, 400, 600);
assert(isfinite(standaloneSummary.IntegratedCurrentDensity_mA_cm2));
assert(abs(standaloneCurve.CumulativeCurrentDensity_mA_cm2(end) - ...
    standaloneSummary.IntegratedCurrentDensity_mA_cm2) < 1e-12);
```

- [ ] **Step 2: Run the self-test and verify RED/GREEN boundary**

The pure calculation should already pass, demonstrating that no algorithm
change is required. The missing behavior is UI state and source selection;
do not change `ipceIntegrateSpectrum`.

- [ ] **Step 3: Add UI state and controls**

Add state fields:

```matlab
"externalIPCE", table(), ...
"externalIPCEFile", "", ...
"spectrumIPCESource", "calculated", ...
```

Expand the spectrum control panel to include:

```matlab
externalIPCEImportButton
externalIPCEPathLabel
ipceSourceDropDown
```

The dropdown uses:

```matlab
"Items", ["本软件计算结果", "外部导入 IPCE"], ...
"ItemsData", ["calculated", "external"]
```

Create `onLoadExternalIPCE`, `loadExternalIPCEFile`, and
`selectedIPCEForSpectrum`. A successful external import sets the dropdown to
`external`, preserves calculated IPCE, clears stale integration output, and
redraws the preview.

- [ ] **Step 4: Route integration and plotting through the selected source**

Replace the hard-coded `state.ipceResult` check with:

```matlab
[selectedIPCE, sourceLabel] = selectedIPCEForSpectrum();
[state.spectrumSummary, state.spectrumCurve] = ...
    ipceIntegrateSpectrum(selectedIPCE, state.spectrum, ...
    integrationStartField.Value, integrationEndField.Value);
```

Store source metadata in both returned tables:

```matlab
state.spectrumSummary.Properties.UserData.IPCESource = char(sourceLabel);
state.spectrumCurve.Properties.UserData.IPCESource = char(sourceLabel);
```

Update `plotSpectrumPreview` to plot the selected IPCE if it exists, and include
the source label in plot/status text.

- [ ] **Step 5: Run full self-tests and UI smoke test**

Run:

```powershell
matlab -batch "run_ipce_selftest; app=IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: green, with no requirement for sample/calibration data in the pure
external-IPCE integration path.

---

### Task 5: Make export independent of measurement settings

**Files:**
- Modify: `IPCEApp.m`
- Modify: `run_ipce_selftest.m`

**Interfaces:**
- Export includes an `ExternalIPCE` item independently of `SampleIPCE`.
- Parameter export builds available metadata without requiring valid anchors.

- [ ] **Step 1: Add a failing standalone export test**

Add:

```matlab
standaloneExportPath = fullfile(pwd, ...
    "IPCE_external_export_selftest.xlsx");
cleanupStandaloneExport = onCleanup( ...
    @()deleteIfPresent(standaloneExportPath));
standaloneItems = struct( ...
    "Name", {"ExternalIPCE", "SpectrumSummary", "SpectrumCurve"}, ...
    "Data", {externalIPCE, standaloneSummary, standaloneCurve});
ipceWriteExport(standaloneItems, string(standaloneExportPath), "xlsx");
standaloneSheets = sheetnames(standaloneExportPath);
assert(all(ismember(["ExternalIPCE", "SpectrumSummary", ...
    "SpectrumCurve"], standaloneSheets)));
```

This verifies the writer already supports the desired content and defines the
UI export contract.

- [ ] **Step 2: Update export availability and controls**

Allow opening export if any of these exist:

```matlab
state.lightResult
state.ipceResult
state.externalIPCE
state.spectrumSummary
```

Add an “外部导入 IPCE” checkbox and pass its value to `performExport`.

- [ ] **Step 3: Remove unconditional measurement-settings validation**

Only call `currentSettings()` when exporting measurement parameters or
measurement-derived results that need them. Build a standalone metadata table
for source files and integration settings that does not call
`readAnchorData`.

Add `ExternalIPCE` when requested:

```matlab
if includeExternalIPCE && ~isempty(state.externalIPCE)
    items(end + 1) = struct( ...
        "Name", "ExternalIPCE", "Data", state.externalIPCE);
end
```

Include the selected IPCE source and external source file in parameters.

- [ ] **Step 4: Run full verification**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: both the existing multi-sheet export and standalone external export
pass.

---

### Task 6: Update user documentation and Agent memory

**Files:**
- Modify: `README_CN.md`
- Create: `AGENTS.md`
- Create: `PROJECT_MEMORY.md`

**Interfaces:**
- `AGENTS.md` is the automatic quick-start context for future agents.
- `PROJECT_MEMORY.md` is a concise decision/change log.

- [ ] **Step 1: Update the README**

Correct the startup filename and document:

- Exact default trace and anchor files.
- Default enabled dark subtraction and both ranges.
- Canonical `s/A` conversion and unknown-unit prompts.
- Separate “完整测量流程” and “外部 IPCE 后处理流程”.
- External file columns (`nm`, `%`), selected source, common-range restriction,
  cumulative curve, and exports.

- [ ] **Step 2: Create `AGENTS.md`**

Include only durable information:

```markdown
# Project Guide

This MATLAB app converts silicon-detector i-t data to monochromatic power
density, calculates sample IPCE, and integrates either calculated or externally
imported IPCE against a solar spectrum.

## Read First

- `README_CN.md` for user workflows and formulas.
- `PROJECT_MEMORY.md` for recent decisions and changes.
- Run `run_ipce_selftest` after every functional change.
```

Then list canonical units, core file responsibilities, exact defaults, the
no-extrapolation rule, and the standalone-post-processing guarantee.

- [ ] **Step 3: Create `PROJECT_MEMORY.md`**

Record the 2026-07-26 confirmed decisions, implemented files, verification
command, and the fact that the directory is not a Git repository. Do not include
transient reasoning or duplicate the whole README.

- [ ] **Step 4: Check documentation consistency**

Run:

```powershell
rg -n "Si-i t \\[300 1100\\] nm-1|暗电流.*默认.*不|先计算样品 IPCE" README_CN.md AGENTS.md PROJECT_MEMORY.md
```

Expected: no stale instruction saying the old trace is the default, dark
subtraction is off by default, or external post-processing requires calculated
IPCE.

---

### Task 7: Final verification and handoff

**Files:**
- Verify all modified and created project files.

- [ ] **Step 1: Run MATLAB self-tests**

```powershell
matlab -batch "run_ipce_selftest"
```

Expected final line:

```text
All IPCE self-tests passed.
```

- [ ] **Step 2: Run application construction smoke test**

```powershell
matlab -batch "app=IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: MATLAB exits successfully with no uncaught exception.

- [ ] **Step 3: Inspect the working tree files**

```powershell
rg --files
rg -n "externalIPCE|TraceUnitsRequired|SiliconAnchorFile|SubtractDark" ...
    IPCEApp.m ipceReadIT.m ipceDefaultConfig.m ...
    ipceReadExternalIPCE.m run_ipce_selftest.m
```

Confirm all requested features are represented and no temporary self-test files
remain.

- [ ] **Step 4: Report results without claiming a Git commit**

Summarize implemented behavior, test commands/results, documentation paths, and
the non-Git status. Mention any MATLAB/UI verification limitation explicitly if
the local runtime cannot construct `uifigure`.
