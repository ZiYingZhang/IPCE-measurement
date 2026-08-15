# IPCE C# Migration Progress

Last updated: 2026-08-15

## Current checkpoint

- Last completed task: Task 3 of the 2026-08-15 repository-normalization plan
- Status: normalized source and shared-data paths pass MATLAB and .NET source
  gates; portable packages will be rebuilt in Task 5
- Next tasks:
  1. finish current documentation routing and portable-package verification;
  2. implement C# bilingual localization;
  3. implement MATLAB bilingual localization;
  4. prepare the private GitHub repository and `v1.0.0` for public release
- MATLAB numerical/UI behavior modified by normalization: no; files moved to
  `matlab/` and shared startup files moved to `data/defaults/`
- Git action taken: local commits on `main`; no remote push, tag, Release, or
  visibility change

## Environment

- .NET SDK: `10.0.302`
- C# target: `net10.0`
- WPF target: `net10.0-windows10.0.19041.0`
- Workspace: Git repository on `main`; C# project root is `csharp`
- Remote: `https://github.com/ZiYingZhang/IPCE-measurement.git`
- Commit policy: local commits are authorized for the approved plan; remote
  changes require explicit owner approval

Current validation commands from the repository root:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
matlab -batch "cd('matlab'); build_ipce_portable"
```

Current expected .NET count after two repository-layout regressions: Core 58,
IO 44, Desktop 97, total 199, with zero failures and skips.

## Checkpoint 2 verification

Domain test RED phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter DomainValidationTests
```

Result before implementation: failed at compile time because the new
`IPCE.Core.Domain` and `IPCE.Core.Errors` contracts did not exist.

MATLAB oracle and golden baseline export:

```powershell
matlab -batch "addpath('csharp/tools'); export_csharp_baseline"
```

Result: the unchanged MATLAB self-test passed, then the baseline exporter
generated:

- `default_silicon_extracted.csv`: 161 rows,
  SHA-256 `1848e24c08ba38a27b310bf0aa2172a6801eece6168bbff680fbd37fe746abdf`
- `default_power_density.csv`: 161 rows,
  SHA-256 `b6f719dcce2df92392e48541a4154620b2e270ee7eb1fd1b63ee733c6ce0ba6c`
- `synthetic_sample_ipce.csv`: 3 rows,
  SHA-256 `4eec34363e1e4510b2980ad01ca67c7163ec5216a61186238413103e922e8843`
- `integration_summary.csv`: 1 row,
  SHA-256 `e1b54b415d06342b0324af6afab2c4ce853a0996a5e8cb8ed439d5353ccf5454`
- `integration_curve.csv`: 9 rows,
  SHA-256 `97140124f577c76e7706ce07a8d06bb60c0856da87b469aa4cb20626b05cacbe`
- `manifest.json`: SHA-256
  `88bf9d92819c86ab6dad5edf787e9dbc388f2e70998bfa696e54191f6f99fe59`

Domain test GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter DomainValidationTests
```

Result: 10 passed, 0 failed, 0 skipped.

Full solution verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 12 tests passed, 0 failed,
0 skipped.

## Files created or changed for checkpoint 2

- `csharp/tools/export_csharp_baseline.m`
- `csharp/tests/TestData/Golden/`
- `csharp/src/IPCE.Core/Errors/IpceException.cs`
- `csharp/src/IPCE.Core/Domain/TraceData.cs`
- `csharp/src/IPCE.Core/Domain/CalibrationData.cs`
- `csharp/src/IPCE.Core/Domain/ScheduleModels.cs`
- `csharp/src/IPCE.Core/Domain/ResultModels.cs`
- `csharp/tests/IPCE.Core.Tests/DomainValidationTests.cs`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

The migration did not edit `IPCEApp.m`. Changes currently visible under
`dist/` were left untouched because they are outside the approved C# migration
scope.

## Checkpoint 3 verification

Numerical test RED phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "InterpolationTests|IntegrationPrimitiveTests"
```

Result before implementation: failed at compile time because
`IPCE.Core.Numerics` did not exist.

MATLAB PCHIP reference generation:

```powershell
matlab -batch "format long g; x=[0 1 2.5 4]; y=[0 2 1 3]; q=[0 0.25 0.5 1 1.5 2.5 3 3.75 4]; v=interp1(x,y,q,'pchip'); disp([q(:),v(:)])"
```

Result: reference values recorded in `InterpolationTests.cs`.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "InterpolationTests|IntegrationPrimitiveTests"
```

Result: 10 passed, 0 failed, 0 skipped.

Full Core verification:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --no-restore
```

Result: 20 passed, 0 failed, 0 skipped.

## Files created for checkpoint 3

- `csharp/src/IPCE.Core/Numerics/Interpolation.cs`
- `csharp/src/IPCE.Core/Numerics/TrapezoidalIntegration.cs`
- `csharp/tests/IPCE.Core.Tests/InterpolationTests.cs`
- `csharp/tests/IPCE.Core.Tests/IntegrationPrimitiveTests.cs`

## Checkpoint 4 verification

Schedule/extraction RED phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "ScheduleBuilderTests|TraceExtractorTests"
```

Result before implementation: failed at compile time because
`IPCE.Core.Scheduling` and `IPCE.Core.Extraction` did not exist.

First GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "ScheduleBuilderTests|TraceExtractorTests"
```

Result: 14 passed, 0 failed, 0 skipped.

A MATLAB parity review then found that the single-wavelength anchor branch
needed a nominal-delay-width window. A focused regression test was added and
observed failing with `IPCE:InvalidSchedule` before the implementation was
corrected.

