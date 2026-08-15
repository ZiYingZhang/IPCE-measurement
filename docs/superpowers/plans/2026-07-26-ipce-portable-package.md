# IPCE Windows Portable Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify a portable Windows ZIP containing the compiled IPCE application and embedded default data, but no MATLAB Runtime installer.

**Architecture:** Add one pure startup-file resolver so MATLAB development runs can use current-folder files while deployed runs fall back to files embedded under `ctfroot`. Add a testable package manifest and a MATLAB build entry that runs regression tests, invokes `mcc -e`, stages the portable files, and creates a verified ZIP.

**Tech Stack:** MATLAB R2023b Update 6, MATLAB Compiler 23.2, MATLAB programmatic UI, MATLAB `mcc`, MATLAB `zip`/`unzip`.

## Global Constraints

- Target platform is 64-bit Windows.
- Output archive is `IPCEApp_R2023b_Windows_x64.zip`.
- The ZIP must not contain MATLAB Runtime or any Runtime installer.
- End users install MATLAB Runtime R2023b Update 6 or a later R2023b update themselves.
- The four approved default data files are embedded in the deployable archive.
- Missing deployed defaults must not prevent the application from opening or using standalone external-IPCE post-processing.
- IPCE calculations, interpolation, units, and export formats are unchanged.
- This directory is not a Git repository; do not initialize Git or create commits.

---

### Task 1: Resolve startup files in MATLAB and deployed applications

**Files:**
- Create: `ipceResolveStartupFile.m`
- Modify: `ipceDefaultConfig.m`
- Test: `run_ipce_selftest.m`

**Interfaces:**
- Produces: `ipceResolveStartupFile(exactFileName, searchPattern, primaryRoot, fallbackRoot) -> string scalar`
- Produces: `ipceDefaultConfig()` fields `CalibrationFile`, `SpectrumFile`, `SiliconTraceFile`, and `SiliconAnchorFile`
- Consumes: `pwd`, `isdeployed`, and `ctfroot` only when optional roots are omitted

- [ ] **Step 1: Write failing configuration and resolver tests**

Add these assertions to startup-default section 4 of `run_ipce_selftest.m`:

```matlab
assert(defaults.CalibrationFile == ...
    "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx");
assert(defaults.SpectrumFile == "标准太阳能光谱数据.xls");
assert(isfile(defaults.CalibrationFile));
assert(isfile(defaults.SpectrumFile));
```

Create two temporary folders under the current project folder and exercise the wished-for resolver API:

```matlab
resolverRoot = string(tempname(pwd));
primaryRoot = fullfile(resolverRoot, "primary");
fallbackRoot = fullfile(resolverRoot, "fallback");
mkdir(primaryRoot);
mkdir(fallbackRoot);
cleanupResolver = onCleanup(@()removeFolderIfPresent(resolverRoot));

exactName = "default.txt";
writelines("primary", fullfile(primaryRoot, exactName));
writelines("fallback", fullfile(fallbackRoot, exactName));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == string(fullfile(primaryRoot, exactName)));

delete(fullfile(primaryRoot, exactName));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == string(fullfile(fallbackRoot, exactName)));

delete(fullfile(fallbackRoot, exactName));
writelines("similar", fullfile(fallbackRoot, "similar.txt"));
resolved = ipceResolveStartupFile( ...
    exactName, "*.txt", primaryRoot, fallbackRoot);
assert(resolved == "");
```

Add this cleanup helper at the end of `run_ipce_selftest.m`:

```matlab
function removeFolderIfPresent(folderPath)
if isfolder(folderPath)
    rmdir(folderPath, "s");
end
end
```

- [ ] **Step 2: Run the self-test and verify the new test fails**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: failure because `CalibrationFile` or `ipceResolveStartupFile` does not yet exist.

- [ ] **Step 3: Add the two exact default filenames**

Extend the struct in `ipceDefaultConfig.m`:

```matlab
"CalibrationFile", ...
"标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx", ...
"SpectrumFile", "标准太阳能光谱数据.xls", ...
```

