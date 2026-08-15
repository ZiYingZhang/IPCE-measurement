# IPCE C# Plot Readability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every C# plot comfortably readable, robust to leakage-current
outliers, inspectable by nearest-point hover, and diagnostically complete with
actual mean-current windows and visible dark-current intervals.

**Architecture:** Add pure viewport, hit-test, and trace-overlay builders below
the WPF layer, then let one reusable interaction controller apply those models
to every ScottPlot control. Scientific extraction remains in Core, and the
display overlay reuses the exact same average-window resolver as the
calculation so plotted windows cannot drift from calculated windows.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, ScottPlot.WPF 5.1.59, MSTest 4,
existing immutable plot models and workflow ViewModels.

## Global Constraints

- Do not change power-density, IPCE, interpolation, or integration formulas.
- Robust ranges affect display only; calculations and exports retain every
  finite source point.
- Default robust Y range is the 0.5th–99.5th percentile with 8% padding.
- Nearest-point hover activates within 12 device-independent pixels.
- i-t defaults to the full time range; spectrum/integration defaults to the
  selected common integration range.
- Font sizes are title 18, axis label 15, tick/legend 13, hover 14, and toolbar
  at least 14 display-independent units.
- Dark overlays are rendered only when dark subtraction is enabled.
- All WPF tests run serially; do not launch two builds/tests of the same WPF
  project concurrently.
- The directory is not a Git repository. Do not initialize Git or add commit
  steps.
- Every production behavior change requires a focused failing test first.

---

## File Structure

**Create**

- `csharp/src/IPCE.Desktop/Plotting/PlotViewport.cs` — immutable viewport
  policy/result models and pure robust/full-range calculation.
- `csharp/src/IPCE.Desktop/Plotting/PlotHitTester.cs` — pure pixel-distance
  nearest-point selection.
- `csharp/src/IPCE.Desktop/Plotting/TraceOverlayBuilder.cs` — joins schedule
  windows to retained mean-current results.
- `csharp/src/IPCE.Desktop/Plotting/PlotInteractionController.cs` — reusable
  WPF mouse, hover, Reset, Show All, and manual-axis coordinator.
- `csharp/src/IPCE.Core/Extraction/AverageWindowResolver.cs` — single source
  of truth for fixed-delay and anchor averaging windows.
- `csharp/tests/IPCE.Desktop.Tests/PlotViewportTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/PlotHitTesterTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/TraceOverlayTests.cs`
- `csharp/tests/IPCE.Core.Tests/AverageWindowResolverTests.cs`

**Modify**

- `csharp/src/IPCE.Core/Extraction/TraceExtractor.cs`
- `csharp/src/IPCE.Desktop/Plotting/PlotModels.cs`
- `csharp/src/IPCE.Desktop/Plotting/PlotTheme.cs`
- `csharp/src/IPCE.Desktop/Plotting/PlotModelRenderer.cs`
- `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml`
- `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml.cs`
- all five `csharp/src/IPCE.Desktop/Views/Plots/*PlotView.xaml(.cs)` pairs
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- `README_CN.md`
- `csharp/PORTABLE_README_CN.txt`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

---

### Task 1: Pure Robust and Full Viewport Calculation

**Files:**

- Create: `csharp/src/IPCE.Desktop/Plotting/PlotViewport.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/PlotViewportTests.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/PlotModels.cs`

**Interfaces:**

```csharp
public enum PlotViewportMode
{
    Robust,
    Full,
}

public sealed record PlotViewportPolicy(
    double LowerQuantile = 0.005,
    double UpperQuantile = 0.995,
    double PaddingFraction = 0.08,
    double? PreferredMinimumX = null,
    double? PreferredMaximumX = null);

public readonly record struct PlotViewport(
    double MinimumX,
    double MaximumX,
    double MinimumY,
    double MaximumY,
    int ClippedYPointCount);

public static class PlotViewportCalculator
{
    public static PlotViewport Calculate(
        PlotModel model,
        PlotViewportPolicy policy,
        PlotViewportMode mode);
}
```

