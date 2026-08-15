# IPCE C# Visualization, Reproducible Export, and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver MATLAB-equivalent diagnostic plots, schedule/coverage guidance, reproducible exports, and a newly verified portable Windows package.

**Architecture:** Replace the monolithic result code-behind with focused plot controls sharing a ScottPlot theme and immutable plot models. A pure preview builder computes schedules and coverage without mutating results; an export snapshot builder adds settings, anchors, and source metadata while preserving existing result table names.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, ScottPlot.WPF 5.1.59, MSTest 4, existing export adapters, PowerShell portable-build scripts, MATLAB R2023b regression baseline.

## Global Constraints

- Execute only after both earlier 2026-07-28 plans pass.
- Do not change numerical formulas, interpolation, integration, or canonical units.
- Plot downsampling may affect display only; calculations, snapping, and exports use original data.
- Use a Chinese-capable installed font with a deterministic fallback.
- Preserve existing export table names and MAT top-level `exportData`.
- Keep the self-contained Windows x64 ZIP below `200 * 1024 * 1024` bytes.
- Do not include MATLAB Runtime or require a preinstalled .NET Runtime.
- The repository is not a Git repository. Do not initialize Git or add commit steps.
- Every behavior change requires a failing test first.

---

## File Structure

**Create**

- `csharp/src/IPCE.Desktop/Plotting/PlotTheme.cs`
- `csharp/src/IPCE.Desktop/Plotting/PlotModels.cs`
- `csharp/src/IPCE.Desktop/Plotting/PlotViewSettings.cs`
- `csharp/src/IPCE.Desktop/Plotting/WorkflowPreviewBuilder.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/TracePlotView.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/TracePlotView.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/SchedulePlotView.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/SchedulePlotView.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/PowerDensityPlotView.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/PowerDensityPlotView.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/IpcePlotView.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/IpcePlotView.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/SpectrumIntegrationPlotView.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/SpectrumIntegrationPlotView.xaml.cs`
- `csharp/src/IPCE.Desktop/ViewModels/WorkflowExportSnapshot.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowPreviewTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/ReproducibleExportTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/RecoverableUiWorkflowTests.cs`

**Modify**

- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/WorkflowExportTables.cs`
- `csharp/src/IPCE.Desktop/App.xaml`
- `csharp/src/IPCE.Desktop/App.xaml.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- `csharp/scripts/build-portable.ps1`
- `csharp/PORTABLE_README_CN.txt`
- `README_CN.md`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

---

### Task 1: Pure Schedule and Coverage Preview

**Files:**

- Create: `csharp/src/IPCE.Desktop/Plotting/WorkflowPreviewBuilder.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/WorkflowPreviewTests.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`

**Interfaces:**

```csharp
public sealed record CoveragePreview(
    double DataMinimum,
    double DataMaximum,
    double RequestedMinimum,
    double RequestedMaximum,
    bool IsWithinCoverage,
    string Message);

public sealed record SchedulePreview(
    IReadOnlyList<SchedulePoint> Points,
    IReadOnlyList<AnchorPoint> Anchors,
    CoveragePreview Coverage);

public static class WorkflowPreviewBuilder
{
    public static SchedulePreview BuildSchedule(
        TraceData trace,
        IReadOnlyList<double> wavelengths,
        AlignmentMode mode,
        IReadOnlyList<AnchorPoint> anchors,
        double fixedStartTimeSeconds,
        double nominalDelaySeconds);

    public static CoveragePreview BuildIntegrationCoverage(
        IReadOnlyList<IpceValue> ipce,
        IReadOnlyList<SpectrumPoint> spectrum,
        double requestedMinimumNm,
        double requestedMaximumNm);
}
```

- [ ] **Step 1: Write schedule-coverage RED tests**

Use a trace covering `0–100 s` and a fixed schedule covering `10–90 s`; assert
`IsWithinCoverage` and an exact range message. Then request `10–110 s` and
assert:

```csharp
Assert.IsFalse(preview.Coverage.IsWithinCoverage);
StringAssert.Contains(preview.Coverage.Message, "超出 10");
```

