# IPCE C# Large Plot Text and Region Styling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enlarge all scientific plot text, make dark-current and integration
regions visually prominent, start the sample workflow in anchor mode, and
re-verify canonical i-t unit conversion.

**Architecture:** Keep the existing shared `PlotTheme`, immutable `PlotBand`,
`ResultPlotModelBuilder`, and `PlotModelRenderer` boundaries. Change only
shared theme constants, the sample ViewModel's initial state, approved band
style values, and the renderer's common boundary width; numerical import and
calculation code remains unchanged.

**Tech Stack:** C# 14, .NET 10 WPF, ScottPlot 5.1.59, MSTest 4.3.2,
MATLAB R2023b regression suite, PowerShell portable-build pipeline.

## Global Constraints

- Work in the current renamed source directory: `csharp APP`.
- This directory is not a Git repository. Do not initialize Git, run Git
  commands, create commits, or claim commits; use test/review checkpoints.
- Tick labels and legends use size `20`; X/Y axis names use `24`; plot titles
  use `26`.
- Hover, toolbar, layer-control, and clipped-status text remain size `14`.
- Dark-current opacity is `0.28`; integration-range opacity is `0.24`;
  region-boundary width is `3`.
- The sample workflow starts in `AlignmentMode.Anchors`.
- i-t internal units remain `s/A`; never infer missing units from magnitude.
- Supported source time units remain `s`, `sec`, `second`, `ms`, `min`, `h`.
- Supported source current units remain `A`, `mA`, `uA`, `µA`, `μA`, `nA`,
  `pA`.
- Display changes must not modify calculation, integration, or export values.
- Run WPF tests serially to avoid temporary XAML-project collisions.

---

### Task 1: Enlarge the Shared Plot Typography

**Files:**

- Modify: `csharp APP/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Modify: `csharp APP/src/IPCE.Desktop/Plotting/PlotTheme.cs`

**Interfaces:**

- Consumes: `PlotTheme.Apply(ScottPlot.Plot plot)`
- Produces: shared literal sizes used by every result plot:
  `TitleFontSize = 26`, `AxisLabelFontSize = 24`,
  `TickFontSize = 20`, `LegendFontSize = 20`

- [ ] **Step 1: Extend the existing real-theme test with literal behavioral requirements**

In
`Theme_UsesChineseCapableFontForEveryTextSurface`, retain the existing font
and constant-to-property assertions, then add independently derived literal
checks:

```csharp
Assert.IsTrue(plot.Axes.Bottom.TickLabelStyle.FontSize >= 20);
Assert.IsTrue(plot.Axes.Left.TickLabelStyle.FontSize >= 20);
Assert.IsTrue(plot.Axes.Right.TickLabelStyle.FontSize >= 20);
Assert.IsTrue(plot.Legend.FontSize >= 20);
Assert.IsTrue(
    plot.Axes.Bottom.Label.FontSize >=
    1.2 * plot.Axes.Bottom.TickLabelStyle.FontSize);
Assert.IsTrue(
    plot.Axes.Left.Label.FontSize >=
    1.2 * plot.Axes.Left.TickLabelStyle.FontSize);
Assert.IsTrue(plot.Axes.Title.Label.FontSize >= 26);
```

This test catches a regression that makes rendered tick/legend text smaller
than the approved visual baseline or makes axis names less prominent.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test "csharp APP/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" `
  -c Release --no-restore `
  --filter Theme_UsesChineseCapableFontForEveryTextSurface
```

Expected: FAIL because the rendered values are currently title `18`, axis
label `15`, tick `13`, and legend `13`.

- [ ] **Step 3: Apply the approved sizes in the shared theme**

Change only the constants in `PlotTheme.cs`:

```csharp
public const float TitleFontSize = 26;
public const float AxisLabelFontSize = 24;
public const float TickFontSize = 20;
public const float LegendFontSize = 20;
public const double HoverFontSize = 14;
public const double ToolbarFontSize = 14;
```

Do not add per-view overrides. `ApplyLabels` must continue assigning the same
constants to bottom, left, top, and right axes plus the legend.

- [ ] **Step 4: Run focused GREEN**

Run the command from Step 2.

Expected: PASS with the larger sizes visible on the real ScottPlot text
surfaces.

- [ ] **Step 5: Review checkpoint**

Inspect `PlotTheme.cs` and confirm there is still one shared source of truth
and no changes to plot data, viewports, or exports.

---

### Task 2: Make Anchor Alignment the Sample Default

**Files:**

- Modify:
  `csharp APP/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- Modify:
  `csharp APP/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- Modify:
  `csharp APP/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`

**Interfaces:**

- Consumes: `SampleWorkflowViewModel.AlignmentMode`
- Produces: a new sample ViewModel whose initial alignment mode is
  `AlignmentMode.Anchors`

- [ ] **Step 1: Add a startup-default assertion**

In `StartupDefaults_LoadAllFourIndependentInputs`, add:

```csharp
Assert.AreEqual(
    AlignmentMode.Anchors,
    viewModel.Sample.AlignmentMode);
```