Extend the `PlotSeries` constructor with an optional final argument:

```csharp
bool contributesToAutoRange = true
```

and expose:

```csharp
public bool ContributesToAutoRange { get; }
```

Extend `PlotModel` with optional:

```csharp
IReadOnlyList<PlotIntervalMarker>? intervals = null,
PlotViewportPolicy? viewportPolicy = null
```

- [ ] **Step 1: Write RED tests for leakage-current robust range**

Create a primary i-t series containing 9,980 values between `-2e-5` and
`-1e-5` plus 20 leakage values near `-3e-4`. Assert:

```csharp
PlotViewport robust = PlotViewportCalculator.Calculate(
    model,
    new PlotViewportPolicy(),
    PlotViewportMode.Robust);
PlotViewport full = PlotViewportCalculator.Calculate(
    model,
    new PlotViewportPolicy(),
    PlotViewportMode.Full);

Assert.IsTrue(robust.MinimumY > -1e-4);
Assert.IsTrue(robust.ClippedYPointCount >= 20);
Assert.IsTrue(full.MinimumY <= -3e-4);
Assert.AreEqual(0, full.ClippedYPointCount);
```

Add separate tests for constant Y, a two-point series, overlay series with
`ContributesToAutoRange == false`, and immutable source arrays.

- [ ] **Step 2: Write RED test for preferred integration X range**

Use spectrum X data `280–4000` with policy `300–600`. Assert the viewport X
limits surround `300–600` and do not extend to 4000, while full mode still
uses the preferred X interval because it is the business focus, not an
outlier filter.

- [ ] **Step 3: Run RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter PlotViewportTests
```

Expected: compile failure because viewport types and the new plot-model
members do not exist.

- [ ] **Step 4: Implement quantile and range calculation**

Use linear interpolation:

```csharp
private static double Quantile(double[] sorted, double probability)
{
    double position = probability * (sorted.Length - 1);
    int lower = (int)Math.Floor(position);
    int upper = (int)Math.Ceiling(position);
    double fraction = position - lower;
    return sorted[lower] +
        fraction * (sorted[upper] - sorted[lower]);
}
```

Filter primary Y values to points whose X is inside the preferred X range.
For robust mode calculate quantiles; for full mode use finite min/max. Add
padding equal to `span * PaddingFraction` on both sides. For a constant range,
use `max(abs(value) * 0.05, 1e-12)`. Count primary in-focus Y values strictly
outside the padded robust limits.

Validate `0 <= LowerQuantile < UpperQuantile <= 1`,
`PaddingFraction >= 0`, paired finite preferred X limits, and
`PreferredMaximumX > PreferredMinimumX`. Throw:

```csharp
new IpceException(
    "IPCE:InvalidViewportPolicy",
    "绘图视野参数无效。")
```

- [ ] **Step 5: Run focused GREEN**

Run the command in Step 3. Expected: all `PlotViewportTests` pass.

---

### Task 2: DPI-Readable Shared Plot Theme

**Files:**

- Modify: `csharp/src/IPCE.Desktop/Plotting/PlotTheme.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml`
- Modify: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`

**Interfaces:**

```csharp
public const float TitleFontSize = 18;
public const float AxisLabelFontSize = 15;
public const float TickFontSize = 13;
public const float LegendFontSize = 13;
public const double HoverFontSize = 14;
public const double ToolbarFontSize = 14;
```

- [ ] **Step 1: Add font-size RED assertions**

Extend `Theme_UsesChineseCapableFontForEveryTextSurface`:

```csharp
Assert.AreEqual(18, plot.Axes.Title.Label.FontSize);
Assert.AreEqual(15, plot.Axes.Bottom.Label.FontSize);
Assert.AreEqual(15, plot.Axes.Left.Label.FontSize);
Assert.AreEqual(13, plot.Axes.Bottom.TickLabelStyle.FontSize);
Assert.AreEqual(13, plot.Axes.Left.TickLabelStyle.FontSize);
Assert.AreEqual(13, plot.Legend.FontSize);
```