Final targeted and Core verification:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "ScheduleBuilderTests|TraceExtractorTests"
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --no-build --no-restore
```

Result: targeted tests 15 passed; all Core tests 35 passed, 0 failed, 0
skipped.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 37 tests passed, 0 failed,
0 skipped.

## Files created or changed for checkpoint 4

- `csharp/src/IPCE.Core/Domain/ScheduleModels.cs`
- `csharp/src/IPCE.Core/Scheduling/ScheduleBuilder.cs`
- `csharp/src/IPCE.Core/Extraction/TraceExtractor.cs`
- `csharp/tests/IPCE.Core.Tests/ScheduleBuilderTests.cs`
- `csharp/tests/IPCE.Core.Tests/TraceExtractorTests.cs`

## Checkpoint 5 verification

Power/IPCE, source-selection, and spectrum-integration RED phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "IpceCalculatorTests|IpceSourceResolverTests|SpectrumIntegratorTests"
```

Result before implementation: failed at compile time because
`IPCE.Core.Calculation`, `PowerDensityPoint`, and `IpcePoint` did not exist.

The result records were extended beyond the original abbreviated interface so
all MATLAB export columns remain available: standard errors, signed currents,
areas, and sample counts are retained.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "IpceCalculatorTests|IpceSourceResolverTests|SpectrumIntegratorTests"
```

Result: 17 passed, 0 failed, 0 skipped. This includes:

- synthetic 20%, 50%, and 80% IPCE with distinct silicon/sample areas;
- PCHIP power-density interpolation to an independent sample grid;
- calibration, power, and integration coverage rejection;
- calculated/external source selection and preservation of 120% external IPCE;
- all 161 rows and all columns of the MATLAB default power-density golden CSV;
- all columns of the MATLAB synthetic IPCE golden CSV;
- analytic spectrum integration and every MATLAB golden cumulative-curve
  column.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 54 tests passed, 0 failed,
0 skipped (52 Core, 1 IO, 1 Desktop).

Original MATLAB regression:

```powershell
matlab -batch "run_ipce_selftest"
```

Result: all MATLAB self-tests passed.

## Files created or changed for checkpoint 5

- `csharp/src/IPCE.Core/Domain/ResultModels.cs`
- `csharp/src/IPCE.Core/Calculation/IpceCalculator.cs`
- `csharp/src/IPCE.Core/Calculation/IpceSourceResolver.cs`
- `csharp/src/IPCE.Core/Calculation/SpectrumIntegrator.cs`
- `csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj`
- `csharp/tests/IPCE.Core.Tests/GoldenCsv.cs`
- `csharp/tests/IPCE.Core.Tests/IpceCalculatorTests.cs`
- `csharp/tests/IPCE.Core.Tests/IpceSourceResolverTests.cs`
- `csharp/tests/IPCE.Core.Tests/SpectrumIntegratorTests.cs`

At this checkpoint the complete numerical calculation engine is usable
without the UI. File import/export and the Windows workflow UI remain to be
implemented.

## Checkpoint 6 verification

Delimited-text and text-import RED phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --filter "DelimitedTableReaderTests|ItTraceReaderTests|AnchorReaderTests|ExternalIpceReaderTests"
```

Result before implementation: failed at compile time because
`IPCE.IO.Tables` and `IPCE.IO.Import` did not exist.

The first GREEN attempt was blocked by MSTest analyzer `MSTEST0044` because
MSTest 4 deprecates `DataTestMethod`. The tests were corrected to use
`TestMethod` with `DataRow`; no production behavior was changed for this
test-only issue.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --filter "DelimitedTableReaderTests|ItTraceReaderTests|AnchorReaderTests|ExternalIpceReaderTests"
```

Result: 20 passed, 0 failed, 0 skipped. Covered behavior includes:

- comma, tab, semicolon, and whitespace-separated two-column text;
- invariant and current-culture numeric parsing;
- thousands separators inside tab-separated values;
- `s/sec/second`, `ms`, `min`, and `h` time units;
- `A`, `mA`, `uA`, `µA`, `μA`, `nA`, and `pA` current units;
- mandatory explicit overrides when headers omit units;
- raw header and unit-factor metadata retention;
- time sorting with current pairing preserved;
- optional anchor/external-IPCE headers;
- duplicate anchor rejection;
- external-IPCE sorting, duplicate-wavelength averaging, and preservation of
  finite 120% values.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 73 tests passed, 0 failed,
0 skipped (52 Core, 20 IO, 1 Desktop).

Original MATLAB regression:

```powershell
matlab -batch "run_ipce_selftest"
```

Result: all MATLAB self-tests passed.

## Files created or changed for checkpoint 6

- `csharp/src/IPCE.IO/Tables/TabularData.cs`
- `csharp/src/IPCE.IO/Tables/DelimitedTableReader.cs`
- `csharp/src/IPCE.IO/Import/ItTraceReader.cs`
- `csharp/src/IPCE.IO/Import/AnchorReader.cs`
- `csharp/src/IPCE.IO/Import/ExternalIpceReader.cs`
- `csharp/tests/IPCE.IO.Tests/TemporaryTextFile.cs`
- `csharp/tests/IPCE.IO.Tests/DelimitedTableReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/AnchorReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/ExternalIpceReaderTests.cs`

## Checkpoint 7 verification

Workbook/startup RED phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --filter "CalibrationReaderTests|SpectrumReaderTests|StartupDataResolverTests"
```

Result before implementation: failed at compile time because the workbook and
startup classes did not exist.