Retain all existing defaults exactly.

- [ ] **Step 4: Implement the minimal resolver**

Create `ipceResolveStartupFile.m` with these rules:

```matlab
function filePath = ipceResolveStartupFile( ...
        exactFileName, searchPattern, primaryRoot, fallbackRoot)
%IPCERESOLVESTARTUPFILE Locate one startup file without guessing.

arguments
    exactFileName (1, 1) string
    searchPattern (1, 1) string = exactFileName
    primaryRoot (1, 1) string = string(pwd)
    fallbackRoot (1, 1) string = defaultFallbackRoot()
end

filePath = "";
exactPrimary = fullfile(primaryRoot, exactFileName);
if isfile(exactPrimary)
    filePath = string(exactPrimary);
    return
end

matches = dir(fullfile(primaryRoot, searchPattern));
matches = matches(~[matches.isdir]);
if numel(matches) == 1
    filePath = string(fullfile(matches(1).folder, matches(1).name));
    return
elseif numel(matches) > 1
    return
end

exactFallback = fullfile(fallbackRoot, exactFileName);
if isfile(exactFallback)
    filePath = string(exactFallback);
end
end

function root = defaultFallbackRoot()
if isdeployed
    root = string(ctfroot);
else
    root = string(fileparts(mfilename("fullpath")));
end
end
```

- [ ] **Step 5: Run the full self-test and verify green**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: exit code 0 and `All IPCE self-tests passed.`

There is no commit step because this directory is not a Git repository.
---

### Task 2: Use the resolver during application startup

**Files:**
- Modify: `IPCEApp.m:2040`
- Test: `run_ipce_selftest.m`

**Interfaces:**
- Consumes: `ipceResolveStartupFile` and the four exact names from `ipceDefaultConfig`
- Preserves: the existing `loadCalibration`, `loadSilicon`, `ipceReadAnchors`, and `loadSpectrumFile` error and UI paths

- [ ] **Step 1: Record the production change that the existing resolver tests protect**

The change that must make the resolver test fail is removing the fallback-root branch from
`ipceResolveStartupFile`. No additional mock UI test is added; the real UI smoke test in
Step 4 covers construction and automatic loading.

- [ ] **Step 2: Replace direct current-directory discovery**

At the start of `autoLoadWorkspaceFiles`, resolve each startup path:

```matlab
calibrationPath = ipceResolveStartupFile( ...
    defaults.CalibrationFile, "*校准*.xlsx");
spectrumPath = ipceResolveStartupFile( ...
    defaults.SpectrumFile, "*太阳能光谱*.xls");
siliconPath = ipceResolveStartupFile( ...
    defaults.SiliconTraceFile, defaults.SiliconTraceFile);
anchorPath = ipceResolveStartupFile( ...
    defaults.SiliconAnchorFile, defaults.SiliconAnchorFile);
```

Replace the four existing `dir`, `isfile`, and relative-path uses with the resolved paths.
Treat `""` as missing. Preserve the existing status messages, loading order, and nonfatal
startup behavior.

- [ ] **Step 3: Run the regression self-test**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: exit code 0 and all existing numerical/import/export checks pass.

- [ ] **Step 4: Run the required UI smoke test**

Run:

```powershell
matlab -batch "app=IPCEApp; drawnow; assert(isvalid(app)); close(app);"
```

Expected: exit code 0 with no unhandled error.

There is no commit step because this directory is not a Git repository.

---

### Task 3: Define and test the portable-package manifest

**Files:**
- Create: `ipcePortablePackageConfig.m`
- Modify: `run_ipce_selftest.m`
- Create: `PORTABLE_README_CN.txt`

**Interfaces:**
- Produces: `ipcePortablePackageConfig() -> scalar struct`
- Required fields: `ProjectRoot`, `ReleaseName`, `ArchiveName`, `ExecutableName`, `DefaultFiles`, `PortableReadme`
- Consumes: the exact startup filenames from `ipceDefaultConfig`

