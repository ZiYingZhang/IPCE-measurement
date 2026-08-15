# IPCE Repository Normalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the current nonstandard project tree into a publication-safe Git repository with `matlab/`, `csharp/`, and shared `data/` directories while preserving all MATLAB/C# behavior and release builds.

**Architecture:** Track a clean pre-migration source baseline first, then move shared data and MATLAB together behind an explicit repository-path contract. Update C# resource embedding and real-file tests to consume the same data roots before renaming `csharp APP` to `csharp`, so every committed task ends with both implementations green.

**Tech Stack:** Git, PowerShell 5.1+, MATLAB R2023b, MATLAB Compiler, .NET SDK 10.0.302, C#/.NET 10, WPF, MSTest.

## Global Constraints

- Do not change physical formulas, interpolation, integration, scheduling, unit conversion, defaults, or export values.
- Keep the four exact startup filenames and deployed fallback behavior.
- Keep calculated and external IPCE state separate; external post-processing must remain independent.
- Do not clip external IPCE or extrapolate beyond common wavelength coverage.
- Preserve the pre-migration C# baseline of Core 58, IO 42, Desktop 97, total
  197; the new repository-layout regression in Task 2 raises IO to 43 and the
  total to 198, with zero failures and skips.
- Keep MATLAB as the numerical oracle and run `run_ipce_selftest` plus a real UI smoke test.
- Keep C# build outputs under `csharp/dist` and MATLAB build outputs under `matlab/dist`; neither is tracked by Git.
- Never commit `bin`, `obj`, `TestResults`, `dist`, `publish`, extracted applications, generated exports, ZIP files, `.superpowers`, `.agents`, or `PROJECT_MEMORY.md`.
- Do not push, tag, create a GitHub Release, or change repository visibility in this plan.
- Preserve all user-owned local generated files; ignore them rather than deleting them.
- Use `apply_patch` for content edits and exact, workspace-contained `git mv` commands for tracked moves.

## Plan Boundary and Follow-on Plans

This plan covers only repository normalization and shared-data routing. After it
passes, create and execute three separate plans in this order:

1. C# WPF bilingual localization;
2. MATLAB bilingual localization; and
3. public documentation, GitHub Actions, packaging, and `v1.0.0` publication.

Each later plan consumes the stable paths produced here:

- MATLAB root: `matlab/`
- C# root: `csharp/`
- startup data: `data/defaults/`
- real example data: `data/examples/`

---

### Task 1: Create a publication-safe pre-migration baseline

**Files:**
- Modify: `.gitignore`
- Create: `.gitattributes`
- Track unchanged: root MATLAB source, `csharp APP/src`, `csharp APP/tests`, `csharp APP/tools`, `csharp APP/scripts`, public data, `README_CN.md`, `AGENTS.md`, and `docs/`

**Interfaces:**
- Consumes: the local root commit containing the approved design specification.
- Produces: a Git baseline in which every intended source/data file is tracked and every generated/local-only path is ignored before any move occurs.

- [ ] **Step 1: Re-run the untouched baseline before staging source**

Run from the repository root:

```powershell
matlab -batch "run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
dotnet build "csharp APP/IPCE.slnx" -c Release --no-restore
dotnet test "csharp APP/IPCE.slnx" -c Release --no-build --no-restore
```

Expected: MATLAB self-test and UI smoke pass; .NET build reports zero warnings
and errors; Core 58, IO 42, Desktop 97 tests pass with zero failures/skips.

- [ ] **Step 2: Replace the root ignore policy**

Use `apply_patch` to make `.gitignore` contain these rules while retaining any
more specific existing MATLAB backup rules:

```gitignore
# Agent/local working state
/.agents/
/.superpowers/
/PROJECT_MEMORY.md

# MATLAB editor, cache, compiler, and generated output
*.asv
*.m~
*.tmp
*.bak
slprj/
codegen/
*.mex*
for_redistribution/
for_redistribution_files_only/
for_testing/
PackagingLog.html
*.ctf
*.exe
/dist/
/matlab/dist/

# .NET/Visual Studio build output
**/bin/
**/obj/
**/TestResults/
**/.vs/
/csharp APP/dist/
/csharp/dist/

# Legacy extracted/manual distributions
/dist APP/

# Generated measurement exports and temporary output
/IPCE_export*.xlsx
/outputs/
/temp/

# Local IDE, operating-system, and secrets
/.vscode/
/.idea/
.env
.env.*
*.key
Thumbs.db
.DS_Store
```

