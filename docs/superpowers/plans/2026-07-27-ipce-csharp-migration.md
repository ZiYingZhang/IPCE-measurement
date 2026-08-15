# IPCE C# Windows Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete Windows 10/11 x64 C# replacement for the MATLAB IPCE application that runs from a portable ZIP without MATLAB or a preinstalled .NET runtime and remains below 200 MB.

**Architecture:** Keep the existing MATLAB application unchanged as the behavioral oracle. Implement numerical behavior in a UI-independent `IPCE.Core` library, file handling in `IPCE.IO`, and the Windows interface in `IPCE.Desktop`; verify each layer against MATLAB-generated golden data before connecting the next layer.

**Tech Stack:** C# 14, .NET 10.0.302 LTS, WPF, MSTest 4.3.2, Microsoft.NET.Test.Sdk 18.8.1, ScottPlot.WPF 5.1.59, NPOI 2.7.5, MatFileHandler 1.3.0, System.Security.Cryptography.Xml 10.0.10 security override, PowerShell 7-compatible build scripts.

## Global Constraints

- Target only Windows 10/11 x64 using
  `net10.0-windows10.0.19041.0` for WPF projects.
- Publish as a self-contained portable ZIP; users install neither MATLAB nor .NET.
- The final ZIP must be smaller than 200 MB.
- Do not modify or remove the existing MATLAB application while the C# version is under development.
- Do not initialize Git or add commit steps unless the user explicitly changes the repository rule.
- Every production change begins with a failing automated regression test.
- Internal i-t units are seconds and amperes; never infer missing units from numeric magnitude.
- Keep calculated IPCE and external IPCE in separate state.
- External IPCE post-processing must work without measurement inputs.
- Do not extrapolate outside calibration, power-density, or common IPCE/spectrum coverage.
- Do not clip finite external IPCE values to `0-100%`.
- Preserve signed photocurrent fields while using absolute photocurrent in power-density and IPCE calculations.
- Spectrum irradiance is `W m^-2 nm^-1`; integrated and cumulative current density is `mA cm^-2`.
- After every completed task, update `docs/superpowers/progress/ipce-csharp-migration-progress.md` with the task number, test command, result, changed files, and exact next task.
- Work may stop after any passing checkpoint. On resumption, read the progress file and rerun the recorded checkpoint command before changing files.

## Planned File Structure

```text
csharp/
  IPCE.slnx
  global.json
  Directory.Build.props
  Directory.Packages.props
  packages.lock.json files under each project
  src/
    IPCE.Core/
      IPCE.Core.csproj
      Domain/TraceData.cs
      Domain/CalibrationData.cs
      Domain/ScheduleModels.cs
      Domain/ResultModels.cs
      Errors/IpceException.cs
      Numerics/Interpolation.cs
      Numerics/TrapezoidalIntegration.cs
      Scheduling/ScheduleBuilder.cs
      Extraction/TraceExtractor.cs
      Calculation/IpceCalculator.cs
      Calculation/SpectrumIntegrator.cs
      Calculation/IpceSourceResolver.cs
    IPCE.IO/
      IPCE.IO.csproj
      Tables/TabularData.cs
      Tables/DelimitedTableReader.cs
      Tables/NpoiWorkbookReader.cs
      Import/ItTraceReader.cs
      Import/AnchorReader.cs
      Import/ExternalIpceReader.cs
      Import/CalibrationReader.cs
      Import/SpectrumReader.cs
      Startup/DefaultConfiguration.cs
      Startup/StartupDataResolver.cs
      Export/ExportModels.cs
      Export/ExportService.cs
      Export/MatExportWriter.cs
    IPCE.Desktop/
      IPCE.Desktop.csproj
      App.xaml
      App.xaml.cs
      MainWindow.xaml
      MainWindow.xaml.cs
      State/SessionState.cs
      Services/FileDialogService.cs
      Services/LocalCrashLogger.cs
      ViewModels/ViewModelBase.cs
      ViewModels/MainViewModel.cs
      ViewModels/SiliconWorkflowViewModel.cs
      ViewModels/SampleWorkflowViewModel.cs
      ViewModels/SpectrumWorkflowViewModel.cs
      Views/WorkflowControls.xaml
      Views/ResultTabs.xaml
      Plotting/PlotController.cs
      Assets/THIRD_PARTY_NOTICES.txt
  tests/
    IPCE.Core.Tests/
    IPCE.IO.Tests/
    IPCE.Desktop.Tests/
    TestData/
      Golden/
      Synthetic/
  tools/
    export_csharp_baseline.m
  scripts/
    build-portable.ps1
    smoke-test.ps1
  PORTABLE_README_CN.txt
docs/superpowers/progress/
  ipce-csharp-migration-progress.md
```

---

### Task 1: Installable Toolchain Plan, Solution Skeleton, and Resume Ledger

**Files:**
- Create: `csharp/global.json`
- Create: `csharp/Directory.Build.props`
- Create: `csharp/Directory.Packages.props`
- Create: `csharp/IPCE.slnx`
- Create: `csharp/src/IPCE.Core/IPCE.Core.csproj`
- Create: `csharp/src/IPCE.IO/IPCE.IO.csproj`
- Create: `csharp/src/IPCE.Desktop/IPCE.Desktop.csproj`
- Create: `csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj`
- Create: `csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj`
- Create: `csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj`
- Create: `docs/superpowers/progress/ipce-csharp-migration-progress.md`

**Interfaces:**
- Consumes: .NET 10.0.302 SDK installed on the development computer.
- Produces: a buildable solution with project references `IO -> Core`, `Desktop -> Core + IO`, and each test project referencing its matching production project.

- [ ] **Step 1: Confirm the current prerequisite failure**

