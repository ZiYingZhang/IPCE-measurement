# IPCE C# Plot Readability and Diagnostic Overlay Design

**Date:** 2026-07-28

**Status:** User-approved design, pending implementation plan

**Scope:** C# Windows application plots only; numerical calculations and
MATLAB behavior remain unchanged.

## 1. Objective

Improve every C# result plot so that it is comfortably readable on typical
16–24 inch Windows displays, shows the scientifically relevant majority of
data despite occasional large leakage-current steps, supports precise
data-point inspection, and exposes the averaging and dark-current regions
used by the calculations.

The implementation must never remove, clip, or modify calculation/export
data. Robust ranges affect only the initial display viewport.

## 2. Evidence and Current Root Causes

The supplied screenshots show:

- plot text is too small at the application's normal window size;
- unconditional ScottPlot `AutoScale()` lets extreme i-t leakage steps
  dominate the Y range;
- the spectrum view includes the full imported 280–4000 nm range even when
  the selected integration range is approximately 300–600 nm;
- hover text reports the continuous mouse coordinate instead of a real data
  point;
- calculated mean currents are retained in power-density/IPCE results and
  averaging windows exist in the schedule preview, but the trace model does
  not combine and render them;
- the dark-current band exists in the model, but a 0.1–10 s band occupies
  less than one percent of a 0–1400 s overview and is visually inconspicuous.

## 3. Chosen Approach

Use a shared robust viewport and hit-testing layer for every plot.

Rejected alternatives:

- a user-adjustable percentile slider adds complexity to routine operation;
- full-data autoscaling preserves the current failure mode;
- per-view bespoke algorithms would create inconsistent interaction and
  duplicate code.

## 4. DPI-Aware Typography

ScottPlot text sizes use a shared theme expressed in display-independent
units:

- plot title: 18;
- axis labels: 15;
- tick labels: 13;
- legend: 13;
- hover tooltip: 14, semibold;
- plot toolbar and status badges: at least 14.

The theme applies to title, bottom/left/right axis labels, all visible tick
labels, legend, annotations, and hover labels. The Chinese font remains
`Microsoft YaHei UI` with ScottPlot's best-font fallback.

Acceptance must cover 16–24 inch displays at Windows scaling 100%, 125%, and
150%. Text must not overlap, truncate essential units, or require the user to
move closer than normal desktop viewing distance.

## 5. Robust Initial Viewport

### 5.1 Shared Y-range algorithm

For each visible primary series:

1. collect finite Y values that are marked as auto-range contributors;
2. sort a copy without modifying source data;
3. compute the 0.5th and 99.5th percentiles using linear interpolation;
4. use that interval when it is finite and non-constant;
5. add eight percent padding on each side;
6. expand a constant range using the existing constant-range rule;
7. fall back to the finite full range if robust bounds cannot be computed.

Overlay bands, averaging segments, hover markers, and annotations do not
change the robust range. A visible badge reports the number of primary data
points outside the robust viewport.

“复位视图” restores this robust viewport. A separate “显示全部” action uses
the finite minimum/maximum of all visible primary data. Manual axis settings
remain authoritative until Reset or Show All is requested.

### 5.2 X-range policy

- i-t: full finite time range plus two percent padding;
- time schedule: full requested wavelength range plus padding;
- power density and IPCE: full wavelength range of visible result series;
- spectrum/integration: selected integration range intersected with common
  spectrum/IPCE coverage, plus small padding;
- cumulative current: actual integration-result wavelength range.

No initial viewport may extrapolate beyond available data.

## 6. Shared Nearest-Point Hover

Every plot uses the same hit-test service:

- search only visible original series data, never display-downsampled data;
- compare candidate points in pixel space so differently scaled axes behave
  correctly;
- activate within a 12-pixel radius;
- show a highlighted marker and vertical/horizontal crosshair;
- show series name and formatted X/Y values with units;
- hide the marker, crosshair, and tooltip when no point is within the radius
  or the pointer leaves the plot.

For multi-series plots, the closest visible series wins. Hover must not mutate
session state or the current axis limits.

## 7. i-t Diagnostic Overlays

### 7.1 Mean-current windows