- [ ] **Step 3: Add deterministic text/binary attributes**

Create `.gitattributes` with:

```gitattributes
* text=auto
*.m text eol=crlf
*.cs text eol=crlf
*.xaml text eol=crlf
*.ps1 text eol=crlf
*.md text eol=lf
*.json text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
*.txt text eol=auto
*.csv text eol=lf
/*Si-i t*.txt binary
/MBVO*.txt binary
data/**/*.txt binary
*.xls binary
*.xlsx binary
*.png binary
*.ico binary
*.zip binary
```

- [ ] **Step 4: Verify the ignore contract**

Run:

```powershell
git check-ignore -q ".superpowers"
git check-ignore -q "PROJECT_MEMORY.md"
git check-ignore -q "dist APP"
git check-ignore -q "csharp APP/dist/IPCEApp_Windows_x64.zip"
git check-ignore -q "csharp APP/src/IPCE.Desktop/bin"
git check-ignore -q "csharp APP/tests/IPCE.IO.Tests/TestResults"
git check-ignore -q "IPCE_export.xlsx"
git check-ignore -q "IPCE_export C.xlsx"
```

Expected: every command exits `0`.

- [ ] **Step 5: Stage and audit the source baseline**

Run:

```powershell
git add --all
git diff --cached --check
git status --short
git diff --cached --name-only
```

Then verify no staged file is 10 MiB or larger:

```powershell
$largeStaged = git -c core.quotepath=false diff --cached --name-only --diff-filter=ACM |
    ForEach-Object { Get-Item -LiteralPath $_ -ErrorAction Stop } |
    Where-Object Length -ge 10MB
if ($largeStaged) {
    $largeStaged | Select-Object FullName, Length
    throw "Unexpected staged file at or above 10 MiB."
}
```

Expected: no ignored/generated path is staged and `$largeStaged` is empty.

- [ ] **Step 6: Commit the clean pre-migration baseline**

```powershell
git commit -m "chore: add publication-safe source baseline"
```

Expected: the commit contains source, tests, data, and public documentation but
no build products or local agent state.

---

### Task 2: Move MATLAB and shared data behind one repository-path contract

**Files:**
- Create: `matlab/ipceRepositoryPaths.m`
- Move: all root `*.m` files to `matlab/`
- Move: `PORTABLE_README_CN.txt` to `matlab/PORTABLE_README_CN.txt`
- Move: four startup files to `data/defaults/`
- Move: four MBVO files to `data/examples/`
- Modify after move: `matlab/ipceResolveStartupFile.m`
- Modify after move: `matlab/ipcePortablePackageConfig.m`
- Modify after move: `matlab/run_ipce_selftest.m`
- Modify: `csharp APP/src/IPCE.IO/IPCE.IO.csproj`
- Modify: `csharp APP/tests/IPCE.IO.Tests/TestPaths.cs`
- Modify: `csharp APP/tests/IPCE.IO.Tests/CalibrationReaderTests.cs`
- Modify: `csharp APP/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- Modify: `csharp APP/tests/IPCE.IO.Tests/RealFileRegressionTests.cs`
- Modify: `csharp APP/tests/IPCE.IO.Tests/StartupDataResolverTests.cs`
- Modify: `csharp APP/scripts/build-portable.ps1`
- Modify: `csharp APP/tools/export_csharp_baseline.m`
- Modify: `AGENTS.md`
- Modify locally but do not stage: `PROJECT_MEMORY.md`

**Interfaces:**
- Consumes: exact startup filenames from `ipceDefaultConfig()` and `DefaultConfiguration.Current`.
- Produces: MATLAB `ipceRepositoryPaths() -> struct` with fields `RepositoryRoot`, `MatlabRoot`, `DataRoot`, `DefaultsRoot`, and `ExamplesRoot`; C# test properties `TestPaths.RepositoryRoot`, `DefaultsRoot`, and `ExamplesRoot`.

- [ ] **Step 1: Add failing MATLAB repository-layout assertions**

At the start of `run_ipce_selftest.m`, immediately after the first `fprintf`,
add:

```matlab
paths = ipceRepositoryPaths();
assert(isfolder(paths.MatlabRoot));
assert(isfolder(paths.DefaultsRoot));
assert(isfolder(paths.ExamplesRoot));
assert(string(fileparts(mfilename("fullpath"))) == paths.MatlabRoot);
```

Replace the four `assert(isfile(defaults.*File))` checks with:

```matlab
defaultPaths = fullfile(paths.DefaultsRoot, [ ...
    defaults.CalibrationFile; ...
    defaults.SpectrumFile; ...
    defaults.SiliconTraceFile; ...
    defaults.SiliconAnchorFile]);