Run:

```powershell
dotnet --list-sdks
```

Expected before installation: no SDK lines. Record this exact result in the progress file.

- [ ] **Step 2: Obtain user approval and install the .NET 10 SDK**

Install the official Windows x64 .NET SDK 10.0.302. This is an external machine change and requires approval at execution time.

Run after installation:

```powershell
dotnet --version
```

Expected: `10.0.302`.

- [ ] **Step 3: Create the pinned SDK and common build configuration**

`csharp/global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

`csharp/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>14.0</LangVersion>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

`csharp/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MSTest" Version="4.3.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="ScottPlot.WPF" Version="5.1.59" />
    <PackageVersion Include="NPOI" Version="2.7.5" />
    <PackageVersion Include="MatFileHandler" Version="1.3.0" />
    <PackageVersion Include="System.Security.Cryptography.Xml" Version="10.0.10" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Scaffold projects and references**

Run from `csharp`:

```powershell
dotnet new sln -n IPCE
dotnet new classlib -n IPCE.Core -o src/IPCE.Core
dotnet new classlib -n IPCE.IO -o src/IPCE.IO
dotnet new wpf -n IPCE.Desktop -o src/IPCE.Desktop
dotnet new mstest -n IPCE.Core.Tests -o tests/IPCE.Core.Tests
dotnet new mstest -n IPCE.IO.Tests -o tests/IPCE.IO.Tests
dotnet new mstest -n IPCE.Desktop.Tests -o tests/IPCE.Desktop.Tests
dotnet sln IPCE.slnx add src/IPCE.Core/IPCE.Core.csproj
dotnet sln IPCE.slnx add src/IPCE.IO/IPCE.IO.csproj
dotnet sln IPCE.slnx add src/IPCE.Desktop/IPCE.Desktop.csproj
dotnet sln IPCE.slnx add tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj
dotnet sln IPCE.slnx add tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj
dotnet sln IPCE.slnx add tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj
dotnet add src/IPCE.IO/IPCE.IO.csproj reference src/IPCE.Core/IPCE.Core.csproj
dotnet add src/IPCE.Desktop/IPCE.Desktop.csproj reference src/IPCE.Core/IPCE.Core.csproj
dotnet add src/IPCE.Desktop/IPCE.Desktop.csproj reference src/IPCE.IO/IPCE.IO.csproj
dotnet add tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj reference src/IPCE.Core/IPCE.Core.csproj
dotnet add tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj reference src/IPCE.IO/IPCE.IO.csproj
dotnet add tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj reference src/IPCE.Desktop/IPCE.Desktop.csproj
```

Change the desktop and desktop-test target frameworks to
`net10.0-windows10.0.19041.0` and set `<UseWPF>true</UseWPF>`.

- [ ] **Step 5: Add packages only to projects that use them**

Run:

```powershell
dotnet add src/IPCE.IO/IPCE.IO.csproj package NPOI
dotnet add src/IPCE.IO/IPCE.IO.csproj package MatFileHandler
dotnet add src/IPCE.IO/IPCE.IO.csproj package System.Security.Cryptography.Xml
dotnet add src/IPCE.Desktop/IPCE.Desktop.csproj package ScottPlot.WPF
dotnet add tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj package MSTest
dotnet add tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj package Microsoft.NET.Test.Sdk
dotnet add tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj package MSTest
dotnet add tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj package Microsoft.NET.Test.Sdk
dotnet add tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj package MSTest
dotnet add tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj package Microsoft.NET.Test.Sdk
dotnet restore --use-lock-file
dotnet list IPCE.slnx package --vulnerable --include-transitive
```

Expected vulnerability result: no known vulnerable package is reported. Stop
this task and reassess the pinned dependency if the command reports one.

- [ ] **Step 6: Verify the empty solution**

Run:

```powershell
dotnet build IPCE.slnx -c Release
dotnet test IPCE.slnx -c Release --no-build
```

Expected: build succeeds with zero warnings and the template tests pass.

- [ ] **Step 7: Write the first resumable checkpoint**

The progress file must contain:

```markdown
# IPCE C# Migration Progress

- Last completed task: 1
- Status: passing
- Verification: `dotnet test csharp/IPCE.slnx -c Release`
- Next task: 2 - MATLAB golden baseline and domain contracts
- Existing MATLAB application modified: no
```

---

### Task 2: MATLAB Golden Baseline and Domain Contracts

**Files:**
- Create: `csharp/tools/export_csharp_baseline.m`
- Create: `csharp/tests/TestData/Golden/manifest.json`
- Create: generated CSV baselines under `csharp/tests/TestData/Golden/`
- Create: `csharp/src/IPCE.Core/Errors/IpceException.cs`
- Create: `csharp/src/IPCE.Core/Domain/TraceData.cs`
- Create: `csharp/src/IPCE.Core/Domain/CalibrationData.cs`
- Create: `csharp/src/IPCE.Core/Domain/ScheduleModels.cs`
- Create: `csharp/src/IPCE.Core/Domain/ResultModels.cs`
- Create: `csharp/tests/IPCE.Core.Tests/DomainValidationTests.cs`

**Interfaces:**
- Consumes: existing MATLAB functions and real default data.
- Produces: immutable C# domain records and versioned golden CSV files that later tests can load without running MATLAB.

- [ ] **Step 1: Run the unchanged MATLAB oracle**

Run from the repository root:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: MATLAB self-test completes without an error.

- [ ] **Step 2: Write the failing domain validation tests**

Use these public contracts:

```csharp
public sealed record TraceMetadata(
    string TimeHeader,
    string CurrentHeader,
    string OriginalTimeUnit,
    string OriginalCurrentUnit,
    double TimeToSecondsFactor,
    double CurrentToAmperesFactor,
    string RawHeaderText)
{
    public static TraceMetadata Unknown { get; } =
        new("", "", "", "", 1, 1, "");
}