Also construct a right axis, call `PlotTheme.Apply`, and assert its label and
tick sizes/font names.

- [ ] **Step 2: Run RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter PlotRenderingTests
```

Expected: size assertions fail because the theme only assigns font names.

- [ ] **Step 3: Apply sizes and WPF toolbar font**

Set every visible axis label and tick style, including `Axes.Right`, in
`ApplyLabels`. Set:

```xml
<UserControl ... FontFamily="Microsoft YaHei UI" FontSize="14">
```

Increase toolbar textbox widths to at least `88` DIPs and button vertical
padding to `6` so 125%/150% scaling does not clip text.

- [ ] **Step 4: Run focused GREEN**

Run Step 2. Expected: all plot rendering tests pass.

---

### Task 3: Pure Pixel-Space Nearest-Point Hit Testing

**Files:**

- Create: `csharp/src/IPCE.Desktop/Plotting/PlotHitTester.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/PlotHitTesterTests.cs`

**Interfaces:**

```csharp
public readonly record struct PlotPixelPoint(double X, double Y);

public sealed record PlotHoverPoint(
    string SeriesLabel,
    int SeriesIndex,
    int PointIndex,
    double X,
    double Y,
    double PixelDistance,
    string Details);

public static class PlotHitTester
{
    public static PlotHoverPoint? FindNearest(
        PlotModel model,
        PlotPixelPoint pointer,
        Func<double, double, PlotPixelPoint> toPixel,
        double maximumDistancePixels = 12);
}
```

- [ ] **Step 1: Write multi-series RED tests**

Build two visible series with different numeric scales and provide a
deterministic `toPixel` lambda. Assert the pixel-nearest point wins even when
another point is numerically closer in data units. Assert exact series/point
indices and original X/Y.

- [ ] **Step 2: Write radius and visibility RED tests**

Assert no hit at `12.01` pixels, a hit at `12`, hidden/non-primary overlay
series are excluded only when the caller filters them out of the immutable
model, and non-mutating repeated calls return the same result.

- [ ] **Step 3: Run RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter PlotHitTesterTests
```

Expected: compile failure because hit-test types do not exist.

- [ ] **Step 4: Implement one-pass nearest selection**

For every series point, map original X/Y through `toPixel`, skip non-finite
pixel coordinates, calculate `Math.Hypot(dx, dy)`, and retain the smallest
distance not exceeding the radius. Format default details as:

```csharp
$"{series.Label}\nX = {x:G8}\nY = {y:G8}"
```

Validate the radius is finite and positive; otherwise throw
`IPCE:InvalidHitTestRadius`.

- [ ] **Step 5: Run focused GREEN**

Run Step 3. Expected: all hit-test tests pass.

---

### Task 4: One Average-Window Resolver and Trace Overlay Builder

**Files:**

- Create: `csharp/src/IPCE.Core/Extraction/AverageWindowResolver.cs`
- Create: `csharp/tests/IPCE.Core.Tests/AverageWindowResolverTests.cs`
- Modify: `csharp/src/IPCE.Core/Extraction/TraceExtractor.cs`
- Create: `csharp/src/IPCE.Desktop/Plotting/TraceOverlayBuilder.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/TraceOverlayTests.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/PlotModels.cs`

**Interfaces:**

```csharp
public static class AverageWindowResolver
{
    public static (double Start, double End) Resolve(
        SchedulePoint point,
        double averagingDurationSeconds);
}

public sealed record TraceMeanResult(
    double WavelengthNm,
    double MeanCurrentAmperes,
    int SampleCount);

public sealed record PlotIntervalMarker(
    double MinimumX,
    double MaximumX,
    double Y,
    string Label,
    string ColorHex,
    string HoverDetails);

public static class TraceOverlayBuilder
{
    public static IReadOnlyList<PlotIntervalMarker> BuildMeans(
        SchedulePreview? preview,
        double averagingDurationSeconds,
        IReadOnlyList<TraceMeanResult> means);
}
```

- [ ] **Step 1: Write Core RED tests for exact averaging windows**