assert(all(isfile(defaultPaths)));
defaultAnchors = ipceReadAnchors(defaultPaths(4));
```

Update the supplied-file import section to read `defaultPaths(1)` and
`defaultPaths(3)` rather than scanning the current folder.

- [ ] **Step 2: Add a failing C# shared-data layout test**

Add this test to `StartupDataResolverTests.cs`:

```csharp
[TestMethod]
public void RepositoryLayout_SeparatesDefaultsAndExamples()
{
    Assert.AreEqual(
        Path.Combine(TestPaths.RepositoryRoot, "data", "defaults"),
        TestPaths.DefaultsRoot);
    Assert.AreEqual(
        Path.Combine(TestPaths.RepositoryRoot, "data", "examples"),
        TestPaths.ExamplesRoot);
    Assert.IsTrue(Directory.Exists(TestPaths.DefaultsRoot));
    Assert.IsTrue(Directory.Exists(TestPaths.ExamplesRoot));
}
```

- [ ] **Step 3: Run the focused tests and observe RED**

Run:

```powershell
matlab -batch "run_ipce_selftest"
dotnet test "csharp APP/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" -c Release --no-restore --filter "RepositoryLayout_SeparatesDefaultsAndExamples"
```

Expected: MATLAB fails because `ipceRepositoryPaths` does not exist; C# fails
to compile because `TestPaths.DefaultsRoot` and `ExamplesRoot` do not exist.

- [ ] **Step 4: Create destination directories and verify their absolute paths**

Run from the repository root:

```powershell
$repositoryRoot = (Resolve-Path -LiteralPath ".").Path
$matlabRoot = Join-Path $repositoryRoot "matlab"
$defaultsRoot = Join-Path $repositoryRoot "data\defaults"
$examplesRoot = Join-Path $repositoryRoot "data\examples"
New-Item -ItemType Directory -Force -Path $matlabRoot | Out-Null
New-Item -ItemType Directory -Force -Path $defaultsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $examplesRoot | Out-Null
@($matlabRoot, $defaultsRoot, $examplesRoot) | ForEach-Object {
    $full = [System.IO.Path]::GetFullPath($_)
    if (-not $full.StartsWith(
        $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination escaped repository root: $full"
    }
}
```

Expected: all three checked destinations are inside the repository root.

- [ ] **Step 5: Move the exact MATLAB files with Git**

Run these exact commands:

```powershell
git mv -- "IPCEApp.m" "matlab/IPCEApp.m"
git mv -- "build_ipce_portable.m" "matlab/build_ipce_portable.m"
git mv -- "ipceBuildPostprocessExportItems.m" "matlab/ipceBuildPostprocessExportItems.m"
git mv -- "ipceBuildSchedule.m" "matlab/ipceBuildSchedule.m"
git mv -- "ipceCalculate.m" "matlab/ipceCalculate.m"
git mv -- "ipceDefaultConfig.m" "matlab/ipceDefaultConfig.m"
git mv -- "ipceExtractScan.m" "matlab/ipceExtractScan.m"
git mv -- "ipceExtractSchedule.m" "matlab/ipceExtractSchedule.m"
git mv -- "ipceIntegrateSpectrum.m" "matlab/ipceIntegrateSpectrum.m"
git mv -- "ipcePortablePackageConfig.m" "matlab/ipcePortablePackageConfig.m"
git mv -- "ipceReadAnchors.m" "matlab/ipceReadAnchors.m"
git mv -- "ipceReadExternalIPCE.m" "matlab/ipceReadExternalIPCE.m"
git mv -- "ipceReadIT.m" "matlab/ipceReadIT.m"
git mv -- "ipceReadReference.m" "matlab/ipceReadReference.m"
git mv -- "ipceReadSpectrum.m" "matlab/ipceReadSpectrum.m"
git mv -- "ipceReadSpectrumHeaders.m" "matlab/ipceReadSpectrumHeaders.m"
git mv -- "ipceResolveIPCESource.m" "matlab/ipceResolveIPCESource.m"
git mv -- "ipceResolveStartupFile.m" "matlab/ipceResolveStartupFile.m"
git mv -- "ipceWriteExport.m" "matlab/ipceWriteExport.m"
git mv -- "runIPCEApp.m" "matlab/runIPCEApp.m"
git mv -- "run_ipce_selftest.m" "matlab/run_ipce_selftest.m"
git mv -- "PORTABLE_README_CN.txt" "matlab/PORTABLE_README_CN.txt"
```

Expected: `git status --short` reports renames rather than delete/add pairs for
the unchanged files.

- [ ] **Step 6: Move the exact shared data files with Git**

```powershell
git mv -- "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx" "data/defaults/标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx"
git mv -- "标准太阳能光谱数据.xls" "data/defaults/标准太阳能光谱数据.xls"
git mv -- "Si-i t [300 1100] nm-grating 2-filter.txt" "data/defaults/Si-i t [300 1100] nm-grating 2-filter.txt"
git mv -- "Si-i t [300 1100] nm-grating 2-filter-time match.txt" "data/defaults/Si-i t [300 1100] nm-grating 2-filter-time match.txt"
git mv -- "MBVO time match.txt" "data/examples/MBVO time match.txt"
git mv -- "MBVO-300-600-match time.txt" "data/examples/MBVO-300-600-match time.txt"
git mv -- "MBVO-IT-300-600 nm.txt" "data/examples/MBVO-IT-300-600 nm.txt"
git mv -- "MBVO.txt" "data/examples/MBVO.txt"
```

- [ ] **Step 7: Implement the MATLAB repository-path contract**

Create `matlab/ipceRepositoryPaths.m`:

```matlab
function paths = ipceRepositoryPaths
%IPCEREPOSITORYPATHS Return stable source and shared-data locations.

matlabRoot = string(fileparts(mfilename("fullpath")));
repositoryRoot = string(fileparts(matlabRoot));
dataRoot = fullfile(repositoryRoot, "data");

paths = struct( ...
    "RepositoryRoot", repositoryRoot, ...
    "MatlabRoot", matlabRoot, ...
    "DataRoot", dataRoot, ...
    "DefaultsRoot", fullfile(dataRoot, "defaults"), ...
    "ExamplesRoot", fullfile(dataRoot, "examples"));
end
```

Modify `defaultFallbackRoot()` in `matlab/ipceResolveStartupFile.m` to:

```matlab
function root = defaultFallbackRoot()
if isdeployed
    root = string(ctfroot);
else
    paths = ipceRepositoryPaths();
    root = paths.DefaultsRoot;
end
end
```

Modify the root calculation in `matlab/ipcePortablePackageConfig.m` to:

```matlab
paths = ipceRepositoryPaths();
projectRoot = paths.MatlabRoot;
defaults = ipceDefaultConfig();
defaultNames = [ ...
    defaults.CalibrationFile; ...
    defaults.SpectrumFile; ...
    defaults.SiliconTraceFile; ...
    defaults.SiliconAnchorFile];
```

and set `DefaultFiles` with:

```matlab
"DefaultFiles", fullfile(paths.DefaultsRoot, defaultNames), ...
```

- [ ] **Step 8: Update MATLAB tests and the golden-baseline exporter**

In `matlab/run_ipce_selftest.m`, replace every direct startup filename read
with the corresponding `defaultPaths` element and use:

```matlab
exampleTracePath = fullfile(paths.ExamplesRoot, "MBVO-IT-300-600 nm.txt");
exampleAnchorPath = fullfile(paths.ExamplesRoot, ...
    "MBVO-300-600-match time.txt");
```

In `csharp APP/tools/export_csharp_baseline.m`, establish the MATLAB path and
shared defaults with:

```matlab
toolDirectory = string(fileparts(mfilename("fullpath")));
csharpDirectory = string(fileparts(toolDirectory));
repositoryRoot = string(fileparts(csharpDirectory));
matlabRoot = fullfile(repositoryRoot, "matlab");
addpath(matlabRoot);
paths = ipceRepositoryPaths();
outputDirectory = fullfile(csharpDirectory, "tests", "TestData", "Golden");

originalDirectory = string(pwd);
restoreDirectory = onCleanup(@()cd(originalDirectory));
cd(matlabRoot);
run_ipce_selftest;

defaults = ipceDefaultConfig();
calibration = ipceReadReference(fullfile( ...
    paths.DefaultsRoot, defaults.CalibrationFile));
siliconTrace = ipceReadIT(fullfile( ...
    paths.DefaultsRoot, defaults.SiliconTraceFile));
anchors = ipceReadAnchors(fullfile( ...
    paths.DefaultsRoot, defaults.SiliconAnchorFile));
```

Make the C# folder name dynamic so this tool works both before and after Task 3:

```matlab
[~, csharpFolderName] = fileparts(csharpDirectory);
sourcePaths = [
    fullfile("data", "defaults", defaults.CalibrationFile)
    fullfile("data", "defaults", defaults.SpectrumFile)
    fullfile("data", "defaults", defaults.SiliconTraceFile)
    fullfile("data", "defaults", defaults.SiliconAnchorFile)
    fullfile(csharpFolderName, "tools", "export_csharp_baseline.m")
    fullfile("matlab", "run_ipce_selftest.m")
];
```

Hash every entry from `repositoryRoot` and set `manifest.generator` to:

```matlab
manifest.generator = fullfile( ...
    csharpFolderName, "tools", "export_csharp_baseline.m");
```

- [ ] **Step 9: Update C# embedded resources and test paths**

In `csharp APP/src/IPCE.IO/IPCE.IO.csproj`, prefix each embedded source with
`data\defaults` while leaving every `LogicalName` unchanged. Example:

```xml
<EmbeddedResource Include="..\..\..\data\defaults\标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx">
  <LogicalName>IPCE.IO.Defaults.标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx</LogicalName>
</EmbeddedResource>
```

Use the same path pattern for the spectrum, silicon trace, and silicon anchor.

Replace `TestPaths.cs` with:

```csharp
namespace IPCE.IO.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string DefaultsRoot { get; } = Path.Combine(
        RepositoryRoot,
        "data",
        "defaults");

    public static string ExamplesRoot { get; } = Path.Combine(
        RepositoryRoot,
        "data",
        "examples");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            bool hasMatlab = File.Exists(Path.Combine(
                directory.FullName,
                "matlab",
                "ipceDefaultConfig.m"));
            bool hasDefaults = Directory.Exists(Path.Combine(
                directory.FullName,
                "data",
                "defaults"));
            if (hasMatlab && hasDefaults)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the IPCE repository root.");
    }
}
```

Update `CalibrationReaderTests.cs` and `SpectrumReaderTests.cs` to combine
filenames with `TestPaths.DefaultsRoot`. Update `RealFileRegressionTests.cs` to
use `DefaultsRoot` for calibration/spectrum/silicon files and `ExamplesRoot`
for MBVO files.

- [ ] **Step 10: Update the C# release script's MATLAB working directory**

In `csharp APP/scripts/build-portable.ps1`, add:

```powershell
$matlabDirectory = Join-Path $repositoryRoot "matlab"
```

Replace the MATLAB command with:

```powershell
$matlabCommand = "cd('" + $matlabDirectory.Replace("'", "''") +
    "'); run_ipce_selftest; app = IPCEApp; drawnow; " +
    "assert(isvalid(app)); close(app)"