public sealed record TraceData
{
    public TraceData(
        IReadOnlyList<double> timeSeconds,
        IReadOnlyList<double> currentAmperes,
        TraceMetadata metadata)
    {
        double[] times = timeSeconds.ToArray();
        double[] currents = currentAmperes.ToArray();
        if (times.Length < 2 || times.Length != currents.Length ||
            times.Any(v => !double.IsFinite(v)) ||
            currents.Any(v => !double.IsFinite(v)) ||
            times.Zip(times.Skip(1), (a, b) => b - a).Any(d => d < 0) ||
            !times.Zip(times.Skip(1), (a, b) => b - a).Any(d => d > 0))
        {
            throw new IpceException("IPCE:InvalidTrace",
                "i-t 数据必须包含至少两个有限、按时间非递减排列的数据点。");
        }

        TimeSeconds = times;
        CurrentAmperes = currents;
        Metadata = metadata;
    }

    public IReadOnlyList<double> TimeSeconds { get; }
    public IReadOnlyList<double> CurrentAmperes { get; }
    public TraceMetadata Metadata { get; }
}

public readonly record struct CalibrationPoint(
    double WavelengthNm,
    double ResponsivityAmperesPerWatt);

public readonly record struct AnchorPoint(
    double WavelengthNm,
    double ConfirmedTimeSeconds);

public sealed record CalibrationData(
    IReadOnlyList<CalibrationPoint> Points);

public readonly record struct IpceValue(
    double WavelengthNm,
    double IpcePercent);

public enum IpceSource
{
    Calculated,
    External
}

public sealed record ExternalIpceData(
    IReadOnlyList<IpceValue> Points,
    string WavelengthHeader,
    string IpceHeader);

public sealed record IntegrationSummary(
    double MinimumWavelengthNm,
    double MaximumWavelengthNm,
    double IntegratedCurrentDensityMilliamperePerSquareCentimetre,
    double IntegratedPowerWattsPerSquareMetre,
    int IntegrationGridPoints,
    string Interpolation);

public readonly record struct IntegrationCurvePoint(
    double WavelengthNm,
    double IrradianceWattsPerSquareMetrePerNanometre,
    double IpcePercent,
    double EqeFraction,
    double PhotonFluxPerSquareMetreSecondNanometre,
    double SpectralCurrentMilliamperePerSquareCentimetreNanometre,
    double CumulativeCurrentDensityMilliamperePerSquareCentimetre);

public sealed record IntegrationResult(
    IntegrationSummary Summary,
    IReadOnlyList<IntegrationCurvePoint> Curve);
```

Test that mismatched trace lengths, fewer than two trace points, non-finite values, non-positive calibration wavelengths/responsivities, and duplicate anchor wavelengths throw `IpceException` with stable error codes.

Example:

```csharp
[TestMethod]
public void TraceData_MismatchedLengths_ThrowsStableCode()
{
    var error = Assert.ThrowsExactly<IpceException>(() =>
        new TraceData([0, 1], [1e-6], TraceMetadata.Unknown));
    Assert.AreEqual("IPCE:InvalidTrace", error.Code);
}
```

- [ ] **Step 3: Verify the tests fail**

Run:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter DomainValidationTests
```

Expected: FAIL because the domain types do not exist.

- [ ] **Step 4: Implement immutable records and stable errors**

`IpceException`:

```csharp
public sealed class IpceException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}
```

Implement factory methods that copy input lists to arrays before validation so callers cannot mutate accepted data.

- [ ] **Step 5: Export deterministic MATLAB baselines**

`export_csharp_baseline.m` must:

1. Run `run_ipce_selftest`.
2. Load the default calibration, trace, and anchors.
3. Produce the 161-point silicon extraction and power-density results.
4. Produce a synthetic sample with known 20%, 50%, and 80% IPCE.
5. Produce one spectrum integration summary and cumulative curve.
6. Write CSV using explicit variable names and `%.17g`-equivalent precision.
7. Write `manifest.json` containing source filenames, SHA-256 hashes, MATLAB release, row counts, and generation timestamp.

The generated files are exactly:

```text
default_silicon_extracted.csv
default_power_density.csv
synthetic_sample_ipce.csv
integration_summary.csv
integration_curve.csv
manifest.json
```

Run:

```powershell
matlab -batch "addpath('csharp/tools'); export_csharp_baseline"
```

Expected: the manifest and all named baseline files exist and are non-empty.

- [ ] **Step 6: Verify the domain tests pass**