The first GREEN build exposed that NPOI 2.7.5 defines
`ICell.DateCellValue` as nullable `DateTime?`. The date-normalization branch was
corrected to handle null before ISO-8601 formatting; no formula, numeric, or
string-cell behavior was changed.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --filter "CalibrationReaderTests|SpectrumReaderTests|StartupDataResolverTests"
```

Result: 8 passed, 0 failed, 0 skipped. Real workbook evidence:

- calibration workbook sheet: `Sheet1`;
- calibration import: 161 positive points, `300-1100 nm`;
- spectrum workbook sheets: `AM1.5`, `Spectra`;
- `Spectra` column 1: `Wavelength (nm)`;
- `Spectra` column 3: `Global tilt  W*m-2*nm-1`;
- spectrum import: 2002 non-negative points, `280-4000 nm`.

Startup behavior verified:

- exact application-directory file overrides embedded data;
- missing application-directory file falls back to the embedded resource;
- all four exact Unicode default files are embedded in `IPCE.IO`;
- dark ranges, illuminated areas, wavelength grid, delay/averaging, and
  spectrum integration defaults match the MATLAB application.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 81 tests passed, 0 failed,
0 skipped (52 Core, 28 IO, 1 Desktop).

Original MATLAB regression:

```powershell
matlab -batch "run_ipce_selftest"
```

Result: all MATLAB self-tests passed.

## Files created or changed for checkpoint 7

- `csharp/src/IPCE.IO/IPCE.IO.csproj`
- `csharp/src/IPCE.IO/Tables/NpoiWorkbookReader.cs`
- `csharp/src/IPCE.IO/Import/CalibrationReader.cs`
- `csharp/src/IPCE.IO/Import/SpectrumReader.cs`
- `csharp/src/IPCE.IO/Startup/DefaultConfiguration.cs`
- `csharp/src/IPCE.IO/Startup/StartupDataResolver.cs`
- `csharp/tests/IPCE.IO.Tests/TestPaths.cs`
- `csharp/tests/IPCE.IO.Tests/CalibrationReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/StartupDataResolverTests.cs`

## Checkpoint 8 verification

Export-service RED phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --no-restore --filter ExportServiceTests
```

Result before implementation: failed at compile time because
`IPCE.IO.Export`, `ExportTable`, and `ExportColumn` did not exist.

The first GREEN run passed seven tests and exposed a MatFileHandler stream
ownership mismatch in both MAT tests. MatFileHandler closed the supplied stream
after writing, while the common atomic writer still owned and needed to flush
that stream. The MAT adapter was corrected with a leave-open stream wrapper;
XLSX and CSV paths were unchanged.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --no-restore --filter ExportServiceTests
```

Result: 9 passed, 0 failed, 0 skipped. Covered behavior includes:

- rejection of empty selections, duplicate table names, mismatched column
  lengths, and invalid export names;
- one XLSX workbook with multiple named sheets and typed cells;
- UTF-8 BOM CSV with Excel-safe quoting;
- deterministic table-name suffixes for multi-table CSV export;
- scalar MAT `exportData` with per-table structures, `VariableNames`, typed
  columns, and `RowCount`;
- same-directory temporary files, close-before-replace, cleanup after failure,
  locked-target preservation, and non-empty post-write checks.

Cross-runtime MAT verification:

```powershell
matlab -batch "addpath('csharp/tools'); verify_csharp_mat_export"
```

Result: MATLAB successfully loaded the C# Level-5 MAT file and verified the
top-level `exportData`, numeric columns, character cell array, logical column,
variable names, and row count.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; 90 tests passed, 0 failed,
0 skipped (52 Core, 37 IO, 1 Desktop).

Original MATLAB regression:

```powershell
matlab -batch "run_ipce_selftest"
```

Result: all MATLAB self-tests passed.

## Files created for checkpoint 8

- `csharp/src/IPCE.IO/Export/ExportModels.cs`
- `csharp/src/IPCE.IO/Export/ExportService.cs`
- `csharp/src/IPCE.IO/Export/MatExportWriter.cs`
- `csharp/tests/IPCE.IO.Tests/ExportServiceTests.cs`
- `csharp/tools/verify_csharp_mat_export.m`

## Checkpoint 9 verification

Transactional-state and ViewModel RED phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "SessionStateTests|WorkflowViewModelTests"
```

Result before implementation: failed at compile time because
`IPCE.Desktop.State` and `IPCE.Desktop.ViewModels` did not exist.

The first GREEN build found that the WPF test project's generated implicit
usings do not include `System.IO`. The two test files were corrected with an
explicit import; no production behavior changed for this test-environment
issue.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "SessionStateTests|WorkflowViewModelTests"
```

Result: 10 passed, 0 failed, 0 skipped. Covered behavior includes:

- a failed silicon-trace import leaves the prior valid trace unchanged;
- calculated and external IPCE remain in separate simultaneous state;
- source switching preserves both datasets;
- external-IPCE integration works with no measurement inputs;
- replacing silicon power density invalidates dependent calculated IPCE but
  preserves external IPCE;
- failed integration preserves the prior valid integration result;
- all workflow ViewModels share one session and relay observable changes;
- file-import commands accept paths rather than concrete file-dialog types,
  perform only file I/O asynchronously, and marshal final assignment to the
  captured UI synchronization context.

Full solution checkpoint:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-build --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: build passed with 0 warnings and 0 errors; all 10 Desktop tests passed;
99 solution tests passed, 0 failed, 0 skipped (52 Core, 37 IO, 10 Desktop).

Original MATLAB regression and UI smoke test:

```powershell
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Result: all MATLAB self-tests passed; `IPCEApp` was constructed, validated,
and closed successfully.

## Files created or changed for checkpoint 9