Add anchor-mode and common IPCE/spectrum coverage tests.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter WorkflowPreviewTests
```

Expected: compile failure because preview models do not exist.

- [ ] **Step 3: Implement preview without state mutation**

Reuse `WorkflowCalculation.BuildWavelengths` and `ScheduleBuilder.Build`.
Coverage compares the first window start and final window end against the
trace minimum/maximum. It must not call `TraceExtractor`, calculate results, or
set any `SessionState` property.

- [ ] **Step 4: Expose live preview properties**

Silicon and sample ViewModels expose:

```csharp
public SchedulePreview? Preview { get; }
public string CoverageMessage => Preview?.Coverage.Message ?? PrerequisiteMessage;
```

Raise these properties whenever trace, anchors, wavelength grid, alignment
mode, fixed start, or Delay changes.

Spectrum exposes the current common integration coverage and updates when the
selected source, IPCE, spectrum, or integration bounds change.

- [ ] **Step 5: Bind coverage messages**

Show each message directly above its calculation button. Use green foreground
for valid coverage, orange/red for invalid coverage, and ordinary muted text
when prerequisites are missing.

- [ ] **Step 6: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all tests pass.

---

### Task 2: Shared ScottPlot Theme and Immutable Plot Models

**Files:**

- Create: `csharp/src/IPCE.Desktop/Plotting/PlotTheme.cs`
- Create: `csharp/src/IPCE.Desktop/Plotting/PlotModels.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`

**Interfaces:**

```csharp
public sealed record PlotSeries(
    string Label,
    IReadOnlyList<double> X,
    IReadOnlyList<double> Y,
    PlotSeriesKind Kind,
    string ColorHex,
    IReadOnlyList<double>? YErrors = null);

public enum PlotSeriesKind
{
    Line,
    Scatter,
}

public sealed record PlotBand(
    double MinimumX,
    double MaximumX,
    string Label,
    string ColorHex,
    double Opacity);

public sealed record PlotModel(
    string Title,
    string XLabel,
    string YLabel,
    IReadOnlyList<PlotSeries> Series,
    IReadOnlyList<PlotBand> Bands,
    string EmptyMessage);

public static class PlotTheme
{
    public const string PreferredChineseFont = "Microsoft YaHei UI";
    public static void Apply(ScottPlot.Plot plot);
    public static void ApplyLabels(ScottPlot.Plot plot);
}

public sealed record PlotViewSettings(
    double? MinimumX,
    double? MaximumX,
    double? MinimumY,
    double? MaximumY,
    bool LogarithmicX,
    bool LogarithmicY);
```

- [ ] **Step 1: Write font and model RED tests**

Create a plot with title `样品 i-t`, call `PlotTheme.Apply`, and assert the title,
axis labels, tick labels, and legend do not retain ScottPlot's unsupported
default font when Microsoft YaHei UI is installed.

Add model validation tests rejecting mismatched X/Y lengths, mismatched
`YErrors`, invalid band bounds, and non-finite coordinates except where a view
explicitly filters log-axis-invalid points.

Add settings tests rejecting non-increasing explicit limits and logarithmic
axes when the visible data or explicit limits contain non-positive values.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter PlotRenderingTests
```

Expected: compile failure because theme and model types do not exist.

- [ ] **Step 3: Implement the theme**

Set title, axis-label, tick-label, and legend font names to
`Microsoft YaHei UI`. If unavailable, call ScottPlot's `LabelStyle.SetBestFont`
after assigning each Chinese text. Apply a white figure/data background,
light-grey grid, and consistent palette:

```text
raw trace       #1976D2
anchor/mean     #EF6C00
dark region     #607D8B
power density   #00897B
calculated IPCE #1976D2
external IPCE   #EF6C00
spectrum        #F57C00
cumulative Jsc  #558B2F
invalid range   #C62828
```

- [ ] **Step 4: Implement plot-model validation**

Copy incoming arrays to immutable collections. Reject unequal X/Y lengths,
unequal non-null error lengths, non-finite values, and bands whose maximum is
not greater than minimum with stable `IpceException` code
`IPCE:InvalidPlotSeries`.

Implement `PlotViewSettings.Validate(PlotModel model)` with stable code
`IPCE:InvalidAxisLimits`. A logarithmic-axis request must fail with the Chinese
message `数据或坐标范围包含非正值，不能使用对数轴。` rather than rendering
`NaN`.

- [ ] **Step 5: Run focused GREEN**

Run Step 2. Expected: all plot theme/model tests pass.

---

### Task 3: Focused Result Plot Controls

**Files:**

- Create all five `csharp/src/IPCE.Desktop/Views/Plots/*PlotView.xaml(.cs)` pairs
  listed in File Structure.
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

**Interfaces:**

Each control exposes one render method:

```csharp
public void Render(PlotModel model);
```

`SpectrumIntegrationPlotView` additionally accepts:

```csharp
public void Render(
    PlotModel irradiance,
    PlotModel selectedIpce,
    PlotModel cumulative,
    IntegrationSummary? summary);
```

- [ ] **Step 1: Write live-shell plot RED tests**

Extend the STA smoke test to require named controls:

```text
SiliconTraceView
SampleTraceView
SchedulePlotView
PowerDensityPlotView
IpcePlotView
SpectrumIntegrationPlotView
```

