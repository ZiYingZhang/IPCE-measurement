# IPCE Measurement User Guide

This project provides a complete workflow for photoelectrochemical measurements: convert a calibrated silicon-detector i-t trace into monochromatic incident power density, calculate sample IPCE from a sample i-t trace, and integrate IPCE with a solar spectrum to obtain integrated and cumulative current density.

## 1. Choose an application

### C# WPF application (recommended on Windows)

Download [IPCEApp_Windows_x64.zip from the GitHub Release](https://github.com/ZiYingZhang/IPCE-measurement/releases/download/v1.0.0/IPCEApp_Windows_x64.zip), extract it, and run `IPCEApp.exe`. This is a self-contained Windows package; MATLAB and a separately installed .NET Runtime are not required.

### MATLAB application

MATLAB source code is under `matlab/`. Download the [MATLAB portable package from the GitHub Release](https://github.com/ZiYingZhang/IPCE-measurement/releases/download/v1.0.0/IPCEApp_R2023b_Windows_x64.zip); it requires 64-bit MATLAB Runtime R2023b. MATLAB is the numerical reference implementation and is useful for development and result cross-checking.

## 2. Data directories

The repository separates startup inputs from reproducible examples:

```text
data/defaults/   Default files imported at startup
data/examples/   Example inputs for a complete calculation
```

On the machine used to prepare this release:

```text
E:\Research Library\Data\Codes\IPCE measurement\data\defaults
E:\Research Library\Data\Codes\IPCE measurement\data\examples
```

The files in `data/defaults/` are shared by MATLAB and C#. The application attempts to load them at startup when they are available. The files in `data/examples/` are optional and demonstrate a full calculation; filenames may change between releases, so use the files actually present in the directory. Replace them with your own inputs when needed. Generated `dist/` folders are intentionally excluded from source history; compiled applications are published as GitHub Release assets.

## 3. Complete calculation workflow

### 3.1 Import silicon calibration data

1. Select and import the silicon calibration table.
2. Import the silicon detector i-t file.
3. Import the silicon time-anchor file. Anchor wavelengths are in `nm` and confirmed times are in `s`.
4. Confirm dark-current subtraction. The default silicon dark range is `0.1–10 s`.
5. Set the start wavelength, end wavelength, wavelength step, and active detector area.

The application converts i-t data to canonical internal columns: `Time_s` and `Current_A`. It never guesses a missing time or current unit from numeric magnitude; the user must confirm the unit.

### 3.2 Calculate monochromatic incident power density

Click **Calculate power density**. The application combines the calibration relationship, silicon responsivity, and silicon i-t trace to produce wavelength—incident-power-density data. Dark and spectral ranges are shown on the plot.

Common plot units are:

- Spectral irradiance: `W m^-2 nm^-1`
- Areal power density: `µW cm^-2`

Plot titles, axes, ticks, legends, and scientific units use English Arial labels for stable glyph and superscript rendering across Windows font environments. Window controls and workflow text can still be switched between Chinese and English.

### 3.3 Import sample data and calculate IPCE

1. Import the sample i-t file.
2. Import sample time anchors, or switch to fixed-delay mode and enter the delay.
3. Set the sample dark range, wavelength range, step, averaging duration, and active area.
4. Click **Calculate IPCE**.

The default sample dark range is `50–60 s`. The sample current is windowed and dark-corrected before it is combined with incident power density. Calculated IPCE and externally imported IPCE remain separate state and never overwrite one another.

### 3.4 Integrate with a solar spectrum

1. Import the solar-spectrum file.
2. Select the IPCE source: calculated IPCE or an external two-column IPCE file.
3. Confirm the wavelength, spectral-irradiance, and IPCE columns.
4. Set the integration interval. Numerical integration is restricted to the common wavelength coverage of IPCE and spectrum; values outside that overlap are not extrapolated.
5. Click **Integrate spectrum** and inspect the integrated value and cumulative curve.

Integrated and cumulative current density use `mA cm^-2`. The integration range is shown with a light fill and grey dashed boundary lines; this is presentation-only and does not change calculations or exports.

### 3.5 Export results

The application can export measurement results, external IPCE, integrated current density, and cumulative current-density-versus-wavelength data. For reproducibility, retain the input files, analysis parameters, and exported tables together.

## 4. Standalone external-IPCE processing

External IPCE post-processing is independent of silicon calibration, silicon i-t, sample i-t, and time anchors. Prepare a file with at least two numeric columns:

1. Column 1: wavelength in `nm`.
2. Column 2: IPCE in `%`.

Import the file, select a solar spectrum, and run the integration. Finite external IPCE values are not forcibly clipped to `0–100%`, so the source data are preserved.

## 5. File formats and units

- Canonical i-t columns: `Time_s`, `Current_A`.
- Supported time units: `s`, `sec`, `second`, `ms`, `min`, `h`.
- Supported current units: `A`, `mA`, `uA`, `µA`, `μA`, `nA`, `pA`.
- Time-anchor files: wavelength `nm`, confirmed time `s`.
- External IPCE: first column `nm`, second column `%`.
- Solar spectrum: `W m^-2 nm^-1`.
- Integrated and cumulative current density: `mA cm^-2`.

Missing units are never inferred from numeric magnitude. Correct the source header or explicitly select the unit during import.

## 6. Troubleshooting

**Defaults are not loaded automatically.** Confirm that the application is launched with its repository/package-relative layout intact and that `data/defaults/` is present. You can always use the browse buttons to import files manually.

**Can the examples complete the full workflow?** Yes. Use the files in `data/examples/` for silicon calibration, silicon i-t, sample i-t, time anchors, and spectrum integration. If filenames differ, follow the actual directory contents and column headers.

**Why is the integration interval shorter than requested?** The application avoids extrapolation and uses the overlap of the IPCE and solar-spectrum wavelength coverage.

**Why are plot titles English in the Chinese UI?** This is intentional. Plot-internal text uses English Arial labels to avoid missing Chinese glyphs and malformed units/superscripts. Window controls, buttons, and workflow descriptions still follow the selected Chinese/English language.

## 7. Developer verification

From the repository root:

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
```

MATLAB numerical and UI smoke test:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

For formulas, MATLAB function details, and numerical conventions, see [README_CN.md](../README_CN.md).