& matlab -batch $matlabCommand
Assert-LastExitCode -Operation "MATLAB regression"
```

- [ ] **Step 11: Update active agent routing before the next task**

Update `AGENTS.md` so the active MATLAB and data routes use `matlab/`,
`data/defaults/`, and `data/examples/`. Keep the C# route as `csharp APP/`
until Task 3 performs that rename. Update the ignored local
`PROJECT_MEMORY.md` with the same active MATLAB/data routing.

Stage `AGENTS.md` in this task; do not stage `PROJECT_MEMORY.md`.

- [ ] **Step 12: Run focused GREEN verification**

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest"
dotnet test "csharp APP/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" -c Release --no-restore --filter "RepositoryLayout_SeparatesDefaultsAndExamples|StartupDataResolverTests|CalibrationReaderTests|SpectrumReaderTests|RealFileRegressionTests"
```

Expected: MATLAB self-test passes; all selected IO tests pass.

- [ ] **Step 13: Run full verification before committing**

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
dotnet build "csharp APP/IPCE.slnx" -c Release --no-restore
dotnet test "csharp APP/IPCE.slnx" -c Release --no-build --no-restore
git diff --check
```

Expected: MATLAB passes; .NET reports 198/198 with zero failures/skips.

- [ ] **Step 14: Commit the shared-data and MATLAB migration**

```powershell
git add --all
git diff --cached --check
git commit -m "refactor: normalize MATLAB and shared data paths"
```

---

### Task 3: Rename the C# project root without hard-coded path regressions

**Files:**
- Move: `csharp APP/` to `csharp/`
- Test after move: `csharp/tests/IPCE.IO.Tests/StartupDataResolverTests.cs`
- Modify: `AGENTS.md`
- Modify locally but do not stage: `PROJECT_MEMORY.md`

**Interfaces:**
- Consumes: structural repository discovery from Task 2 and C# project-relative build scripts.
- Produces: exact C# solution path `csharp/IPCE.slnx`; no production/test code may require the former directory name.

- [ ] **Step 1: Add a failing normalized-C#-root test**

Add to `StartupDataResolverTests.cs` before the move:

```csharp
[TestMethod]
public void RepositoryLayout_UsesNormalizedCSharpDirectory()
{
    string normalizedRoot = Path.Combine(TestPaths.RepositoryRoot, "csharp");

    Assert.IsTrue(File.Exists(Path.Combine(normalizedRoot, "IPCE.slnx")));
    Assert.IsFalse(Directory.Exists(Path.Combine(
        TestPaths.RepositoryRoot,
        "csharp APP")));
}
```

- [ ] **Step 2: Run the focused test and observe RED**

```powershell
dotnet test "csharp APP/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" -c Release --no-restore --filter "RepositoryLayout_UsesNormalizedCSharpDirectory"
```

Expected: FAIL because `csharp/IPCE.slnx` does not exist and `csharp APP` does.

- [ ] **Step 3: Verify the exact source and destination paths**

```powershell
$repositoryRoot = (Resolve-Path -LiteralPath ".").Path
$source = (Resolve-Path -LiteralPath "csharp APP").Path
$destination = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "csharp"))
if ($source -ne (Join-Path $repositoryRoot "csharp APP")) {
    throw "Unexpected C# source path: $source"
}
if ($destination -ne (Join-Path $repositoryRoot "csharp")) {
    throw "Unexpected C# destination path: $destination"
}
if (Test-Path -LiteralPath $destination) {
    throw "C# destination already exists: $destination"
}
```

- [ ] **Step 4: Rename the tracked C# root**

```powershell
git mv -- "csharp APP" "csharp"
```

Expected: the exact folder becomes `csharp`; ignored build products remain
ignored after the move.

- [ ] **Step 5: Update active C# routing before any later task**

Update every active `csharp APP` route and command in `AGENTS.md` to `csharp`.
Update the repository-status section to say:

```text
As of 2026-08-15 this directory is a Git repository on main with origin
https://github.com/ZiYingZhang/IPCE-measurement.git. Remote pushes, tags,
Releases, and visibility changes require explicit owner approval.
```

Apply the same current C# routing to ignored local `PROJECT_MEMORY.md`. Stage
`AGENTS.md`; do not stage `PROJECT_MEMORY.md`.

- [ ] **Step 6: Verify repository-relative baseline metadata**

Inspect `csharp/tools/export_csharp_baseline.m` and confirm the dynamic
`csharpFolderName` now resolves these manifest paths exactly:

```matlab
sourcePaths = [
    fullfile("data", "defaults", defaults.CalibrationFile)
    fullfile("data", "defaults", defaults.SpectrumFile)
    fullfile("data", "defaults", defaults.SiliconTraceFile)
    fullfile("data", "defaults", defaults.SiliconAnchorFile)
    fullfile("csharp", "tools", "export_csharp_baseline.m")
    fullfile("matlab", "run_ipce_selftest.m")
];
```

Run the Core golden-parity tests to ensure the move did not change numerical
fixtures:

```powershell
dotnet test "csharp/tests/IPCE.Core.Tests/IPCE.Core.Tests.csproj" -c Release --no-restore --filter "GoldenParityTests"
```

Expected: all golden-parity tests pass without regenerating numerical CSVs.

- [ ] **Step 7: Run focused and full GREEN verification**

```powershell
dotnet test "csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj" -c Release --no-restore --filter "RepositoryLayout_UsesNormalizedCSharpDirectory|RepositoryLayout_SeparatesDefaultsAndExamples"
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "cd('matlab'); run_ipce_selftest"
```

Expected: both layout tests pass; .NET remains 198/198; MATLAB passes.

- [ ] **Step 8: Commit the C# root rename**

```powershell
git add --all
git diff --cached --check
git commit -m "refactor: normalize C# project path"
```

---

### Task 4: Update current documentation and project routing

**Files:**
- Modify: `README_CN.md`
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Review without rewriting historical statements: existing dated files under `docs/superpowers/specs/` and `docs/superpowers/plans/`

**Interfaces:**
- Consumes: final normalized paths from Tasks 2 and 3.
- Produces: current user/developer commands that work from a fresh clone; historical dated documents remain accurate records of their original context.

- [ ] **Step 1: Add failing documentation command checks**

Run:

```powershell
$currentDocs = @(
    "README_CN.md",
    "docs/superpowers/progress/ipce-csharp-migration-progress.md"
)
$stale = Select-String -Path $currentDocs -Pattern 'csharp APP|dotnet .*csharp APP|matlab -batch "run_ipce_selftest'
if (-not $stale) {
    throw "Expected stale current-document paths before migration update."
}
```

Expected: stale references are found, demonstrating the check detects the old
current commands.

- [ ] **Step 2: Update the current Chinese README**

Make these command/path rules explicit in `README_CN.md`:

```text
MATLAB 源码目录：matlab/
C# WPF 源码目录：csharp/
共享启动数据：data/defaults/
真实示例数据：data/examples/
```

Use these exact commands:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
matlab -batch "cd('matlab'); build_ipce_portable"
```