- [ ] **Step 1: Write a failing manifest test**

Add a new section before the final self-test success message:

```matlab
packageConfig = ipcePortablePackageConfig();
assert(packageConfig.ReleaseName == "IPCEApp_R2023b_Windows_x64");
assert(packageConfig.ArchiveName == ...
    "IPCEApp_R2023b_Windows_x64.zip");
assert(packageConfig.ExecutableName == "IPCEApp.exe");
assert(numel(packageConfig.DefaultFiles) == 4);
assert(all(isfile(packageConfig.DefaultFiles)));
assert(isfile(packageConfig.PortableReadme));
fprintf("  Portable package manifest: passed\n");
```

- [ ] **Step 2: Run the self-test and verify red**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: failure because `ipcePortablePackageConfig` does not exist.

- [ ] **Step 3: Implement the package manifest**

Create `ipcePortablePackageConfig.m`:

```matlab
function config = ipcePortablePackageConfig
%IPCEPORTABLEPACKAGECONFIG Return the verified portable-build manifest.

projectRoot = string(fileparts(mfilename("fullpath")));
defaults = ipceDefaultConfig();
defaultNames = [ ...
    defaults.CalibrationFile; ...
    defaults.SpectrumFile; ...
    defaults.SiliconTraceFile; ...
    defaults.SiliconAnchorFile];

config = struct( ...
    "ProjectRoot", projectRoot, ...
    "ReleaseName", "IPCEApp_R2023b_Windows_x64", ...
    "ArchiveName", "IPCEApp_R2023b_Windows_x64.zip", ...
    "ExecutableName", "IPCEApp.exe", ...
    "DefaultFiles", fullfile(projectRoot, defaultNames), ...
    "PortableReadme", fullfile(projectRoot, "PORTABLE_README_CN.txt"));
end
```

- [ ] **Step 4: Add the end-user readme**

Create `PORTABLE_README_CN.txt` containing:

- MATLAB Runtime R2023b official download URL:
  `https://www.mathworks.com/products/compiler/matlab-runtime.html`
- Required Runtime level: R2023b Update 6 or a later R2023b update.
- Steps: install Runtime, extract all ZIP contents, double-click `IPCEApp.exe`.
- Note that the first launch can take approximately as long as starting MATLAB.
- Troubleshooting for missing/mismatched Runtime, Windows SmartScreen, and incomplete ZIP extraction.
- A reminder to verify the automatically loaded data batch before calculating.

- [ ] **Step 5: Run the self-test and verify green**

Run:

```powershell
matlab -batch "run_ipce_selftest"
```

Expected: exit code 0, including `Portable package manifest: passed`.

There is no commit step because this directory is not a Git repository.

---

### Task 4: Build and verify the Runtime-free ZIP

**Files:**
- Create: `build_ipce_portable.m`
- Modify: `README_CN.md`
- Generated: `dist/IPCEApp_R2023b_Windows_x64/`
- Generated: `dist/IPCEApp_R2023b_Windows_x64.zip`

**Interfaces:**
- Consumes: `ipcePortablePackageConfig()`
- Produces: a staged release directory and ZIP with identical application contents
- Invokes: `run_ipce_selftest`, `mcc -e`, `zip`, and `unzip`

- [ ] **Step 1: Implement guarded build preparation**

Create `build_ipce_portable.m` as a function. It must:

```matlab
config = ipcePortablePackageConfig();
assert(~isempty(ver("compiler")), ...
    "IPCE:CompilerMissing", "MATLAB Compiler is not installed.");
assert(license("test", "Compiler"), ...
    "IPCE:CompilerLicenseMissing", ...
    "A MATLAB Compiler license is not available.");
assert(all(isfile(config.DefaultFiles)), ...
    "IPCE:PackageDataMissing", ...
    "One or more default package files are missing.");
```

Construct:

```matlab
distRoot = fullfile(config.ProjectRoot, "dist");
releaseDir = fullfile(distRoot, config.ReleaseName);
archivePath = fullfile(distRoot, config.ArchiveName);
```