- `csharp/src/IPCE.Desktop/State/SessionState.cs`
- `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/tests/IPCE.Desktop.Tests/SessionStateTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- removed the generated no-op `csharp/tests/IPCE.Desktop.Tests/Test1.cs`

## Checkpoint 10 verification

WPF-shell RED phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter MainWindowSmokeTests
```

Result before implementation: failed at compile time because the local
services and redesigned window interfaces did not exist.

The smoke-test harness then exposed two WPF lifecycle assumptions. App-level
resources are unavailable under a bare `Application`, and `Shutdown()` cannot
complete before a dispatcher loop is running. The final test uses the real
`App`, assigns the test window explicitly, enters `Application.Run()`, and
closes the last window from an idle-dispatcher callback. Application startup
was changed from `StartupUri` to explicit window creation so production and
tests use the same controllable path.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter MainWindowSmokeTests
```

Result: 3 passed, 0 failed, 0 skipped. The tests verify:

- a real STA window displays the named workflow and result regions with a
  `MainViewModel` data context and closes cleanly;
- calibration, silicon trace, silicon anchors, and spectrum defaults load as
  one validated startup bundle;
- local crash diagnostics contain the exception type and message.

Checkpoint build and Desktop regression:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore
```

Result: build passed with 0 warnings and 0 errors; all 13 Desktop tests passed.

## Files created or changed for checkpoint 10

- `csharp/src/IPCE.Desktop/App.xaml`
- `csharp/src/IPCE.Desktop/App.xaml.cs`
- `csharp/src/IPCE.Desktop/MainWindow.xaml`
- `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/src/IPCE.Desktop/Services/FileDialogService.cs`
- `csharp/src/IPCE.Desktop/Services/LocalCrashLogger.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

## Checkpoint 11 verification

Plot-controller and anchor-editing RED phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "PlotControllerTests|AnchorEditingTests"
```

Result before implementation: failed at compile time because
`IPCE.Desktop.Plotting` and the workflow anchor-editing interfaces did not
exist.

Targeted GREEN phase with real WPF shell coverage:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "PlotControllerTests|AnchorEditingTests|MainWindowSmokeTests"
```

Result: 10 passed, 0 failed, 0 skipped. Covered behavior includes:

- nearest original-trace time selection;
- exact data-limit reset and rejection of invalid or non-increasing limits;
- rejection of non-positive limits on logarithmic axes;
- silicon and sample anchors remain isolated in their owning workflows;
- confirmed anchors snap to original samples, update by wavelength, and sort;
- both real ScottPlot WPF trace controls are present in the live shell.

Full Desktop regression:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore
```

Result: all 20 Desktop tests passed, 0 failed, 0 skipped.

## Files created or changed for checkpoint 11

- `csharp/src/IPCE.Desktop/Plotting/PlotController.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/tests/IPCE.Desktop.Tests/PlotControllerTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/AnchorEditingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

## Checkpoint 12 verification

End-to-end RED phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter EndToEndWorkflowTests
```

Result before implementation: failed at compile time because the complete
calculation, integration, selection, and export interfaces were not connected
through the workflow ViewModels.

Targeted GREEN phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter EndToEndWorkflowTests
```

Result: 5 passed, 0 failed, 0 skipped. The scenarios verify:

- embedded defaults calculate exactly 161 positive power-density points;
- a fixed-delay synthetic sample trace calculates 20%, 50%, and 80% IPCE;
- external IPCE imports, integrates, and exports with no measurement inputs;
- calculated and external IPCE survive switching in both directions;
- selected post-processing exports use exact `ExternalIPCE`,
  `SpectrumSummary`, and `SpectrumCurve` table names in XLSX, CSV, and MAT.

The full Desktop run initially exposed an old notification test that assumed
the source notification must be the final notification. The new prerequisite
commands legitimately also notify `CanIntegrate`; the test now verifies that
the source notification is present without constraining subsequent valid
notifications.

Fresh full verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Result:

- Release build passed with 0 warnings and 0 errors;
- 114 .NET tests passed, 0 failed, 0 skipped
  (52 Core, 37 IO, 25 Desktop);
- all MATLAB self-tests passed;
- the original MATLAB UI constructed, validated, and closed successfully.

## Files created or changed for checkpoint 12

- `csharp/src/IPCE.Desktop/App.xaml`
- `csharp/src/IPCE.Desktop/ViewModels/WorkflowCalculation.cs`
- `csharp/src/IPCE.Desktop/ViewModels/WorkflowExportTables.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`

## Checkpoint 13 verification

Full-column parity and real-file tests were added before changing numerical
production code:

```powershell
dotnet test csharp/IPCE.slnx -c Release --no-restore --filter "GoldenParityTests|RealFileRegressionTests"
```

The two real-file tests passed immediately. The parity report initially
failed in extraction and dependent power-density columns because several
anchor-derived averaging windows selected a different boundary sample.
The demonstrated root cause was an IEEE-754 evaluation-order difference:
MATLAB `griddedInterpolant` evaluates linear interpolation as
`(1-t)*y0 + t*y1`, while the original C# implementation used
`y0 + t*(y1-y0)`. At 855 nm these produce
`951.0000000000001 s` and `951 s`, respectively, which changes the
half-open sample window.

A focused regression test first reproduced the exact rounding mismatch. The
C# linear interpolation expression was then changed to MATLAB's two-endpoint
weighted form. No tolerance relaxation or result clipping was introduced.

Targeted GREEN result:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --no-restore --filter "Linear_MatchesMatlabBarycentricRounding|GoldenParityTests"
```

Result: 2 passed, 0 failed, 0 skipped.

Machine-readable parity result:

