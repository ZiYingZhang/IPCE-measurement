# IPCE C# Large Plot Text and Region Styling Design

**Date:** 2026-07-29

**Status:** User-approved design

**Scope:** `csharp APP` Windows application typography, region styling,
sample alignment default, and i-t unit-conversion verification.

## 1. Objective

Make every scientific plot comfortably readable at the user's normal viewing
distance while preserving the current calculation, interaction, import, and
export behavior.

The visual target is:

- tick labels and legends must be at least as visually large as the WPF
  “原始轨迹” layer-control text below the plot;
- axis names must be approximately 20% larger than tick labels and legends;
- dark-current and integration ranges must be immediately recognizable as
  filled, bounded regions;
- sample time alignment must start in anchor mode.

## 2. Typography

The shared ScottPlot theme will use:

- tick labels: `20`;
- legend: `20`;
- X/Y axis names: `24`;
- plot title: `26`;
- hover text: retain `14`;
- toolbar, layer controls, and clipped-point status: retain `14`.

The font remains `Microsoft YaHei UI`. The same constants apply to every
result plot, including silicon/sample i-t, schedule, power density, IPCE,
solar irradiance, selected IPCE, and cumulative current density.

The larger text may reduce the data rectangle slightly. Readability takes
priority, but essential axis units and legend entries must remain visible
without overlap at the application's normal 16:9 window size.

## 3. Scientific Region Styling

### 3.1 Dark-current range

When dark-current subtraction is enabled:

- use a neutral grey-blue fill with opacity `0.28`;
- draw both vertical boundaries at line width `3`;
- retain the “暗电流区间” legend item;
- keep the existing independent trace-layer checkbox.

When dark-current subtraction is disabled, the region remains absent.

### 3.2 Integration range

For solar-spectrum and selected-IPCE plots:

- use a light blue fill with opacity `0.24`;
- draw both vertical boundaries at line width `3`;
- retain the “积分范围” legend item;
- apply the band only to the requested integration interval;
- do not change the selected common-coverage viewport or numerical
  integration range.

Region styling affects rendering only. It must not alter source arrays,
viewport calculations, integration, or exported values.

## 4. Sample Alignment Default

Change the initial `SampleWorkflowViewModel.AlignmentMode` from
`FixedDelay` to `Anchors`.

Consequences:

- the sample workflow initially displays “锚点” in the alignment selector;
- sample calculation requires sample anchors unless the user explicitly
  switches to fixed-delay mode;
- imported and edited sample anchors continue to use the existing scheduling
  and validation logic;
- existing sessions and explicit user selections are not silently replaced
  after startup.

The compiled smoke test may continue setting fixed-delay mode explicitly for
its synthetic trace.

## 5. i-t Unit Conversion Invariant

The existing C# importer already converts supported source units to canonical
internal `s/A`:

- time: `s`, `sec`, `second`, `ms`, `min`, `h`;
- current: `A`, `mA`, `uA`, `µA`, `μA`, `nA`, `pA`.

The importer reads units from the first two column headers, records the
original headers and conversion factors, converts values before constructing
`TraceData`, and sorts time/current pairs together.

If either unit is missing or unsupported, the UI must continue requesting
explicit unit selection. Numeric magnitude must never be used to guess units.

No production conversion formula is changed in this work. Existing unit tests
remain mandatory, and the final regression must demonstrate representative
automatic conversions such as `ms/mA` and `min/uA`.

## 6. Error and Compatibility Behavior

- Larger text and stronger bands must not introduce application-closing
  exceptions.
- Explicit axis limits, logarithmic-axis validation, Reset, Show All, hover,
  layer controls, and PNG export remain unchanged.
- MATLAB source and MATLAB plotting behavior remain unchanged.
- The external-IPCE standalone workflow remains independent of measurement
  traces and anchors.

## 7. Test Strategy

All behavior changes follow RED/GREEN TDD.

Focused tests will verify:

- tick and legend constants are at least `20`;
- axis-label size is at least 20% larger than tick size;
- title size is at least `26`;
- the shared theme applies these sizes to bottom, left, top, and right axes;
- dark-current and integration models use the approved opacity values;
- the renderer uses line width `3` for region boundaries;
- a new sample workflow starts in `AlignmentMode.Anchors`;
- existing i-t automatic conversion tests continue covering all supported
  time and current units;
- missing units still require explicit overrides.

Final verification will run:

```powershell
dotnet build "csharp APP/IPCE.slnx" -c Release --no-restore
dotnet test "csharp APP/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

The portable package is rebuilt only after all tests pass. Human visual
acceptance remains necessary because physical display size and viewing
distance cannot be proven by automated tests.