Load synthetic session data and assert each corresponding ScottPlot contains
at least one plottable after property notifications are pumped.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "PlotRenderingTests|MainWindowSmokeTests"
```

Expected: failure because only the two current trace plots exist.

- [ ] **Step 3: Implement `TracePlotView`**

Render raw i-t, dark interval as a vertical span, first reference line, anchor
times, actual averaging windows, and window means. Keep:

- wheel zoom;
- left-drag pan;
- double-click `Axes.AutoScale()`;
- hover coordinate text;
- explicit “新增锚点”, “复位视图”, and “保存图像” buttons;
- layer checkboxes.

The click callback must snap through the owning ViewModel using original data,
not display-downsampled points.

- [ ] **Step 4: Implement `SchedulePlotView`**

Render silicon/sample schedule lines and anchors. Add horizontal data-coverage
boundaries. Split a schedule into valid and invalid series so points outside
the trace range are red. When preview generation fails, show the Chinese
validation message inside the control without throwing.

- [ ] **Step 5: Implement `PowerDensityPlotView` and `IpcePlotView`**

Power density uses wavelength and
`IncidentPowerDensityWattsPerSquareCentimetre * 1e6`, labelled
`µW cm⁻²`; optional standard errors use the same conversion.

IPCE renders calculated and external series simultaneously, with independent
visibility checkboxes and a visible badge for the selected integration source.
Do not clip external values.

- [ ] **Step 6: Implement `SpectrumIntegrationPlotView`**

Use a right Y axis for IPCE and left Y axis for irradiance. Render a translucent
integration-range span. Render cumulative current density below and emphasize
the final point. Show the summary value in `mA cm⁻²`.

- [ ] **Step 7: Rebuild `ResultTabs` around the confirmed layout**

Tabs:

```text
i-t 轨迹 | 时间调度 | 功率密度 | IPCE | 光谱积分 | 结果摘要
```

Each result tab places the plot first and a collapsible table below. Define
explicit Chinese table columns and units. Move trace/axis code out of the old
`ResultTabs.xaml.cs`; leave it only as the session-subscription coordinator.

- [ ] **Step 8: Add one shared plot toolbar**

`PlotToolbar` contains X/Y minimum and maximum fields, X/Y logarithmic toggles,
Apply, Reset, and Save Image. It raises:

```csharp
public event EventHandler<PlotViewSettings>? ApplyRequested;
public event EventHandler? ResetRequested;
public event EventHandler? SaveImageRequested;
```

Every plot view validates settings before applying them. Reset restores exact
current-data limits. Save Image uses a PNG save dialog and the current plot
dimensions. Invalid settings show an expected warning and preserve the prior
view.

- [ ] **Step 9: Refresh on every relevant state/status property**

Refresh narrowly:

- trace/anchor/preview changes refresh trace and schedule views;
- power or power status refreshes power;
- either IPCE or source change refreshes IPCE;
- spectrum, source, integration, bounds, or integration status refreshes
  spectrum/integration.

Do not reset user zoom on unrelated property changes.

- [ ] **Step 10: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all tests pass.

---

### Task 4: Reproducible Settings, Anchor, and Input-Metadata Export

**Files:**

- Create: `csharp/src/IPCE.Desktop/ViewModels/WorkflowExportSnapshot.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/ReproducibleExportTests.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/WorkflowExportTables.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Interfaces:**

```csharp
public sealed record SettingEntry(
    string Parameter,
    string Value,
    string Unit);

public sealed record InputMetadataEntry(
    string Dataset,
    string FileName,
    string Column1Header,
    string Column2Header,
    string SourceUnits,
    string CanonicalUnits,
    string Selection);

public sealed record WorkflowExportSnapshot(
    IReadOnlyList<SettingEntry> Settings,
    IReadOnlyList<AnchorPoint> SiliconAnchors,
    IReadOnlyList<AnchorPoint> SampleAnchors,
    IReadOnlyList<InputMetadataEntry> Inputs);
```

Factories:

```csharp
WorkflowExportTables.MeasurementSettings(snapshot.Settings);
WorkflowExportTables.Anchors("SiliconAnchors", snapshot.SiliconAnchors);
WorkflowExportTables.Anchors("SampleAnchors", snapshot.SampleAnchors);
WorkflowExportTables.InputMetadata(snapshot.Inputs);
```

- [ ] **Step 1: Write reproducibility RED tests**

Build a complete workflow with non-default silicon/sample areas, dark ranges,
Delay, anchors, source units, spectrum sheet/columns, and external-IPCE source.
For XLSX, CSV, and MAT, assert existing result table names remain and these
new names are present:

```text
MeasurementSettings
SiliconAnchors
SampleAnchors
InputMetadata
```