Run:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter DomainValidationTests
```

Expected: PASS.

- [ ] **Step 7: Record checkpoint 2**

Update the progress file with the exact MATLAB and .NET commands, generated baseline filenames, hashes, and next task `3 - numerical primitives`.

---

### Task 3: Linear Interpolation, PCHIP, and Trapezoidal Integration

**Files:**
- Create: `csharp/src/IPCE.Core/Numerics/Interpolation.cs`
- Create: `csharp/src/IPCE.Core/Numerics/TrapezoidalIntegration.cs`
- Create: `csharp/tests/IPCE.Core.Tests/InterpolationTests.cs`
- Create: `csharp/tests/IPCE.Core.Tests/IntegrationPrimitiveTests.cs`

**Interfaces:**
- Consumes: finite, strictly increasing source x-values.
- Produces:
  - `double[] Interpolation.Linear(ReadOnlySpan<double> x, ReadOnlySpan<double> y, ReadOnlySpan<double> query, bool allowExtrapolation)`
  - `double[] Interpolation.Pchip(ReadOnlySpan<double> x, ReadOnlySpan<double> y, ReadOnlySpan<double> query)`
  - `double TrapezoidalIntegration.Integrate(ReadOnlySpan<double> x, ReadOnlySpan<double> y)`
  - `double[] TrapezoidalIntegration.Cumulative(ReadOnlySpan<double> x, ReadOnlySpan<double> y)`

- [ ] **Step 1: Write failing numerical tests**

Cover:

- exact recovery at source points;
- linear interpolation of `[0, 10] -> [0, 20]`;
- linear extrapolation only when explicitly enabled;
- PCHIP monotonicity for monotonic data;
- PCHIP rejection outside coverage;
- MATLAB reference values for a nonuniform source grid;
- trapezoid area of `y = x` on `[0, 1, 2]` equals `2`;
- cumulative final value equals total value.

Example:

```csharp
[TestMethod]
public void Pchip_MonotoneInput_DoesNotOvershoot()
{
    double[] result = Interpolation.Pchip(
        [0, 1, 3], [0, 2, 3], [0.5, 2]);
    CollectionAssert.AreEqual(
        new[] { true, true },
        result.Select(v => v is >= 0 and <= 3).ToArray());
}
```

- [ ] **Step 2: Verify failure**

Run:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "InterpolationTests|IntegrationPrimitiveTests"
```

Expected: FAIL because the numerical classes do not exist.

- [ ] **Step 3: Implement the MATLAB-compatible algorithms**

Implement Fritsch-Carlson/Fritsch-Butland endpoint slopes for shape-preserving cubic Hermite interpolation, binary-search interval selection, linear boundary interpolation, trapezoidal total, and cumulative sums.

Use stable errors:

- `IPCE:InvalidInterpolationInput`
- `IPCE:InterpolationCoverage`
- `IPCE:InvalidIntegrationGrid`

- [ ] **Step 4: Compare against golden numeric values**

Use combined absolute/relative comparison:

```csharp
static void AssertClose(double expected, double actual,
    double relative = 1e-9, double absolute = 1e-12)
{
    double tolerance = Math.Max(absolute, relative * Math.Abs(expected));
    Assert.IsTrue(Math.Abs(expected - actual) <= tolerance,
        $"expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
}
```

- [ ] **Step 5: Verify pass and record checkpoint 3**

Run all core tests. Expected: PASS. Update the progress file and set next task to schedule/extraction.

---

### Task 4: Scan Scheduling and Current Extraction

**Files:**
- Create: `csharp/src/IPCE.Core/Scheduling/ScheduleBuilder.cs`
- Create: `csharp/src/IPCE.Core/Extraction/TraceExtractor.cs`
- Create: `csharp/tests/IPCE.Core.Tests/ScheduleBuilderTests.cs`
- Create: `csharp/tests/IPCE.Core.Tests/TraceExtractorTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum AlignmentMode { FixedDelay, Anchors }

public readonly record struct SchedulePoint(
    double WavelengthNm,
    double ReferenceTimeSeconds,
    double WindowStartSeconds,
    double WindowEndSeconds,
    string AlignmentSource);

public readonly record struct DarkCorrection(
    bool Enabled,
    double StartSeconds,
    double EndSeconds);

public readonly record struct ExtractedPoint(
    double WavelengthNm,
    double MeanCurrentAmperes,
    double PhotoCurrentSignedAmperes,
    double AbsolutePhotoCurrentAmperes,
    double PhotoCurrentStandardErrorAmperes,
    int SampleCount);

public static IReadOnlyList<SchedulePoint> ScheduleBuilder.Build(
    IReadOnlyList<double> wavelengthsNm,
    AlignmentMode mode,
    IReadOnlyList<AnchorPoint> anchors,
    double fixedStartTimeSeconds,
    double nominalDelaySeconds);

public static IReadOnlyList<ExtractedPoint> TraceExtractor.Extract(
    TraceData trace,
    IReadOnlyList<SchedulePoint> schedule,
    double averagingDurationSeconds,
    DarkCorrection darkCorrection);
```

- [ ] **Step 1: Write failing schedule tests**

Test fixed delay, one anchor plus nominal delay, multiple piecewise-linear anchors, endpoint slope extrapolation, duplicate anchors, and non-monotonic generated time.

Include the real expected references:

```csharp
AssertClose(127, schedule.Single(p => p.WavelengthNm == 370).ReferenceTimeSeconds);
AssertClose(168, schedule.Single(p => p.WavelengthNm == 400).ReferenceTimeSeconds);
AssertClose(333, schedule.Single(p => p.WavelengthNm == 500).ReferenceTimeSeconds);
AssertClose(965, schedule.Single(p => p.WavelengthNm == 885).ReferenceTimeSeconds);
```

- [ ] **Step 2: Write failing extraction tests**

Test explicit dark range, dark range outside trace, fewer than two dark samples, stable-window mean, sample standard error, signed versus absolute photocurrent, empty windows, and trace coverage.

- [ ] **Step 3: Run tests and confirm failure**

Expected failure: missing `ScheduleBuilder` and `TraceExtractor`.

- [ ] **Step 4: Implement minimal scheduling and extraction**

Port behavior from `ipceBuildSchedule.m` and `ipceExtractSchedule.m` without changing formula order. Use stable error codes matching the MATLAB identifiers where they exist.

- [ ] **Step 5: Verify**

Run:

```powershell
dotnet test csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj -c Release --filter "ScheduleBuilderTests|TraceExtractorTests"
```

Expected: PASS. Then run all core tests and record checkpoint 4.

---

### Task 5: Power Density, IPCE, Source Selection, and Spectrum Integration