For fixed delay `[0, 10]` with duration `4`, assert `[6, 10]`. For an anchor
point with reference time `3`, window end `10`, and duration `4`, assert
`[3, 7]`. For duration `0`, assert the complete available window. Reproduce
all current invalid-duration and invalid-anchor errors.

- [ ] **Step 2: Run Core RED**

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj `
  -c Release --no-restore --filter AverageWindowResolverTests
```

Expected: compile failure because the resolver does not exist.

- [ ] **Step 3: Move, do not duplicate, window logic**

Move the existing private `TraceExtractor.GetAverageWindow` body into
`AverageWindowResolver.Resolve`. Replace the extractor call with:

```csharp
(double averageStart, double averageEnd) =
    AverageWindowResolver.Resolve(point, averagingDurationSeconds);
```

Delete the old private method.

- [ ] **Step 4: Run Core GREEN**

Run Step 2 plus all Core tests. Expected: numerical extraction tests remain
unchanged and green.

- [ ] **Step 5: Write Desktop trace-overlay RED tests**

Use two schedule points and means in reverse order. Assert the builder matches
by wavelength, produces exact resolved start/end, preserves mean current and
sample count in hover details, uses one common label `平均电流`, and rejects
duplicate or unmatched wavelengths with `IPCE:InvalidTraceOverlay`.

- [ ] **Step 6: Run Desktop RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter TraceOverlayTests
```

Expected: compile failure because overlay types do not exist.

- [ ] **Step 7: Implement immutable intervals**

Copy interval collections in `PlotModel`. Validate finite values,
`MaximumX > MinimumX`, non-empty color, and finite Y. In
`TraceOverlayBuilder`, create:

```csharp
new PlotIntervalMarker(
    start,
    end,
    mean.MeanCurrentAmperes,
    "平均电流",
    "#EF6C00",
    $"波长：{mean.WavelengthNm:G8} nm\n" +
    $"平均窗口：{start:G8}–{end:G8} s\n" +
    $"平均电流：{mean.MeanCurrentAmperes:E6} A\n" +
    $"样本数：{mean.SampleCount}")