In `SampleAndIntegrationParameters_UseNarrowInvalidation`, change the
time-alignment mutation so it remains a real state change after the new
default:

```csharp
("时间对齐", () =>
    sample.AlignmentMode = AlignmentMode.FixedDelay),
```

The first assertion catches the wrong startup default. The second preserves
the existing invalidation test's intent rather than assigning the default
value back to itself.

- [ ] **Step 2: Run focused tests and verify RED**

Run serially:

```powershell
dotnet test "csharp APP/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" `
  -c Release --no-restore `
  --filter "StartupDefaults_LoadAllFourIndependentInputs|SampleAndIntegrationParameters_UseNarrowInvalidation"
```

Expected: FAIL because a new sample ViewModel currently starts in
`FixedDelay`; the invalidation case also observes no change.

- [ ] **Step 3: Change only the initial sample state**

In `SampleWorkflowViewModel.cs`, replace:

```csharp
private AlignmentMode _alignmentMode = AlignmentMode.FixedDelay;
```

with:

```csharp
private AlignmentMode _alignmentMode = AlignmentMode.Anchors;
```

Do not change the setter, scheduling algorithm, import behavior, or compiled
smoke fixture. The smoke fixture already assigns `FixedDelay` explicitly when
it needs a synthetic fixed-delay trace.

- [ ] **Step 4: Run focused GREEN**

Run the command from Step 2.

Expected: both tests PASS; startup is Anchors and switching to FixedDelay
still marks only the sample measurement chain stale.

- [ ] **Step 5: Review checkpoint**

Confirm no code resets `AlignmentMode` after construction or after anchor
import, so explicit user selections remain authoritative.

---

### Task 3: Strengthen Dark-Current and Integration Regions

**Files:**

- Modify:
  `csharp APP/tests/IPCE.Desktop.Tests/ResultPlotModelBuilderTests.cs`
- Modify:
  `csharp APP/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Modify:
  `csharp APP/src/IPCE.Desktop/Plotting/ResultPlotModelBuilder.cs`
- Modify:
  `csharp APP/src/IPCE.Desktop/Plotting/PlotModelRenderer.cs`

**Interfaces:**

- Consumes: existing `PlotBand.Opacity`, `PlotBand.ColorHex`, and
  `PlotModelRenderer.Render`
- Produces: dark bands with opacity `0.28`, integration bands with opacity
  `0.24`, and two rendered boundary lines at width `3`

- [ ] **Step 1: Add model-level RED assertions for both region types**

In `BuildTrace_SeparatesPrimaryDiagnosticsDarkAndMeanLayers`, after the
existing dark-band range assertions, add:

```csharp
Assert.AreEqual(0.28, enabled.Bands[0].Opacity, 1e-12);
Assert.AreEqual("#607D8B", enabled.Bands[0].ColorHex);
```

In `BuildSpectrumIntegration_FocusesOnCommonRequestedRange`, add:

```csharp
Assert.AreEqual(1, models.Irradiance.Bands.Count);
Assert.AreEqual(0.24, models.Irradiance.Bands[0].Opacity, 1e-12);
Assert.AreEqual("#90CAF9", models.Irradiance.Bands[0].ColorHex);
Assert.AreEqual(1, models.SelectedIpce.Bands.Count);
Assert.AreEqual(0.24, models.SelectedIpce.Bands[0].Opacity, 1e-12);
```

These assertions catch weak or missing scientific-region styling without
testing ScottPlot's own alpha implementation.

- [ ] **Step 2: Add a renderer-level RED assertion for visible boundaries**

In the STA body of
`Renderer_UsesExplicitViewportInsteadOfUnconditionalAutoscale`, give the
model one band:

```csharp
[
    new PlotBand(0.1, 0.3, "暗电流区间", "#607D8B", 0.28),
],
```

After `PlotModelRenderer.Render`, inspect the real rendered boundary objects:

```csharp
ScottPlot.Plottables.VerticalLine[] boundaries = target.Plot
    .GetPlottables<ScottPlot.Plottables.VerticalLine>()
    .ToArray();
Assert.AreEqual(2, boundaries.Length);
Assert.IsTrue(boundaries.All(line => line.LineWidth == 3));
```

This catches a renderer that keeps thin boundaries even when the model is
correct.

- [ ] **Step 3: Run focused tests and verify RED**

Run:

```powershell
dotnet test "csharp APP/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" `
  -c Release --no-restore `
  --filter "ResultPlotModelBuilderTests|Renderer_UsesExplicitViewportInsteadOfUnconditionalAutoscale"
```

Expected: FAIL because both model bands currently use opacity `0.18` and
renderer boundaries use width `2`.

- [ ] **Step 4: Apply the approved model opacities**

In `ResultPlotModelBuilder.BuildTrace`, construct the dark band as:

```csharp
new PlotBand(
    darkStartSeconds,
    darkEndSeconds,
    "暗电流区间",
    "#607D8B",
    0.28)