**Files:**
- Create: `csharp/src/IPCE.Core/Calculation/IpceCalculator.cs`
- Create: `csharp/src/IPCE.Core/Calculation/IpceSourceResolver.cs`
- Create: `csharp/src/IPCE.Core/Calculation/SpectrumIntegrator.cs`
- Create: `csharp/tests/IPCE.Core.Tests/IpceCalculatorTests.cs`
- Create: `csharp/tests/IPCE.Core.Tests/IpceSourceResolverTests.cs`
- Create: `csharp/tests/IPCE.Core.Tests/SpectrumIntegratorTests.cs`

**Interfaces:**
- Produces:

```csharp
public readonly record struct PowerDensityPoint(
    double WavelengthNm,
    double SiliconResponsivityAmperesPerWatt,
    double SiliconMeanCurrentAmperes,
    double SiliconPhotoCurrentSignedAmperes,
    double SiliconPhotocurrentAmperes,
    double SiliconPhotoCurrentStandardErrorAmperes,
    double SiliconIlluminatedAreaSquareCentimetres,
    double IncidentPowerDensityWattsPerSquareCentimetre,
    double IncidentPowerDensityStandardError);

public readonly record struct IpcePoint(
    double WavelengthNm,
    double IncidentPowerDensityWattsPerSquareCentimetre,
    bool PowerDensityInterpolated,
    double SamplePhotoCurrentSignedAmperes,
    double SamplePhotocurrentAmperes,
    double SamplePhotocurrentDensityAmperesPerSquareCentimetre,
    double IpcePercent,
    double IpceEstimatedStandardErrorPercent);

public readonly record struct SpectrumPoint(
    double WavelengthNm,
    double IrradianceWattsPerSquareMetrePerNanometre);

public static IReadOnlyList<PowerDensityPoint> IpceCalculator.CalculatePowerDensity(
    CalibrationData calibration,
    IReadOnlyList<ExtractedPoint> siliconExtracted,
    double siliconAreaSquareCentimetres);

public static IReadOnlyList<IpcePoint> IpceCalculator.CalculateIpce(
    IReadOnlyList<PowerDensityPoint> powerDensity,
    IReadOnlyList<ExtractedPoint> sampleExtracted,
    double sampleAreaSquareCentimetres);

public static IReadOnlyList<IpceValue> IpceSourceResolver.Resolve(
    IReadOnlyList<IpcePoint>? calculated,
    ExternalIpceData? external,
    IpceSource source);

public static IntegrationResult SpectrumIntegrator.Integrate(
    IReadOnlyList<IpceValue> ipce,
    IReadOnlyList<SpectrumPoint> spectrum,
    double minimumWavelengthNm,
    double maximumWavelengthNm);
```

- [ ] **Step 1: Write failing power/IPCE tests**

Cover known 20%, 50%, and 80% synthetic results, distinct silicon/sample areas, signed current preservation, PCHIP power interpolation onto a different sample grid, calibration coverage, power coverage, and non-positive responsivity/power errors.

- [ ] **Step 2: Write failing source selection tests**

Test `Calculated`, `External`, missing calculated result, missing external result, and unknown source.

- [ ] **Step 3: Write failing integration tests**

Test:

- analytic constant-IPCE case;
- common coverage rejection;
- IPCE PCHIP plus spectrum linear interpolation;
- inserted integration bounds;
- no clipping of a 120% external IPCE point;
- cumulative final value equals summary;
- monotonic cumulative curve for non-negative IPCE.

- [ ] **Step 4: Verify failure**

Run the three named test classes and expect missing implementation failures.

- [ ] **Step 5: Port formulas exactly**

Use:

```csharp
const double Planck = 6.62607015e-34;
const double SpeedOfLight = 299792458.0;
const double ElementaryCharge = 1.602176634e-19;
const double HcOverQElectronVoltNanometres = 1239.8419843320026;
```

Preserve the expression and operation ordering in `ipceCalculate.m` and
`ipceIntegrateSpectrum.m`. Any change in ordering must first be demonstrated
by a failing golden-data test and documented in `parity-report.json`. Do not
extrapolate calibration, power, or integration data.

- [ ] **Step 6: Verify against MATLAB golden files**

Compare all exported power, IPCE, summary, and cumulative columns with relative tolerance `1e-9` and near-zero absolute tolerance `1e-12`.

- [ ] **Step 7: Run all core tests and record checkpoint 5**

Expected: all core tests PASS. At this checkpoint the complete calculation engine is usable without any UI.

---

### Task 6: Delimited Text, i-t, Anchor, and External IPCE Import

**Files:**
- Create: `csharp/src/IPCE.IO/Tables/TabularData.cs`
- Create: `csharp/src/IPCE.IO/Tables/DelimitedTableReader.cs`
- Create: `csharp/src/IPCE.IO/Import/ItTraceReader.cs`
- Create: `csharp/src/IPCE.IO/Import/AnchorReader.cs`
- Create: `csharp/src/IPCE.IO/Import/ExternalIpceReader.cs`
- Create: `csharp/tests/IPCE.IO.Tests/DelimitedTableReaderTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/AnchorReaderTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/ExternalIpceReaderTests.cs`

**Interfaces:**
- Consumes: `.txt` and `.csv` files with optional headers and mixed delimiters.
- Produces:

```csharp
public sealed record UnitOverrides(
    string TimeUnit,
    string CurrentUnit);

public static TraceData ItTraceReader.Read(
    string path,
    UnitOverrides? overrides = null);

public static IReadOnlyList<AnchorPoint> AnchorReader.Read(string path);

public static ExternalIpceData ExternalIpceReader.Read(string path);
```

The corresponding instance methods may wrap these static contracts for dependency injection, but the argument and return models must not change.