- report: `csharp/tests/TestData/Golden/parity-report.json`;
- compared columns: 48;
- report status: passing;
- maximum absolute error: `1.77635683940025e-15`;
- maximum relative error: `7.66379200222849e-16`;
- required tolerance: `1e-12` absolute or `1e-9` relative.

Fresh full verification:

```powershell
dotnet test csharp/IPCE.slnx -c Release --no-restore --logger "trx;LogFileName=parity.trx"
matlab -batch "run_ipce_selftest"
```

Result:

- 118 .NET tests passed, 0 failed, 0 skipped
  (54 Core, 39 IO, 25 Desktop);
- all original MATLAB self-tests passed.

## Files created or changed for checkpoint 13

- `csharp/src/IPCE.Core/Numerics/Interpolation.cs`
- `csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj`
- `csharp/tests/IPCE.Core.Tests/InterpolationTests.cs`
- `csharp/tests/IPCE.Core.Tests/GoldenParityTests.cs`
- `csharp/tests/IPCE.IO.Tests/RealFileRegressionTests.cs`
- `csharp/tests/TestData/Golden/parity-report.json`
- NuGet lock files refreshed without changing pinned package versions

## Task 14 automated portable-build checkpoint

Package-validation RED phase:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter PortablePackageTests
```

Result before implementation: all 5 package-validator scenarios failed
because `csharp/scripts/smoke-test.ps1` did not exist. The tests require
scenario-specific diagnostics so a missing or unparsable script cannot make a
negative case pass accidentally.

The validator now covers:

- missing archive;
- archive at or above `200 * 1024 * 1024` bytes;
- archive without a root `IPCEApp.exe`;
- archive containing `MATLAB Runtime`, `mcr`, or `v93` path markers;
- a valid archive.

A separate failing process test demonstrated that the compiled application
ignored `--smoke-test` and remained open. The implemented smoke mode creates
the real WPF window, processes dispatcher work, loads all embedded defaults,
validates their row counts, calculates all 161 default silicon power-density
points, closes, and returns exit code 0.

Targeted and Desktop GREEN results:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter PortablePackageTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore
```

Result: all 6 portable tests passed; all 31 Desktop tests passed.

The final build command was run after the third-party notice text was checked
against the exact pinned NuGet-package license files:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File csharp/scripts/build-portable.ps1
```

It passed every automated gate:

1. original MATLAB self-tests;
2. all 124 .NET tests (54 Core, 39 IO, 31 Desktop);
3. untrimmed, self-contained `win-x64` publish;
4. published `IPCEApp.exe --smoke-test`;
5. ZIP creation and structure validation;
6. extraction to a fresh temporary directory;
7. extracted `IPCEApp.exe --smoke-test`;
8. rejection scan for MATLAB Runtime path markers;
9. strict ZIP size gate below 200 MB.

Portable artifact:

- path: `csharp/dist/IPCEApp_Windows_x64.zip`;
- exact size: `85,437,456` bytes;
- SHA-256:
  `4cda7da161ca6512e68cc9f7738fc59afb30aca133268cca8faf95f24cae0ddf`;
- entries: 440;
- root `IPCEApp.exe`: present;
- Chinese portable README: present;
- third-party notices: present and identical to the checked source;
- MATLAB Runtime markers: 0;
- build host OS version: `Microsoft Windows NT 10.0.26200.0`.

Clean-Windows acceptance is not yet claimable. Read-only environment checks
found no `WindowsSandbox.exe`, VMware, VirtualBox, or Hyper-V connection tool.
The current host has six .NET runtimes and MATLAB installed, so it cannot serve
as the plan's required VM with neither runtime. The final manual acceptance
must therefore be run on an external clean Windows 10/11 machine or VM before
marking task 14 and the migration complete.

## Files created or changed for the task 14 automated checkpoint

- `csharp/src/IPCE.Desktop/IPCE.Desktop.csproj`
- `csharp/src/IPCE.Desktop/App.xaml.cs`
- `csharp/src/IPCE.Desktop/Assets/THIRD_PARTY_NOTICES.txt`
- `csharp/tests/IPCE.Desktop.Tests/PortablePackageTests.cs`
- `csharp/scripts/smoke-test.ps1`
- `csharp/scripts/build-portable.ps1`
- `csharp/PORTABLE_README_CN.txt`
- `csharp/dist/IPCEApp_Windows_x64.zip`
- `csharp/dist/IPCEApp_Windows_x64.build.json`

## Checkpoint 1 verification

MATLAB baseline:

```powershell
matlab -batch "run_ipce_selftest"
```

Result: passed, including unit conversion, standalone external IPCE,
calculation, integration, XLSX export, and portable manifest tests.

Dependency restore:

```powershell
dotnet restore csharp/IPCE.slnx --use-lock-file
```

Result: passed.

Dependency vulnerability scan:

```powershell
dotnet list csharp/IPCE.slnx package --vulnerable --include-transitive
```

Result: no known vulnerable packages in all six projects.

Release build:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
```

Result: passed with 0 warnings and 0 errors.

Template tests:

```powershell
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Result: 3 passed, 0 failed, 0 skipped.

## Dependency decisions discovered during checkpoint 1

- NPOI 2.7.5 requested vulnerable
  `System.Security.Cryptography.Xml 8.0.2` as a minimum transitive version.
- The project explicitly pins `System.Security.Cryptography.Xml 10.0.10`;
  the vulnerability scan passes with this override.
- ScottPlot's SkiaSharp WPF dependency requires Windows build 19041 or later.
  WPF projects therefore target `net10.0-windows10.0.19041.0`, consistent
  with the Windows 10/11 product requirement.

## Files created or changed for checkpoint 1

- `csharp/global.json`
- `csharp/Directory.Build.props`
- `csharp/Directory.Packages.props`
- `csharp/IPCE.slnx`
- `csharp/.gitignore`
- `csharp/src/IPCE.Core/`
- `csharp/src/IPCE.IO/`
- `csharp/src/IPCE.Desktop/`
- `csharp/tests/IPCE.Core.Tests/`
- `csharp/tests/IPCE.IO.Tests/`
- `csharp/tests/IPCE.Desktop.Tests/`
- `docs/superpowers/plans/2026-07-27-ipce-csharp-migration.md`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

## Resume procedure

1. Read this file.
2. Run:

   ```powershell
   dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
   ```

3. Confirm all 124 solution tests pass.
4. Run the clean-Windows manual workflow listed in task 14, record its OS
   build and export results, then mark the migration complete.

## Checkpoint 15 verification

The first C# usability and reliability phase was implemented test-first from:

- `docs/superpowers/plans/2026-07-28-ipce-csharp-reliability-input.md`

Focused RED runs established the missing behavior before production changes:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter UserOperationRunnerTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "MainWindowSmokeTests|WorkflowViewModelTests"
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter FiniteDoubleConverterTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "ResultFreshnessTests|SessionStateTests"
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "WorkflowViewModelTests|EndToEndWorkflowTests|MainWindowSmokeTests"
```

The RED failures respectively showed missing operation-boundary interfaces,
missing injected ViewModel/App constructors, missing finite-number converter,
missing freshness state, and missing prerequisite/result messages.

The real-WPF survival test initially found that Windows allows only one
`Application` instance per AppDomain. The shell, recoverable calculation
failure, and dispatcher-failure scenarios were therefore combined under one
real STA application lifecycle. That test now proves expected calculation
errors leave the window usable without a crash log, while an unexpected
dispatcher exception is logged, reported, marked handled, and does not shut
down the dispatcher.

Final verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Result:

- Release build passed with 0 warnings and 0 errors.
- 148 .NET tests passed, 0 failed, 0 skipped
  (54 Core, 39 IO, 55 Desktop).
- All original MATLAB self-tests passed.
- The MATLAB UI constructed, validated, and closed successfully.

Implemented behavior:

- all user import, calculation, integration, and command-based export actions
  run inside one testable recoverable-error boundary;
- expected data, file, and permission errors warn without creating crash logs
  or terminating the application;
- unexpected command and dispatcher errors create local diagnostics while the
  dispatcher remains alive;
- scientific numeric fields accept decimal points, locale decimal commas, and
  exponent notation, reject blank/non-finite/grouped input, and commit on lost
  focus rather than every keystroke;
- power density, calculated IPCE, and integration results retain their last
  data with explicit `Missing`, `Current`, or `Stale` status;
- parameter changes invalidate only their dependent calculation chain;
- stale calculated results cannot be reused as calculation inputs or exported;
- workflow controls explain missing prerequisites and current/stale result
  status next to their action buttons;
- the sample fixed-start default is restored to 50 seconds.

## Files created or changed for checkpoint 15

- `csharp/src/IPCE.Desktop/App.xaml`
- `csharp/src/IPCE.Desktop/App.xaml.cs`
- `csharp/src/IPCE.Desktop/Input/FiniteDoubleConverter.cs`
- `csharp/src/IPCE.Desktop/Services/UserNotificationService.cs`
- `csharp/src/IPCE.Desktop/Services/UserOperationRunner.cs`
- `csharp/src/IPCE.Desktop/State/ResultStatus.cs`
- `csharp/src/IPCE.Desktop/State/SessionState.cs`
- `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/FiniteDoubleConverterTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/ResultFreshnessTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/SessionStateTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/UserOperationRunnerTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`

No Git commit exists because this project is not a Git repository.

## Checkpoint 16 verification

The import-selection and transactional-anchor phase was implemented
test-first from:

- `docs/superpowers/plans/2026-07-28-ipce-csharp-import-anchor-parity.md`

Focused RED runs:

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --no-restore --filter ItTraceReaderTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter ImportCoordinatorTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "ImportCoordinatorTests|WorkflowViewModelTests"
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --no-restore --filter SpectrumReaderTests
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj -c Release --no-restore --filter ExternalIpceReaderTests
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter "AnchorTableEditingTests|AnchorEditingTests"
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --no-restore --filter MainWindow_RemainsUsableAcrossRecoverableAndDispatcherErrors
```

The RED failures demonstrated missing trace inspection/coordinators,
transactional ViewModel imports, spectrum sheet discovery and selection,
workbook external-IPCE support, editable anchor interfaces, and real editable
anchor grids. A separate focused test failed because successful external-IPCE
import still left the calculated source selected.

Final verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Result:

- Release build passed with 0 warnings and 0 errors.
- 164 .NET tests passed, 0 failed, 0 skipped
  (54 Core, 42 IO, 68 Desktop).
- All original MATLAB self-tests passed.
- The MATLAB UI constructed, validated, and closed successfully.

Implemented behavior:

- i-t headers can be inspected without guessing units from magnitudes;
- missing i-t units open an explicit `s/ms/min/h` and
  `A/mA/uA/nA/pA` selector, while recognized units bypass the dialog;
- cancelled or failed i-t imports preserve the prior trace and summary;
- successful i-t imports display file, point count, time range, original
  units, and canonical `s/A` conversion metadata;
- solar-spectrum import discovers workbook sheets and numeric columns, shows
  column letters, headers, and numeric counts, and preserves prior spectrum
  metadata on cancellation;
- external IPCE imports consistently from TXT, CSV, XLS, and XLSX, averages
  duplicate wavelengths, preserves finite values above 100%, and selects the
  external source without overwriting calculated IPCE;
- silicon and sample anchors have isolated editable tables with explicit add,
  apply, and delete actions;
- anchor-table replacement is transactional: invalid duplicates restore both
  retained session anchors and visible editable rows;
- graph confirmation shows the snapped time, permits time adjustment, and
  identifies updates to an existing wavelength.

## Files created or changed for checkpoint 16

- `csharp/src/IPCE.IO/Import/TraceImportInspection.cs`
- `csharp/src/IPCE.IO/Import/ItTraceReader.cs`
- `csharp/src/IPCE.IO/Import/SpectrumReader.cs`
- `csharp/src/IPCE.IO/Import/ExternalIpceReader.cs`
- `csharp/src/IPCE.Desktop/Import/TraceImportCoordinator.cs`
- `csharp/src/IPCE.Desktop/Import/SpectrumImportCoordinator.cs`
- `csharp/src/IPCE.Desktop/Services/ImportSelectionService.cs`
- `csharp/src/IPCE.Desktop/ViewModels/AnchorRowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/ExternalIpceReaderTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/AnchorEditingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/AnchorTableEditingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