```

In `BuildSpectrumIntegration`, construct the integration band as:

```csharp
new PlotBand(
    requestedMinimumNm,
    requestedMaximumNm,
    "积分范围",
    "#90CAF9",
    0.24)
```

Do not change the conditions deciding whether either band exists.

- [ ] **Step 5: Increase the common rendered boundary width**

In `PlotModelRenderer.Render`, replace:

```csharp
left.LineWidth = right.LineWidth = 2;
```

with:

```csharp
left.LineWidth = right.LineWidth = 3;
```

Keep `VerticalSpan` rendering, band legend text, colors, and viewport limits
unchanged.

- [ ] **Step 6: Run focused GREEN**

Run the command from Step 3.

Expected: all focused tests PASS.

- [ ] **Step 7: Run the full Desktop suite serially**

Run:

```powershell
dotnet test "csharp APP/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" `
  -c Release --no-restore
```

Expected: 97 tests PASS, 0 failed, 0 skipped. The count remains unchanged
because requirements were added to existing behavioral tests.

- [ ] **Step 8: Review checkpoint**

Confirm dark subtraction disabled still produces zero dark bands, integration
bands still cover only the requested interval, and no opacity or line-width
value enters calculation/export models.

---

### Task 4: Re-verify Units, Document the New Defaults, and Rebuild Release

**Files:**

- Verify:
  `csharp APP/src/IPCE.IO/Import/ItTraceReader.cs`
- Verify:
  `csharp APP/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`
- Modify: `README_CN.md`
- Modify: `csharp APP/PORTABLE_README_CN.txt`
- Modify:
  `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Generate: `csharp APP/dist/IPCEApp_Windows_x64.zip`
- Generate: `csharp APP/dist/IPCEApp_Windows_x64.build.json`

**Interfaces:**

- Consumes: existing `ItTraceReader.Read(string, UnitOverrides?)`
- Produces: fresh verification evidence and a rebuilt self-contained Windows
  package; no unit-conversion production changes

- [ ] **Step 1: Run the complete i-t importer test class**

Run:

```powershell
dotnet test "csharp APP/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" `
  -c Release --no-restore --filter ItTraceReaderTests
```

Expected: all `ItTraceReaderTests` PASS, including automatic time conversion
for `ms/min/h`, automatic current conversion for
`mA/uA/µA/μA/nA/pA`, combined `ms/mA` header metadata, explicit
`min/uA` overrides, and missing-unit rejection.

Do not modify `ItTraceReader.cs` unless this fresh verification exposes an
actual failing conversion.

- [ ] **Step 2: Update the Chinese user documentation**

In `README_CN.md` and `csharp APP/PORTABLE_README_CN.txt`, document:

- plot ticks and legends use size `20`;
- axis names use `24`, titles use `26`;
- sample time alignment starts in anchor mode;
- users may explicitly switch to fixed-delay mode;
- grey dark-current and blue integration regions are display-only;
- the importer automatically converts supported header units to `s/A`;
- missing units require an explicit selector and are never guessed from
  magnitude.

Do not describe these display filters as changes to calculation or export
values.

- [ ] **Step 3: Run a fresh Release build**

Run:

```powershell
dotnet build "csharp APP/IPCE.slnx" -c Release --no-restore
```

Expected: exit code `0`, 0 warnings, 0 errors.

- [ ] **Step 4: Run all .NET tests from the built solution**

Run:

```powershell
dotnet test "csharp APP/IPCE.slnx" `
  -c Release --no-build --no-restore
```

Expected: 197 tests PASS, 0 failed, 0 skipped
(`58` Core, `42` IO, `97` Desktop).

- [ ] **Step 5: Run MATLAB numerical regression and UI smoke**

Run:

```powershell
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: all MATLAB self-tests PASS and `IPCEApp` constructs, validates, and
closes.

- [ ] **Step 6: Rebuild and validate the self-contained Windows package**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File "csharp APP/scripts/build-portable.ps1"
```

Expected:

- published executable smoke exit `0`;
- extracted-archive executable smoke exit `0`;
- archive smaller than 200 MiB;
- `matlabRuntimeIncluded` remains `false`;
- manifest test counts remain `197/58/42/97`.

- [ ] **Step 7: Record exact release evidence**

Read the generated manifest and record in
`docs/superpowers/progress/ipce-csharp-migration-progress.md`:

- build warning/error count;
- Core/IO/Desktop test counts;
- MATLAB self-test and UI-smoke result;
- archive bytes;
- SHA-256;
- archive entry count;
- published and extracted smoke exit codes;
- the remaining manual visual check at Windows scaling 100%, 125%, and 150%.

- [ ] **Step 8: Final visual handoff**

Provide the absolute link to
`csharp APP/dist/IPCEApp_Windows_x64.zip`. Ask the user to confirm on their
actual display that:

1. ticks and legends are at least as readable as “原始轨迹”;
2. axis names are visibly larger;
3. no essential units or legend entries are clipped;
4. the dark-current range is a clear grey region;
5. the integration range is a clear blue region;
6. the sample alignment selector initially shows “锚点”.

Do not claim physical-display acceptance before the user performs it.