```

- [ ] **Step 8: Run Desktop GREEN**

Run Step 6. Expected: all overlay tests pass.

---

### Task 5: Shared WPF Interaction Controller

**Files:**

- Create: `csharp/src/IPCE.Desktop/Plotting/PlotInteractionController.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/PlotModelRenderer.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml.cs`
- Modify: all five `Views/Plots/*PlotView.xaml(.cs)` pairs
- Modify: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

**Interfaces:**

```csharp
public sealed class PlotInteractionController
{
    public PlotInteractionController(
        ScottPlot.WPF.WpfPlot plot,
        TextBlock hoverText,
        TextBlock clippedText);

    public PlotModel? Model { get; }
    public PlotViewportMode ViewportMode { get; }
    public void Render(PlotModel model);
    public void Apply(PlotViewSettings settings);
    public void Reset();
    public void ShowAll();
    public void HandleMouseMove(MouseEventArgs eventArgs);
    public void ClearHover();
}
```

Add toolbar event:

```csharp
public event EventHandler? ShowAllRequested;
```

- [ ] **Step 1: Write renderer RED test for explicit viewport**

Change `PlotModelRenderer.Render` to accept:

```csharp
PlotViewport viewport
```

Render a leakage series with a robust viewport and assert
`plot.Axes.GetLimits()` exactly matches it. The test must fail while the
renderer still calls unconditional `AutoScale()`.

- [ ] **Step 2: Write WPF shell RED assertions**

In the single existing STA `Application` smoke lifecycle, assert every plot
view contains:

- a hover text element with `FontSize >= 14`;
- a clipped-point status element;
- a toolbar Show All button;
- mouse-move and mouse-leave interaction through the controller.

Do not create a second `Application` in a separate test.

- [ ] **Step 3: Run focused RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore `
  --filter "PlotRenderingTests|MainWindowSmokeTests"
```

Expected: viewport assertion and named-control assertions fail.

- [ ] **Step 4: Make renderer obey calculated limits**

Remove unconditional `Axes.AutoScale()`. Render bands first, primary series,
and interval markers. For every interval add one orange line from
`MinimumX` to `MaximumX` at `Y`, using line width `5` and one midpoint marker.
Deduplicate legend text so “平均电流” appears once.

Render bands with a translucent span plus:

```csharp
var left = plot.Add.VerticalLine(band.MinimumX);
var right = plot.Add.VerticalLine(band.MaximumX);
left.Color = right.Color = ScottPlot.Color.FromHex(band.ColorHex);
left.LineWidth = right.LineWidth = 2;
```

For a non-empty band label, add one text annotation at the horizontal midpoint
and `viewport.MaximumY - 0.05 * viewportHeight`, using
`Microsoft YaHei UI`, font size 13, and the band color. This keeps
“暗电流区间” readable even when the filled span is narrower than one pixel.

Apply the supplied viewport using `Axes.SetLimits`.

- [ ] **Step 5: Implement controller viewport commands**

`Render` calculates robust limits from `model.ViewportPolicy` and records
manual settings as null. `Reset` returns to robust mode. `ShowAll` selects full
mode. `Apply` validates settings, overlays explicit limits on the current
calculated viewport, and preserves the prior limits if validation throws.

Set clipped status exactly:

```text
默认显示主体范围；视野外 20 个极端点。可点“显示全部”。
```

Hide it when the count is zero or mode is Full.

- [ ] **Step 6: Implement hover marker, crosshair, and tooltip**

On mouse move, use `PlotHitTester.FindNearest` with a mapper backed by the
current ScottPlot axes. Add/reuse one marker, one vertical line, and one
horizontal line; set them visible at the selected original coordinate and
set hover text to `PlotHoverPoint.Details`. Use font size 14 and a high
contrast white panel with dark border. On mouse leave or no hit, hide all four
objects and refresh without changing limits.

For interval markers, compare the pointer to the segment midpoint and segment
line in pixel space; when closer than a raw-series point, show the interval's
structured hover details.

- [ ] **Step 7: Route every view through the controller**

Replace duplicated Apply/Reset/Save logic in Trace, Schedule, PowerDensity,
IPCE, and SpectrumIntegration views. Add `MouseMove` and `MouseLeave` handlers
to every contained `WpfPlot`, including all three spectrum/integration plots.
Keep Save PNG behavior unchanged.

- [ ] **Step 8: Make trace layer checkboxes functional**

Name the checkboxes:

```text
RawTraceLayerBox
DarkLayerBox
DiagnosticLayerBox
```

On checked/unchecked, create a filtered immutable model:

- raw unchecked removes the raw series;
- dark unchecked removes dark bands;
- diagnostic unchecked removes anchors and mean intervals.

Re-render through the controller without touching session state.

- [ ] **Step 9: Run focused and full Desktop GREEN**

Run Step 3, then:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore
```

Expected: all Desktop tests pass serially.

---

### Task 6: Build Correct Business-Focused Models

**Files:**

- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/SpectrumIntegrationPlotView.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/SpectrumIntegrationPlotView.xaml.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Consumes:**

- `PlotViewportPolicy`
- `TraceOverlayBuilder.BuildMeans`
- `PlotIntervalMarker`
- `PlotInteractionController`

- [ ] **Step 1: Write trace-model RED tests**

Extract or expose an internal pure `ResultPlotModelBuilder`. For silicon and
sample separately assert:

- raw trace contributes to auto range;
- anchors do not contribute;
- `SubtractDark == true` produces one band with configured bounds;
- `SubtractDark == false` produces no band;
- current results plus preview produce one mean interval per wavelength;
- each mean segment has the exact actual resolved averaging window.
- a stale result labels its interval legend and hover details with
  `结果已过期`.

- [ ] **Step 2: Write integration-focus RED tests**

With spectrum 280–4000 nm, IPCE 300–600 nm, and requested integration
300–600 nm, assert irradiance and selected-IPCE policies use preferred X
`300–600`; cumulative uses its result curve range.

- [ ] **Step 3: Run RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore `
  --filter "PlotRenderingTests|EndToEndWorkflowTests"
```

Expected: model-policy, mean-interval, and dark-disabled assertions fail.

- [ ] **Step 4: Implement `ResultPlotModelBuilder`**

Create trace mean inputs from:

```csharp
power.Select(point => new TraceMeanResult(
    point.WavelengthNm,
    point.SiliconMeanCurrentAmperes,
    point.SampleCount))
```

and:

```csharp
ipce.Select(point => new TraceMeanResult(
    point.WavelengthNm,
    point.SampleMeanCurrentAmperes,
    point.SampleCount))
```

Pass workflow `AveragingDurationSeconds` and `Preview` to the overlay builder.
Only add a dark band when `SubtractDark` is true. Mark anchors and mean
markers as non-primary overlays. Pass the corresponding `ResultStatus`; when
its freshness is `Stale`, change the interval label to
`平均电流（结果已过期）` and append `状态：结果已过期` to hover details.

- [ ] **Step 5: Apply policies to all result models**

- trace: default policy with full trace X;
- schedule: full wavelength X;
- power/IPCE: full visible wavelength X;
- irradiance/selected IPCE:
  `PreferredMinimumX = max(requestedMinimum, commonMinimum)` and
  `PreferredMaximumX = min(requestedMaximum, commonMaximum)`;
- cumulative: curve min/max.

Do not modify source arrays or exported data.

- [ ] **Step 6: Improve spectrum layout**

Keep irradiance, selected IPCE, and cumulative Jsc as three readable panels,
but give each its own hover overlay and clipped status. At 16:9 window sizes,
use a minimum plot height of `220` DIPs and place axis units outside the data
rectangle without overlap.

- [ ] **Step 7: Run focused and full GREEN**

Run Step 3 and the full Desktop suite. Expected: all tests pass.

---

### Task 7: Final Regression, Visual Acceptance, and Portable Release

**Files:**

- Modify: `README_CN.md`
- Modify: `csharp/PORTABLE_README_CN.txt`
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Generate: `csharp/dist/IPCEApp_Windows_x64.zip`
- Generate: `csharp/dist/IPCEApp_Windows_x64.build.json`

- [ ] **Step 1: Update Chinese documentation**

Document robust/default versus Show All, clipped-point badge, nearest-point
hover, mean-current windows, dark-current boundaries, layer controls, and the
new shared font sizes. State explicitly that display filtering never changes
calculation or export values.

- [ ] **Step 2: Run fresh Release build**

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Run all .NET tests**

```powershell
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Expected: Core, IO, and Desktop all pass with zero failures/skips.

- [ ] **Step 4: Run MATLAB regression and UI smoke**

```powershell
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: all MATLAB tests pass and the original UI constructs/closes.

- [ ] **Step 5: Run visual acceptance on development Windows**

Use the supplied real silicon and sample traces and check:

1. title/axis/tick/legend readability at 100%, 125%, and 150% Windows scaling;
2. default i-t shows the main signal shape despite leakage steps;
3. clipped badge count is nonzero and Show All reveals every extreme;
4. Reset returns to the robust range;
5. hover snaps to original raw points on every plot and clears on leave;
6. mean-current segments align with visible averaging windows;
7. mean hover shows wavelength/window/current/sample count;
8. dark band and both boundaries remain visible at 0.1–10 s over 0–1400 s;
9. integration plots focus on the selected range rather than 280–4000 nm.

- [ ] **Step 6: Rebuild and verify portable archive**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File csharp/scripts/build-portable.ps1
```

Expected: MATLAB and .NET gates pass, published and extracted smoke exit 0,
ZIP remains under `200 * 1024 * 1024` bytes, and no MATLAB Runtime marker is
present.

- [ ] **Step 7: Record evidence**

Append exact test counts, archive bytes, SHA-256, entry count, both smoke exit
codes, and visual acceptance observations to the progress document and build
JSON. Preserve the external clean-Windows acceptance gate.