After a current result exists, combine:

- schedule preview `WindowStartSeconds` / `WindowEndSeconds`;
- power-density `SiliconMeanCurrentAmperes` or calculated-IPCE
  `SampleMeanCurrentAmperes`;
- result `SampleCount`;
- wavelength.

Render one thick orange horizontal segment over each actual averaging window,
with a marker at its midpoint. The legend contains one “平均电流” item rather
than one item per wavelength.

Hovering a mean segment or midpoint shows:

- wavelength in nm;
- averaging-window start and end in s;
- mean current in A using scientific notation;
- sample count.

Stale results remain visible only if the existing application policy exposes
them, and their overlay must be labelled as stale. Missing results simply omit
the mean overlay.

### 7.2 Dark-current interval

When dark subtraction is enabled:

- render a grey-blue full-height band;
- render both interval boundaries as visible vertical lines;
- place a “暗电流区间” label near the top boundary;
- include start/end times in hover details;
- keep the band visible at narrow widths using the boundary lines even when
  the filled area is sub-pixel.

When dark subtraction is disabled, do not render the band or boundary lines.

### 7.3 Layer controls

The existing trace checkboxes become functional and independently control:

- raw trace;
- dark-current interval;
- anchors, averaging windows, and mean-current segments.

Changing layer visibility recomputes only the viewport and plot contents; it
does not recalculate scientific results.

## 8. Plot Model and Component Boundaries

Add focused immutable models:

- `PlotViewportPolicy`: primary-series selection, robust percentile bounds,
  preferred X range, padding, and full/robust mode;
- `PlotViewport`: calculated X/Y limits and clipped-point count;
- `PlotIntervalMarker`: X start/end, Y value, label, color, and structured
  hover details;
- `PlotHoverPoint`: selected series/index, data coordinates, pixel distance,
  and formatted details.

Add pure services:

- `PlotViewportCalculator`: calculates robust/full limits without ScottPlot;
- `PlotHitTester`: finds the nearest original data point in pixel space;
- `TraceOverlayBuilder`: joins schedule and retained calculation results into
  averaging segments.

`PlotModelRenderer` remains responsible only for translating immutable models
to ScottPlot objects. WPF controls own pointer events, toolbar commands, and
tooltip visibility.

## 9. Refresh and Error Behavior

- Relevant result or trace changes rebuild models and restore the robust view.
- Unrelated state changes do not reset user zoom.
- Invalid or empty series show the existing empty-state message.
- Robust-range failure falls back to full finite bounds without terminating
  the application.
- Hover failures are contained inside the control and cannot close the
  application.
- Logarithmic-axis validation remains unchanged; non-positive visible values
  still produce the existing recoverable warning.

## 10. Test Strategy

All production changes follow RED/GREEN TDD.

Pure tests:

- font sizes and Chinese font applied to all text surfaces;
- percentile interpolation, padding, constant data, non-finite rejection, and
  clipped-point counts;
- a trace with a large leakage step keeps at least 99% of points visible in
  robust mode while Show All contains every point;
- integration X range focuses on selected common coverage;
- nearest-point selection across multiple series uses pixel distance and
  returns no hit outside 12 pixels;
- trace overlay builder joins every wavelength to the correct window, mean,
  and sample count;
- disabled dark subtraction produces no dark overlay.

STA/WPF tests:

- every result view exposes readable font sizes;
- mouse movement creates and clears the hover marker/tooltip;
- Reset restores robust limits and Show All restores full limits;
- mean segments and dark boundaries produce ScottPlot plottables;
- layer checkboxes actually hide/show their associated objects;
- a recoverable range error still leaves the real window usable.

Final verification:

- fresh Release build with zero warnings/errors;
- all Core, IO, and Desktop tests;
- original MATLAB self-test and UI smoke;
- rebuilt self-contained archive and both published/extracted smoke tests;
- visual acceptance at 100%, 125%, and 150% display scaling.

## 11. Out of Scope

- changing IPCE, power-density, interpolation, or integration formulas;
- deleting leakage-current data;
- modifying exported numerical results;
- adding a user-configurable percentile slider;
- replacing ScottPlot or redesigning the complete application shell.