Before recursively removing `releaseDir`, verify its normalized path is a child of the
normalized `distRoot` and that its final folder name equals `config.ReleaseName`. Remove
only `releaseDir` and `archivePath`, then recreate `releaseDir`.

- [ ] **Step 2: Run regression tests before compilation**

From the build function:

```matlab
originalFolder = string(pwd);
restoreFolder = onCleanup(@()cd(originalFolder));
cd(config.ProjectRoot);
run_ipce_selftest;
```

Any self-test failure must abort the build before calling `mcc`.

- [ ] **Step 3: Compile without a console and embed the defaults**

Build the `mcc` argument cell array and invoke it:

```matlab
mccArguments = { ...
    "-e", char(fullfile(config.ProjectRoot, "IPCEApp.m")), ...
    "-o", "IPCEApp", ...
    "-d", char(releaseDir)};
for fileIndex = 1:numel(config.DefaultFiles)
    mccArguments(end + 1:end + 2) = { ...
        "-a", char(config.DefaultFiles(fileIndex))};
end
mcc(mccArguments{:});
```

Copy `PORTABLE_README_CN.txt` to
`fullfile(releaseDir, "README_运行说明.txt")`. Verify
`fullfile(releaseDir, config.ExecutableName)` exists and is nonempty.

- [ ] **Step 4: Reject Runtime payloads and create the archive**

Recursively inspect staged filenames. Abort if a filename contains
`MATLAB_Runtime`, `MCRInstaller`, or `runtime_installer`, case-insensitively.

Create the ZIP from all files under `releaseDir` with paths relative to `releaseDir`.
Verify the ZIP exists and is nonempty.

- [ ] **Step 5: Verify the ZIP by extraction**

Extract the completed ZIP to a unique temporary folder under `tempdir`. Use
`onCleanup` to remove only that exact temporary folder. Verify:

```matlab
assert(isfile(fullfile(validationRoot, config.ExecutableName)));
```

Recursively scan the extracted archive again for the forbidden Runtime payload names.
Print the absolute release directory, ZIP path, and ZIP byte size only after all checks pass.

- [ ] **Step 6: Document portable deployment**

Add a `## Windows 绿色包` section to `README_CN.md` explaining:

- the ZIP does not include MATLAB Runtime;
- end users must install R2023b Runtime first;
- the four default files are embedded and automatically loaded;
- build command is `build_ipce_portable`;
- generated output is under `dist`;
- clean-machine Runtime-only validation remains the release acceptance test.

- [ ] **Step 7: Run fresh full verification**

Run:

```powershell
matlab -batch "run_ipce_selftest"
matlab -batch "app=IPCEApp; drawnow; assert(isvalid(app)); close(app);"
matlab -batch "build_ipce_portable"
```

Expected: every command exits 0. The build prints a nonempty ZIP path under `dist`.

- [ ] **Step 8: Inspect archive contents outside MATLAB**

Run:

```powershell
$zip = '.\dist\IPCEApp_R2023b_Windows_x64.zip'
$verify = Join-Path $env:TEMP ('IPCE_zip_verify_' + [guid]::NewGuid())
Expand-Archive -LiteralPath $zip -DestinationPath $verify
Get-ChildItem -LiteralPath $verify -Recurse
Get-ChildItem -LiteralPath $verify -Recurse |
    Where-Object { $_.Name -match 'MATLAB_Runtime|MCRInstaller|runtime_installer' }
```

Expected: the listing contains `IPCEApp.exe` and `README_运行说明.txt`; the forbidden
Runtime query returns no rows. Remove only the exact generated verification folder.

- [ ] **Step 9: Launch the compiled executable**

Start `dist/IPCEApp_R2023b_Windows_x64/IPCEApp.exe`, allow Runtime initialization,
confirm that the IPCE window appears, and close it normally. Record any initialization
error rather than claiming deployment success.

There is no commit step because this directory is not a Git repository.