Previously abbreviated signatures are therefore fixed as:

  - `TraceData ItTraceReader.Read(string path, UnitOverrides? overrides = null)`
  - `IReadOnlyList<AnchorPoint> AnchorReader.Read(string path)`
  - `ExternalIpceData ExternalIpceReader.Read(string path)`

- [ ] **Step 1: Write failing parser and unit tests**

Include exact unit cases:

```csharp
[DataRow("ms", 1e-3)]
[DataRow("min", 60.0)]
[DataRow("h", 3600.0)]
public void TimeUnits_ConvertToSeconds(string unit, double factor) { }

[DataRow("mA", 1e-3)]
[DataRow("uA", 1e-6)]
[DataRow("µA", 1e-6)]
[DataRow("μA", 1e-6)]
[DataRow("nA", 1e-9)]
[DataRow("pA", 1e-12)]
public void CurrentUnits_ConvertToAmperes(string unit, double factor) { }
```

Test missing units returns `IPCE:TraceUnitsRequired`; overrides `min/uA` produce `[0, 60]` and `[1e-6, 2e-6]`.

- [ ] **Step 2: Write failing anchor and external-IPCE tests**

Test two numeric columns, optional headers, sorting, duplicate external wavelengths averaged, duplicate anchor wavelengths rejected, and finite 120% external IPCE retained.

- [ ] **Step 3: Verify failure**

Run the four named test classes; expected missing readers.

- [ ] **Step 4: Implement readers**

Parse invariant and current-culture decimal forms without treating thousands separators as delimiters. Preserve raw header text and unit conversion metadata. Parse into temporary arrays and construct domain objects only after validation.

- [ ] **Step 5: Verify and record checkpoint 6**

Run all core and IO tests. Expected: PASS.

---

### Task 7: XLS/XLSX Calibration and Spectrum Import plus Startup Defaults

**Files:**
- Create: `csharp/src/IPCE.IO/Tables/NpoiWorkbookReader.cs`
- Create: `csharp/src/IPCE.IO/Import/CalibrationReader.cs`
- Create: `csharp/src/IPCE.IO/Import/SpectrumReader.cs`
- Create: `csharp/src/IPCE.IO/Startup/DefaultConfiguration.cs`
- Create: `csharp/src/IPCE.IO/Startup/StartupDataResolver.cs`
- Create: `csharp/tests/IPCE.IO.Tests/CalibrationReaderTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/StartupDataResolverTests.cs`

**Interfaces:**
- Produces workbook sheet names and actual header names for UI dropdowns.
- Resolves startup data by exact file name in `AppContext.BaseDirectory`, then embedded resource.

- [ ] **Step 1: Add failing real-file tests**

Test:

- default calibration has at least two positive responsivity points;
- default `.xls` spectrum exposes sheet `Spectra`;
- wavelength column 1 and global-tilt irradiance column 3 are discoverable;
- imported spectrum has more than 100 non-negative points;
- exact startup file overrides embedded data;
- missing override falls back to the embedded resource.
- default dark subtraction is enabled;
- silicon dark range is exactly `0.1-10 s`;
- sample dark range is exactly `50-60 s`;
- silicon/sample illuminated areas default to `0.36/1 cm^2`;
- wavelength range and step default to `300-1100 nm` and `5 nm`;
- nominal Delay and post-confirmation averaging duration default to `8 s`
  and `4 s`;
- spectrum integration defaults to `300-1100 nm`, worksheet `Spectra`,
  wavelength column 1, and irradiance column 3.

- [ ] **Step 2: Verify failure**

Expected: workbook and startup classes are missing.

- [ ] **Step 3: Implement NPOI workbook access**

Use only NPOI 2.7.5 for XLS and XLSX. Register `CodePagesEncodingProvider.Instance` before reading BIFF `.xls`. Normalize formula, numeric, string, blank, and date cells into `TabularData`.

- [ ] **Step 4: Embed the four defaults**

Add linked `EmbeddedResource` items for the calibration, spectrum, silicon trace, and silicon anchor files. Keep exact Unicode file names in `DefaultConfiguration`.

- [ ] **Step 5: Verify and record checkpoint 7**

Run all core and IO tests. Expected: PASS. Record real workbook sheet and column names.

---

### Task 8: XLSX, CSV, and MAT Export

**Files:**
- Create: `csharp/src/IPCE.IO/Export/ExportModels.cs`
- Create: `csharp/src/IPCE.IO/Export/ExportService.cs`
- Create: `csharp/src/IPCE.IO/Export/MatExportWriter.cs`
- Create: `csharp/tests/IPCE.IO.Tests/ExportServiceTests.cs`
- Create: `csharp/tools/verify_csharp_mat_export.m`

**Interfaces:**
- Produces:

```csharp
public enum ExportFormat { Xlsx, Csv, Mat }
public sealed record ExportColumn(string Name, Type DataType, IReadOnlyList<object?> Values);
public sealed record ExportTable(string Name, IReadOnlyList<ExportColumn> Columns);
public IReadOnlyList<string> ExportService.Write(
    IReadOnlyList<ExportTable> tables,
    string outputPath,
    ExportFormat format);
```

- [ ] **Step 1: Write failing export tests**

Test no selection, duplicate sheet names, one XLSX with multiple named sheets, one CSV for one table, suffixed CSV files for multiple tables, MAT top-level `exportData`, locked output file, and post-write non-empty verification.

- [ ] **Step 2: Verify failure**

Expected: export types do not exist.

- [ ] **Step 3: Implement XLSX and CSV**

Use NPOI for XLSX. Write UTF-8 CSV with a BOM for reliable Chinese Excel display. Quote fields containing commas, quotes, CR, or LF. Use temporary files in the target directory and atomically replace only after successful close.

- [ ] **Step 4: Implement Level-5 MAT export**