State that generated packages are under `csharp/dist` and `matlab/dist`, are
ignored by Git, and are intended for GitHub Releases.

- [ ] **Step 3: Update the active progress handoff**

In `docs/superpowers/progress/ipce-csharp-migration-progress.md`, update only
the active environment, commands, current paths, and next-step sections. Keep
dated checkpoint narratives unchanged when they describe the former path.

- [ ] **Step 4: Verify current docs contain no stale active commands**

```powershell
$currentDocs = @(
    "README_CN.md",
    "docs/superpowers/progress/ipce-csharp-migration-progress.md"
)
$staleCommands = Select-String -Path $currentDocs -Pattern 'dotnet (build|test) .*csharp APP|File "csharp APP/scripts|matlab -batch "run_ipce_selftest'
if ($staleCommands) {
    $staleCommands
    throw "Current documentation still contains stale executable commands."
}
```

Expected: no stale executable command is reported. Historical prose may still
mention `csharp APP` when clearly labeled as a dated former path.

- [ ] **Step 5: Run source validation and commit documentation routing**

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest"
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
git add -- "README_CN.md" "docs/superpowers/progress/ipce-csharp-migration-progress.md"
git diff --cached --check
git commit -m "docs: route development through normalized layout"
```

Expected: MATLAB passes; .NET reports 198 passed; ignored
`PROJECT_MEMORY.md` is not in the commit.

---

### Task 5: Verify portable builds and audit the normalized repository

**Files:**
- Generated but ignored: `csharp/dist/**`
- Generated but ignored: `matlab/dist/**`
- Inspect: `csharp/dist/IPCEApp_Windows_x64.build.json`
- Inspect: `matlab/dist/IPCEApp_R2023b_Windows_x64.zip`
- No source file is expected to change in this task.

**Interfaces:**
- Consumes: normalized source, shared data, tests, and build scripts from Tasks 1-4.
- Produces: evidence that a clean Git checkout can build/test both implementations and that generated release outputs stay outside Git history.

- [ ] **Step 1: Run the final source gates**

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Expected: .NET build has zero warnings/errors; Core 58, IO 43, Desktop 97 pass;
MATLAB self-test and UI smoke pass.

- [ ] **Step 2: Rebuild and inspect the C# portable package**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
Get-Content -Raw -Encoding UTF8 "csharp/dist/IPCEApp_Windows_x64.build.json"
```

Expected: published and extracted smoke exit codes are `0`, `selfContained` is
`true`, `matlabRuntimeIncluded` is `false`, and the recorded test totals are
Core 58, IO 43, Desktop 97, total 198.

- [ ] **Step 3: Rebuild and inspect the MATLAB portable package**

```powershell
matlab -batch "cd('matlab'); build_ipce_portable"
Get-Item -LiteralPath "matlab/dist/IPCEApp_R2023b_Windows_x64.zip" |
    Select-Object FullName, Length, LastWriteTime
Get-FileHash -Algorithm SHA256 -LiteralPath "matlab/dist/IPCEApp_R2023b_Windows_x64.zip"
```

Expected: build, extraction verification, and Runtime-payload rejection pass;
the ZIP is nonempty and a fresh SHA-256 is printed.

- [ ] **Step 4: Audit the Git index for forbidden content**

```powershell
$forbidden = git ls-files | Select-String -Pattern '(^|/)(bin|obj|TestResults|dist|publish|dist APP|\.superpowers|\.agents)(/|$)|(^|/)PROJECT_MEMORY\.md$|IPCE_export.*\.xlsx$|\.zip$'
if ($forbidden) {
    $forbidden
    throw "Forbidden generated/local content is tracked."
}
```

Expected: `$forbidden` is empty.

- [ ] **Step 5: Audit tracked file sizes and repository state**

```powershell
$oversized = git -c core.quotepath=false ls-files | ForEach-Object {
    Get-Item -LiteralPath $_ -ErrorAction Stop
} | Where-Object Length -ge 10MB
if ($oversized) {
    $oversized | Select-Object FullName, Length
    throw "Tracked file at or above 10 MiB requires explicit review."
}
git diff --check
git status --short
git log --oneline --decorate -5
git remote -v
```

Expected: no oversized tracked file; no uncommitted tracked change; generated
packages do not appear in status; `origin` is the approved private repository.

- [ ] **Step 6: Record the execution handoff without pushing**

Report:

- exact final commit ID;
- .NET build and 198-test result;
- MATLAB self-test/UI-smoke result;
- fresh C# archive path, size, SHA-256, entry count, and both smoke codes;
- fresh MATLAB archive path, size, and SHA-256;
- Git forbidden-content audit result; and
- the three still-pending follow-on plans.

Do not run `git push`, create a tag or Release, or change visibility.

## Completion Criteria

This plan is complete only when:

- `matlab/IPCEApp.m`, `csharp/IPCE.slnx`, `data/defaults`, and `data/examples`
  exist at the exact target paths;
- neither root-level MATLAB source nor `csharp APP` remains;
- MATLAB development startup and compiled fallback still resolve all four exact
  defaults;
- C# embeds those same four shared files with unchanged logical resource names;
- MATLAB and all 198 .NET tests pass;
- both portable-build workflows pass from the normalized paths;
- generated outputs remain ignored and absent from Git history; and
- all changes are committed locally on `main` without a remote push.
