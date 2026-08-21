# Project Guide

This repository contains the MATLAB programmatic-UI application under
`matlab/`, the self-contained Windows C# WPF application under `csharp/`,
and shared public inputs under `data/`. Read this file first, then
`PROJECT_MEMORY.md`; consult
`README_CN.md` for user workflows and formulas, and
`docs/superpowers/progress/ipce-csharp-migration-progress.md` for the latest
C# verification state. Do not scan every source file unless the task requires
it.

## What the application does

1. Uses a calibrated silicon detector i-t trace to calculate monochromatic
   incident power density.
2. Uses a sample i-t trace and that power density to calculate sample IPCE.
3. Integrates either the calculated IPCE or an externally imported two-column
   IPCE dataset against a solar spectrum.
4. Exports measurement results, external IPCE, integrated current density, and
   cumulative current-density-versus-wavelength data.

External IPCE post-processing is a standalone workflow. It must continue to
work without calibration, silicon/sample i-t data, or anchors.

## Entry points and file routing

### MATLAB reference application

- `matlab/IPCEApp.m`: UI, state, file dialogs, source selection, plotting,
  export UI.
- `matlab/ipceDefaultConfig.m`: exact startup filenames and dark-current
  defaults.
- `matlab/ipceReadIT.m`: i-t parsing, unit detection, conversion to canonical
  `s/A`.
- `matlab/ipceReadExternalIPCE.m`: external wavelength/IPCE parsing.
- `matlab/ipceResolveIPCESource.m`: selects calculated or external IPCE for
  integration.
- `matlab/ipceIntegrateSpectrum.m`: numerical spectrum integration and
  cumulative curve.
- `matlab/ipceBuildPostprocessExportItems.m`: standalone post-processing
  export bundle.
- `matlab/ipceWriteExport.m`: XLSX/CSV/MAT writing and on-disk verification.
- `matlab/run_ipce_selftest.m`: regression and numerical self-test.
- `matlab/ipceRepositoryPaths.m`: repository, MATLAB, default-data, and example
  data roots.
- `matlab/ipceLanguageCatalog.m`: stable bilingual catalog keys and first-level
  UI strings.
- `matlab/ipceLocalizeLiteral.m`: MATLAB static/runtime UI translation,
  formatted messages, and English fallback guard.
- `matlab/ipceLanguagePreference.m`: safe system-locale resolution and atomic
  `%LOCALAPPDATA%/IPCEApp/settings.json` persistence.
- `matlab/ipceSystemLocale.m`: deployed-safe Windows UI-culture detection with
  tested fallbacks.
- `matlab/ipceLocalizeException.m`: stable error-identifier to localized
  recoverable-message mapping.
- `data/defaults/`: four exact startup files shared by MATLAB and C#.
- `data/examples/`: MBVO example measurement and alignment files.
- `README_CN.md`: complete Chinese user documentation and physical formulas.
- `PROJECT_MEMORY.md`: recent decisions and implementation history.

Other `ipce*.m` files implement calibration import, scheduling, window
extraction, and IPCE calculation. Open only the file relevant to the task.

### MATLAB development workflow

Treat MATLAB as the numerical oracle, original programmatic UI, and supported
Runtime-based fallback deployment route.

- Route UI/state/file-dialog work to `matlab/IPCEApp.m`.
- Route import, scheduling, extraction, calculation, integration, and export
  work to the narrowest matching `matlab/ipce*.m` function.
- Put MATLAB numerical and regression coverage in
  `matlab/run_ipce_selftest.m`.
- Use `matlab/runIPCEApp.m` only as the zero-output deployed launcher; its
  `--smoke-test` mode must construct, validate, and close the real UI.
- New MATLAB behavior and bug fixes require a failing regression test before
  production changes. Run the focused reproduction first, then the complete
  self-test and UI smoke.
- If formulas, defaults, unit conversion, interpolation, scheduling, or export
  semantics change intentionally, review the C# golden-parity fixtures and
  tests. Regenerate or update them deliberately, or document an intentional
  divergence; never allow silent MATLAB/C# drift.

MATLAB packaging is controlled by:

- `matlab/ipcePortablePackageConfig.m`: project root, release/archive names,
  executable, portable readme, and the four embedded default files.
- `matlab/build_ipce_portable.m`: runs the MATLAB self-test, compiles with
  `mcc -e`,
  copies the portable readme, rejects bundled Runtime installers, creates the
  ZIP, and verifies it by extraction.
- `matlab/dist/`: authoritative MATLAB-compiled output location.

The MATLAB ZIP is not self-contained: it excludes MATLAB Runtime and requires
64-bit MATLAB Runtime R2023b Update 6 or a later R2023b update on the target
computer. This is distinct from the self-contained C# package under
`csharp/dist`.

### Current C# Windows application

The C# project root is exactly `csharp`; the former folder name `csharp APP`
must not be reintroduced.

- `csharp/IPCE.slnx`: solution entry point.
- `csharp/src/IPCE.Core`: calculations, scheduling, extraction, numerical
  methods, and domain models.