No Git commit exists because this project is not a Git repository.

## Checkpoint 17 verification

The visualization, reproducible-export, recovery-smoke, and portable-release
phase was implemented from:

- `docs/superpowers/plans/2026-07-28-ipce-csharp-visualization-export-release.md`

The plot-model, coverage-preview, result-control, and reproducible-export
changes were developed with focused RED/GREEN tests. WPF tests were run
serially because parallel builds of the same WPF project can collide in
temporary XAML-generation projects.

Fresh final verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
powershell -NoProfile -ExecutionPolicy Bypass -File csharp/scripts/build-portable.ps1
```

Result:

- Release build passed with 0 warnings and 0 errors.
- 178 .NET tests passed, 0 failed, 0 skipped
  (54 Core, 42 IO, 82 Desktop).
- All original MATLAB self-tests passed, and the MATLAB UI constructed,
  validated, and closed.
- Published and extracted `IPCEApp.exe --smoke-test` both exited 0.
- The compiled smoke covers default silicon power density, synthetic sample
  IPCE, independent external-IPCE integration, required plot controls, and
  reproducibility table construction.

Implemented behavior:

- live schedule and common-range previews report exact coverage before
  calculation and display out-of-range schedule points in red;
- all plot titles, axis labels, tick labels, and legends use a Chinese-capable
  Windows font;
- result tabs now plot silicon/sample i-t, schedules and anchors, power
  density, calculated plus external IPCE, solar spectrum, selected IPCE, and
  cumulative current density;
- plot controls support zoom/pan, double-click autoscale, explicit axis limits,
  guarded logarithmic axes, and PNG save;
- invalid plot series and axis settings fail with stable recoverable error
  codes instead of propagating rendering exceptions;
- exports retain existing result table names and append
  `MeasurementSettings`, optional `SiliconAnchors` / `SampleAnchors`, and
  `InputMetadata`;
- snapshot metadata includes invariant settings, result freshness/reasons,
  input file names, original/canonical units, headers, and spectrum
  worksheet/column selection;
- the real-window recovery test and extended compiled smoke confirm expected
  calculation errors do not terminate the application and stale results
  cannot be exported.

Portable artifact evidence:

- archive:
  `csharp/dist/IPCEApp_Windows_x64.zip`
- bytes: `85473720`
- SHA-256:
  `10e32d39c66b0b447cb45353284ce7f4444cf67fc99245ff3a8a9bc02bb4e469`
- entries: `440`
- root `IPCEApp.exe`, `PORTABLE_README_CN.txt`, and
  `THIRD_PARTY_NOTICES.txt`: present
- MATLAB/MCR markers: `0`
- build manifest:
  `csharp/dist/IPCEApp_Windows_x64.build.json`

The remaining release gate is external: test this exact archive and hash on a
clean Windows 10/11 x64 computer or VM with neither MATLAB nor .NET Runtime
installed. Record OS build, startup, both workflows, XLSX/CSV/MAT exports,
plots, recovery after a deliberate range error, and normal close before
marking the migration complete.

No Git commit exists because this project is not a Git repository.

## Checkpoint 18: plot readability and scientific overlays

Implemented from:

- `docs/superpowers/specs/2026-07-28-ipce-csharp-plot-readability-design.md`
- `docs/superpowers/plans/2026-07-28-ipce-csharp-plot-readability.md`

Implemented behavior:

- plot titles, axis labels, tick labels, legends, hover text, and toolbars use
  the shared DPI-aware Chinese-capable font theme;
- robust default limits use the 0.5% and 99.5% quantiles with padding, report
  clipped extreme-point counts, and provide separate Reset and Show All
  commands;
- all seven plot surfaces snap hover to real data points in pixel space and
  clear hover graphics on mouse leave;
- trace plots render calculated mean current as a thick orange segment over
  the exact averaging interval with a midpoint marker and structured hover
  details;
- dark-current ranges render as a translucent band with two explicit boundary
  lines and only appear when dark subtraction is enabled;
- raw trace, dark-current, and diagnostic trace layers can be toggled without
  mutating session data;
- irradiance and selected-IPCE panels focus on the requested common wavelength
  range, while cumulative current focuses on its result-curve range;
- stale mean-current overlays are explicitly labelled as stale.

Fresh final verification:

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
powershell -NoProfile -ExecutionPolicy Bypass -File csharp/scripts/build-portable.ps1
```

Result:

- Release build passed with 0 warnings and 0 errors.
- 197 .NET tests passed, 0 failed, 0 skipped
  (58 Core, 42 IO, 97 Desktop).