Assert exact parameter/value/unit cells for area `0.36` / `cm2`, dark range
`50–60` / `s`, source `sec/uA`, canonical `s/A`, sheet `Spectra`, columns
`A/C`, and selected IPCE source.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter ReproducibleExportTests
```

Expected: failure because snapshot tables do not exist.

- [ ] **Step 3: Retain successful source metadata**

Store only file names, not mandatory absolute paths, in ViewModels. Preserve
trace headers/units from `TraceMetadata`, and spectrum worksheet/column
selection from the import result. Do not include raw trace contents in the
metadata table.

- [ ] **Step 4: Build invariant snapshot entries**

Format numeric `Value` fields with `"G17"` and invariant culture. Use explicit
units (`nm`, `s`, `cm2`) rather than embedding units in parameter names.
Include each result's `Current/Missing/Stale` status and reason.

- [ ] **Step 5: Append snapshot tables without renaming results**

Only include anchor tables when their owner has anchors. Always include
`MeasurementSettings` and `InputMetadata` when any current result is exported.
Continue to reject an export containing only stale/missing selected results.

- [ ] **Step 6: Run focused and full export GREEN**

Run Step 2, all `ExportServiceTests`, and all `EndToEndWorkflowTests`.
Expected: all pass in XLSX, CSV, and MAT.

---

### Task 5: Recoverable Real-UI Workflow and Compiled Smoke Coverage

**Files:**

- Create: `csharp/tests/IPCE.Desktop.Tests/RecoverableUiWorkflowTests.cs`
- Modify: `csharp/src/IPCE.Desktop/App.xaml.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Interfaces:**

- `--smoke-test` must exercise both the default measurement prerequisite path
  and independent external post-processing, while remaining non-interactive.

- [ ] **Step 1: Write a real-window recovery RED test**

On one STA dispatcher:

1. Show the real window.
2. Load defaults and calculate power density.
3. Set a sample schedule outside trace coverage and invoke calculation.
4. Assert warning is shown and the window remains visible.
5. Correct the range and calculate successfully.
6. Change sample area and assert calculated IPCE becomes stale.
7. Assert export is unavailable until recalculation.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter RecoverableUiWorkflowTests
```

Expected: one or more recovery/staleness assertions fail before final wiring.

- [ ] **Step 3: Extend compiled smoke mode**

In addition to existing defaults and 161-point power calculation:

- create a synthetic sample trace and calculate IPCE;
- load embedded or generated external IPCE;
- select external source and integrate with embedded spectrum;
- validate every required plot control exists;
- build all selected export tables in memory;
- close and exit 0.

Smoke mode must not show dialogs or write user export files.

- [ ] **Step 4: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all pass.

---

### Task 6: Documentation, Full Regression, and Portable Release

**Files:**

- Modify: `README_CN.md`
- Modify: `csharp/PORTABLE_README_CN.txt`
- Modify: `csharp/scripts/build-portable.ps1` only if the extended smoke command requires changed arguments.
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Generate: `csharp/dist/IPCEApp_Windows_x64.zip`
- Generate: `csharp/dist/IPCEApp_Windows_x64.build.json`

- [ ] **Step 1: Update Chinese workflows**

Document:

- missing-unit selection;
- spectrum worksheet/column selection;
- four external-IPCE formats;
- editable anchors and graph selection;
- stale-result behavior;
- every result plot and its interaction;
- recoverable error messages;
- new reproducibility export tables.

Remove any statement that implies the current incomplete C# UI behavior.

- [ ] **Step 2: Run fresh .NET build and tests**

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Expected: zero warnings/errors and every test passes. Record exact project
counts.

- [ ] **Step 3: Run original MATLAB regression and UI smoke**

```powershell
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: all MATLAB self-tests pass and the original UI constructs and closes.

- [ ] **Step 4: Build the portable archive**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File csharp/scripts/build-portable.ps1
```

Expected gates:

1. MATLAB regression passes.
2. All .NET tests pass.
3. Self-contained `win-x64` publish succeeds.
4. Published and extracted `IPCEApp.exe --smoke-test` exit 0.
5. Archive contains root `IPCEApp.exe`, Chinese README, and notices.
6. No MATLAB Runtime marker exists.
7. ZIP size is below `200 * 1024 * 1024`.

- [ ] **Step 5: Record exact artifact evidence**

Record archive path, byte size, SHA-256, entry count, smoke exit codes, and
test counts in the progress document and build JSON.

- [ ] **Step 6: Run manual development-machine UI acceptance**

Perform the nine scenarios in design section 14.2. Record each as pass/fail,
including the exact range error used to prove recovery.

- [ ] **Step 7: Preserve the clean-Windows gate**

Do not mark migration complete until the new archive passes on a Windows 10/11
x64 computer or VM with neither MATLAB nor .NET Runtime installed. Record OS
build, archive hash, startup, both workflows, three export formats, and close.