Use MatFileHandler 1.3.0 to write a scalar `exportData` struct. Each table field is a scalar struct containing:

- `VariableNames`: cell array of column names;
- one field per column containing a numeric, logical, or character/cell array;
- `RowCount`: scalar double.

This structure is intentionally MATLAB-readable without attempting to serialize MATLAB's undocumented `table` object internals.

- [ ] **Step 5: Verify MAT from MATLAB**

`verify_csharp_mat_export.m` must load the file, assert `isfield(data, "exportData")`, verify selected table fields and `VariableNames`, and compare numeric columns.

Run:

```powershell
matlab -batch "addpath('csharp/tools'); verify_csharp_mat_export"
```

Expected: verification succeeds.

- [ ] **Step 6: Verify and record checkpoint 8**

Run all .NET tests plus the MATLAB MAT verification. Expected: PASS.

---

### Task 9: Transactional Session State and Workflow ViewModels

**Files:**
- Create: `csharp/src/IPCE.Desktop/State/SessionState.cs`
- Create: `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`
- Create: `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- Create: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Create: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Create: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/SessionStateTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`

**Interfaces:**
- `SessionState` exposes separate nullable `CalculatedIpce` and `ExternalIpce`.
- Each import method returns a fully validated replacement value before assigning state.
- ViewModels expose commands and observable properties without referencing concrete file dialogs.

- [ ] **Step 1: Write failing transactional-state tests**

Test:

- failed import leaves the prior valid trace unchanged;
- importing external IPCE does not clear calculated IPCE;
- source switching changes only the selected source;
- external integration works in an otherwise empty session;
- recalculating silicon invalidates dependent sample IPCE but does not delete external IPCE.

- [ ] **Step 2: Verify failure**

Expected: session and viewmodels are missing.

- [ ] **Step 3: Implement state and commands**

Implement `INotifyPropertyChanged` directly in `ViewModelBase`. Use async commands only for file I/O; marshal final state assignment to the WPF dispatcher. Put all state-changing operations behind methods that validate first and assign last.

- [ ] **Step 4: Verify**

Run desktop tests without opening a window. Expected: PASS. Record checkpoint 9.

---

### Task 10: WPF Shell, Measurement Controls, and Result Tables

**Files:**
- Modify: `csharp/src/IPCE.Desktop/App.xaml`
- Modify: `csharp/src/IPCE.Desktop/App.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml`
- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- Create: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Create: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Create: `csharp/src/IPCE.Desktop/Services/FileDialogService.cs`
- Create: `csharp/src/IPCE.Desktop/Services/LocalCrashLogger.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`

**Interfaces:**
- Main window data context is `MainViewModel`.
- Left panel exposes silicon, sample, and spectrum/export steps.
- Right tabs expose traces, anchors, power density, IPCE, integration curve, and result table.

- [ ] **Step 1: Write a failing STA smoke test**

Create an STA thread, instantiate `Application` and `MainWindow`, call `Show`, pump the dispatcher, assert `IsLoaded`, close the window, and assert no unhandled exception.

- [ ] **Step 2: Verify failure**

Expected: the redesigned shell and controls do not exist.

- [ ] **Step 3: Implement the shell**

Use a two-column `Grid`; left column is scrollable workflow controls, right column is a tab control. Bind all numeric inputs with explicit validation messages and invariant storage. Keep user-facing labels in Chinese.

- [ ] **Step 4: Implement startup defaults and local crash logging**

Load the four defaults asynchronously after the window is shown. Log unhandled exceptions to:

```text
%LOCALAPPDATA%\IPCEApp\Logs\IPCEApp-yyyyMMdd-HHmmss.log
```

Display the log path without uploading the file.

- [ ] **Step 5: Verify**

Run:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj -c Release --filter MainWindowSmokeTests
```

Expected: PASS. Record checkpoint 10.

---

### Task 11: Interactive Plots, Anchor Editing, and Axis Settings

**Files:**
- Create: `csharp/src/IPCE.Desktop/Plotting/PlotController.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/PlotControllerTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/AnchorEditingTests.cs`

**Interfaces:**
- `PlotController` accepts arrays and labels; it does not read files or mutate calculation state.
- Plot click reports the nearest original sample time to the owning ViewModel.
- Anchor confirmation updates only the selected silicon or sample anchor collection.

- [ ] **Step 1: Write failing plot-model tests**

Test nearest sample lookup, invalid logarithmic limits, reset-to-data limits, silicon/sample anchor isolation, row update, and row append.

- [ ] **Step 2: Verify failure**

Expected: plotting controller is missing.

- [ ] **Step 3: Implement ScottPlot views**

Use ScottPlot.WPF 5.1.59 for zoom, pan, reset, line/scatter plots, and coordinate conversion. Add explicit axis min/max and linear/log toggles. Snap clicks to the nearest time sample before displaying the anchor confirmation dialog.

- [ ] **Step 4: Manual UI verification**

Run the desktop application with the default silicon trace. Verify zoom, pan, reset, nearest-point selection, anchor edit, and separate sample anchors.

- [ ] **Step 5: Automated verification and checkpoint 11**

Run all desktop tests and record the manual checks in the progress file.

---

### Task 12: End-to-End Workflows and Export UI

**Files:**
- Modify: `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Create: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Interfaces:**
- Exposes complete commands for silicon calculation, sample IPCE, external IPCE import, spectrum integration, source selection, and multi-item export.

- [ ] **Step 1: Write failing end-to-end tests**

Test these exact scenarios:

1. Defaults -> silicon extraction -> 161 positive power-density points.
2. Synthetic sample -> expected IPCE values.
3. Empty measurement session -> external IPCE import -> spectrum integration -> export.
4. Both IPCE sources loaded -> switch source -> each result retained.
5. Export selection -> exact `ExternalIPCE`, `SpectrumSummary`, and
   `SpectrumCurve` table names in XLSX, CSV, and MAT formats.