- `csharp/src/IPCE.IO`: calibration/i-t/IPCE/spectrum import and export.
- `csharp/src/IPCE.Desktop`: WPF UI, state, view models, plotting, and
  application services.
- `csharp/src/IPCE.Desktop/Plotting/PlotTheme.cs`: shared plot typography.
- `csharp/src/IPCE.Desktop/Plotting/ResultPlotModelBuilder.cs`: scientific
  plot series, dark-current bands, and integration bands.
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`: sample
  workflow defaults and invalidation behavior.
- `csharp/tests/IPCE.Core.Tests`: numerical and MATLAB-golden parity tests.
- `csharp/tests/IPCE.IO.Tests`: import/export and unit-conversion tests.
- `csharp/tests/IPCE.Desktop.Tests`: UI view-model, plotting, smoke, and
  portable-package tests.
- `csharp/scripts/build-portable.ps1`: complete release verification,
  self-contained publish, smoke validation, ZIP creation, and build manifest.
- `csharp/dist/IPCEApp_Windows_x64.build.json`: latest machine-readable
  release evidence.

`dist APP` contains extracted/manual distribution copies. Treat
`csharp/dist` and its fresh build manifest as the authoritative generated
release location.

## Canonical units and non-negotiable behavior

- i-t internal columns: `Time_s`, `Current_A`.
- Supported source time units: `s`, `sec`, `second`, `ms`, `min`, `h`.
- Supported source current units: `A`, `mA`, `uA`, `µA`, `μA`, `nA`, `pA`.
- Never guess missing i-t units from numeric magnitude; the UI asks the user.
- Time-alignment files are fixed as wavelength `nm`, confirmed time `s`.
- External IPCE is first numeric column `nm`, second numeric column `%`.
- Spectrum irradiance is `W m^-2 nm^-1`.
- Integrated and cumulative current density is `mA cm^-2`.
- Do not extrapolate beyond common IPCE/spectrum wavelength coverage.
- Do not clip finite external IPCE to `0–100%`.
- Keep calculated and external IPCE in separate state; never overwrite one with
  the other.

## Startup defaults

The four exact startup files live under `data/defaults/` and are shared by both
implementations.

- Silicon trace:
  `Si-i t [300 1100] nm-grating 2-filter.txt`
- Silicon anchors:
  `Si-i t [300 1100] nm-grating 2-filter-time match.txt`
- Dark subtraction: enabled.
- Silicon dark range: `0.1–10 s`.
- Sample dark range: `50–60 s`.

For the C# application, a new sample workflow starts in anchor-alignment mode.
Users may explicitly switch to fixed-delay mode; importing anchors must not
silently change the user's selected alignment mode.

## C# plotting invariants

- Shared title/axis/tick/legend font sizes are `28/30/24/24`.
- WPF controls use Arial in English and Microsoft YaHei in Chinese; all
  ScottPlot-internal titles, axes, units, legends, ranges, and annotations
  remain English and use Arial in both UI languages.
- Hover and toolbar text remain size `14`.
- Primary scientific plot series use line width `3`.
- Dark-current, spectrum, and integration ranges use grey `#9E9E9E` at
  opacity `0.14`.
- Each range has two grey dashed boundary lines at width `3`.
- Plot legends use proportional `24 x 12` symbols and no outer border.
- Plot styling, viewport controls, and display layers must never change
  calculation or export values.

## Validation

All new behavior or bug fixes require a failing regression test before
production code. Temporary self-test files must be removed by cleanup helpers.

For MATLAB functional changes, first change to `matlab/`:

```matlab
run_ipce_selftest
```

For UI changes also run:

```matlab
app = IPCEApp;
drawnow;
assert(isvalid(app));
close(app);
```

The equivalent non-interactive validation command is:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

For MATLAB packaging changes, run:

```powershell
matlab -batch "cd('matlab'); build_ipce_portable"
```

Then inspect the fresh `matlab/dist/IPCEApp_R2023b_Windows_x64.zip`, confirm that
the Runtime payload rejection and extraction verification completed, and keep
the clean-machine acceptance gate pending until the exact ZIP is tested on a
machine without MATLAB but with the matching R2023b Runtime installed.

For C# changes, run:

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
```

The current expected test counts are Core `58`, IO `44`, Desktop `130`, total
`232`, with zero failures and skips.

For release work, run:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
```

Read and report the newly generated build manifest; never reuse an older
archive size, hash, test count, or smoke result.

Two external release gates remain until explicitly performed and recorded:

1. Run the exact final ZIP/hash through the complete workflow on a clean
   Windows 10/11 x64 machine or VM with neither MATLAB nor .NET Runtime.
2. Check representative plots at Windows scaling 100%, 125%, and 150% on
   representative 16–24 inch displays.

## Repository status

As of 2026-08-15 this directory is a Git repository on `main` with origin
`https://github.com/ZiYingZhang/IPCE-measurement.git`. Local commits are
authorized for the approved normalization plan. Remote pushes, tags, Releases,
and visibility changes still require explicit owner approval.