- All original MATLAB self-tests passed, and the MATLAB UI constructed,
  validated, and closed.
- Published and extracted `IPCEApp.exe --smoke-test` both exited 0.
- archive:
  `csharp/dist/IPCEApp_Windows_x64.zip`
- bytes: `85486930`
- SHA-256:
  `eaf99028363e7ac983d872963b15e6d943339852bf72ca997f45dce773b7e6ce`
- entries: `440`
- MATLAB/MCR markers: `0`
- build manifest:
  `csharp/dist/IPCEApp_Windows_x64.build.json`

The automated WPF smoke verifies the controls, sizes, rendering limits, and
event wiring. Human visual acceptance at Windows 100%, 125%, and 150% scaling
on representative 16–24 inch displays remains an explicit manual gate.

No Git commit exists because this project is not a Git repository.

## Checkpoint 19: large plot text, display bands, relocated paths, and release

Implemented and verified from:

- `.superpowers/sdd/2026-07-29-ipce-csharp-large-plot-text-and-bands/`

Confirmed presentation and startup behavior:

- plot ticks and legends use size `20`;
- axis names use size `24`;
- plot titles use size `26`;
- the sample time-alignment workflow initially selects anchor mode, while an
  explicit user switch to fixed-delay mode remains supported;
- dark-current bands use grey `#607D8B` at opacity `0.28`;
- irradiance and selected-IPCE integration bands use blue `#90CAF9` at
  opacity `0.24`;
- every band has two boundary lines of width `3`;
- these presentation bands do not change calculation, integration, or export
  values.

Relocated-folder regression:

- the C# source folder was renamed from `csharp` to `csharp APP`;
- `PortablePackageTests` and `GoldenParityTests` still appended a hard-coded
  `csharp` segment to the MATLAB repository root;
- the pre-fix evidence was 0/6 portable tests and 57/58 Core tests;
- both helpers now walk upward from `AppContext.BaseDirectory` and select the
  first directory containing `IPCE.slnx` and the expected local project
  structure, without hard-coding the current folder name;
- redirected Windows PowerShell diagnostics normalize only the known
  `Archive contains MATLAB Runtime marker` phrase: a regex is built from that
  constant so adjacent ASCII letters inside that phrase may contain a soft
  CRLF/LF, then the match is restored to the complete constant;
- an explicit regression assertion preserves ordinary `alpha\r\nbeta`
  output unchanged, while the real validator still covers the observed
  `mar\r\nker` wrap and preserves the process exit code;
- final focused evidence is 6/6 portable tests and 58/58 Core tests.

i-t unit verification:

```powershell
dotnet test "csharp APP/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" -c Release --no-restore --filter ItTraceReaderTests
```

Result: 14 passed, 0 failed, 0 skipped, with no new `TestMethod` or `DataRow`.
Actual `ItTraceReader.Read` calls cover all time aliases
`s/sec/second/ms/min/h` and all current aliases
`A/mA/uA/µA/μA/nA/pA`. The `s`, `sec`, and `second` reads each assert a second
row time of `1 s`, a `TimeToSecondsFactor` of `1`, and the exact original time
unit. `TraceData.TimeSeconds` is the canonical-seconds representation; the
domain metadata has no separate `CanonicalTimeUnit` string. Existing
`TimeUnits_ConvertToSeconds` reads `Current/A`, so `A` already has actual-read
coverage. The suite also verifies combined `ms/mA` metadata, explicit
`min/uA` overrides, missing-unit rejection, canonical `s/A` values, and no
numeric-magnitude guessing. `ItTraceReader.cs` required no change.

Fresh release verification:

```powershell
dotnet build "csharp APP/IPCE.slnx" -c Release --no-restore
dotnet test "csharp APP/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Result:

- Release build exit `0`, with 0 warnings and 0 errors;
- 197 .NET tests passed, 0 failed, 0 skipped
  (58 Core, 42 IO, 97 Desktop);
- MATLAB numerical self-tests passed;
- the MATLAB UI constructed, satisfied `isvalid`, and closed without error.

The first portable publish attempt encountered a concurrent compiler lock on
`IPCE.Desktop/obj/.../win-x64/IPCEApp.dll` after its MATLAB and .NET
regressions had passed. No application change was made for that environmental
failure. A single rerun with no concurrent build completed every gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp APP/scripts/build-portable.ps1"
```

Final portable evidence from
`csharp APP/dist/IPCEApp_Windows_x64.build.json`:

- archive: `csharp APP/dist/IPCEApp_Windows_x64.zip`;
- exact bytes: `85487039` (`81.526793 MiB`);
- SHA-256:
  `15fa9cc7e850a3eafd80c2da50237a61877ae594830f7955496546a048c4f4e0`;
- archive entries: `440`;
- published smoke exit code: `0`;
- extracted-archive smoke exit code: `0`;
- `matlabRuntimeIncluded`: `false`;
- manifest test counts: total 197, Core 58, IO 42, Desktop 97, failed 0,
  skipped 0;
- manifest MATLAB self-test and UI-smoke flags: `true`;
- generated UTC: `2026-07-29T14:03:38.2169636Z`.

Two external release-acceptance gates remain and neither is claimed as passed:

1. On a clean Windows 10/11 x64 machine or VM with neither MATLAB nor .NET
   Runtime, run the complete workflow using this exact archive and SHA-256:
   `csharp APP/dist/IPCEApp_Windows_x64.zip`,
   `15fa9cc7e850a3eafd80c2da50237a61877ae594830f7955496546a048c4f4e0`.
2. Perform a representative physical-display visual review at Windows 100%,
   125%, and 150% scaling.

No Git commit exists because this project is not a Git repository.