- [ ] **Step 2: Verify failure**

Expected: commands are not wired through the complete workflow.

- [ ] **Step 3: Connect workflows**

Bind progress indicators, disable only commands whose own prerequisites are missing, and keep the independent external post-processing path enabled.

- [ ] **Step 4: Verify**

Run all .NET tests. Expected: PASS. Record checkpoint 12.

---

### Task 13: Full MATLAB Parity Review and Regression Closure

**Files:**
- Create: `csharp/tests/IPCE.Core.Tests/GoldenParityTests.cs`
- Create: `csharp/tests/IPCE.IO.Tests/RealFileRegressionTests.cs`
- Create: `csharp/tests/TestData/Golden/parity-report.json`
- Modify: C# production files only when a new failing parity test demonstrates a mismatch.

**Interfaces:**
- Consumes all MATLAB golden CSV files and real source files.
- Produces a machine-readable report listing every compared column, maximum absolute error, maximum relative error, and pass/fail.

- [ ] **Step 1: Write parity tests before any corrections**

Compare schedule, extracted current, power density, IPCE, integration summary, and cumulative curve. Require exact row counts and names and default `1e-9` relative / `1e-12` absolute tolerance.

- [ ] **Step 2: Run and capture failures**

Run:

```powershell
dotnet test csharp/IPCE.slnx -c Release --logger "trx;LogFileName=parity.trx"
```

Expected: any mismatch is visible by column and row.

- [ ] **Step 3: Correct one demonstrated mismatch at a time**

For each mismatch, preserve the failing test, make the smallest production change, rerun the focused test, then rerun the full suite.

- [ ] **Step 4: Run both complete test suites**

Run:

```powershell
matlab -batch "run_ipce_selftest"
dotnet test csharp/IPCE.slnx -c Release
```

Expected: both PASS. Record checkpoint 13 with `parity-report.json`.

---

### Task 14: Portable Build, Size Gate, and Clean-Windows Acceptance

**Files:**
- Create: `csharp/scripts/build-portable.ps1`
- Create: `csharp/scripts/smoke-test.ps1`
- Create: `csharp/PORTABLE_README_CN.txt`
- Create: `csharp/src/IPCE.Desktop/Assets/THIRD_PARTY_NOTICES.txt`
- Create: `csharp/tests/IPCE.Desktop.Tests/PortablePackageTests.cs`
- Create: `csharp/dist/IPCEApp_Windows_x64.zip` during the build

**Interfaces:**
- `build-portable.ps1` exits nonzero unless tests pass, publish succeeds, smoke test succeeds, ZIP is valid, and ZIP size is below 200 MB.

- [ ] **Step 1: Write a failing package test**

Test that a missing archive, archive at or above `200 * 1024 * 1024` bytes, archive without `IPCEApp.exe`, or archive containing MATLAB Runtime files fails validation.

- [ ] **Step 2: Verify failure**

Expected: build and smoke scripts are missing.

- [ ] **Step 3: Implement self-contained publish**

Before publishing, write `THIRD_PARTY_NOTICES.txt` with package name, pinned
version, copyright attribution, and full license text for ScottPlot.WPF
5.1.59 (MIT), NPOI 2.7.5 (Apache-2.0), and MatFileHandler 1.3.0 (MIT).

Use:

```powershell
dotnet publish csharp/src/IPCE.Desktop/IPCE.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -o csharp/dist/publish
```

Do not enable trimming until a separate test proves WPF, NPOI, MatFileHandler, and ScottPlot behavior is intact.

- [ ] **Step 4: Implement the compiled smoke test**

Add a `--smoke-test` argument that creates the real main window, loads embedded defaults, processes dispatcher events, validates loaded state, and closes with exit code 0.

- [ ] **Step 5: Build and validate the ZIP**

The build script must:

1. run MATLAB self-tests;
2. run all .NET tests;
3. publish to a clean staging directory;
4. copy the Chinese portable readme and third-party notices;
5. run `IPCEApp.exe --smoke-test`;
6. compress the staging directory;
7. extract the ZIP to a fresh temporary directory;
8. rerun the extracted smoke test;
9. reject any file name containing `MATLAB Runtime`, `mcr`, or `v93`;
10. reject a ZIP size greater than or equal to 200 MB.

- [ ] **Step 6: Perform clean Windows 10/11 acceptance**

On a VM with no MATLAB and no .NET runtime:

1. extract the ZIP;
2. start the EXE without elevation;
3. complete default silicon power calculation;
4. import `MBVO-IT-300-600 nm.txt` with
   `MBVO-300-600-match time.txt` and calculate IPCE;
5. complete external-IPCE standalone integration;
6. export XLSX, CSV, and MAT;
7. reopen the XLSX/CSV and verify the MAT on the development MATLAB machine.

Record OS build, archive SHA-256, archive byte size, and results in the progress file.

- [ ] **Step 7: Final verification**

Run:

```powershell
matlab -batch "run_ipce_selftest"
dotnet test csharp/IPCE.slnx -c Release
powershell -ExecutionPolicy Bypass -File csharp/scripts/build-portable.ps1
```

Expected: every command passes and `csharp/dist/IPCEApp_Windows_x64.zip` is below 200 MB.

- [ ] **Step 8: Mark the migration complete**

Update the progress file with:

```markdown
- Last completed task: 14
- Status: complete
- MATLAB regression: passing
- C# regression: passing
- Clean Windows acceptance: passing
- Portable archive: csharp/dist/IPCEApp_Windows_x64.zip
- Archive size: recorded exact byte count
- Existing MATLAB application modified: no
```
