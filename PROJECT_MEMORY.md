# Project Memory

Last updated: 2026-08-21

## Current release handoff — v1.0.0

The repository is now public on GitHub at
`https://github.com/ZiYingZhang/IPCE-measurement`.

- `main` contains the bilingual source documentation, MIT license, and the
  current public example-data replacement (`sample-*` files under
  `data/examples/`).
- Tag `v1.0.0` has been pushed and the public Release is:
  `https://github.com/ZiYingZhang/IPCE-measurement/releases/tag/v1.0.0`.
- Release assets (do not move them into tracked source `dist/` folders):
  - C# WPF: `IPCEApp_Windows_x64.zip`, 85,511,830 bytes,
    SHA-256 `df7998e581d8125dfd38498f7bc5d34d75750bf87b7f509270c83be427871326`.
  - MATLAB: `IPCEApp_R2023b_Windows_x64.zip`, 790,179 bytes,
    SHA-256 `21b16b20ad240e5269281b1bf076e5ceb45b071ac9d0b4ebe3a251af9352a897`.
- Generated `matlab/dist/` and `csharp/dist/` outputs are intentionally
  ignored by `.gitignore`; compiled ZIPs are published as GitHub Release
  assets. Do not force-add them to `main` unless the owner explicitly changes
  this policy.
- The C# ZIP is self-contained `win-x64` and needs neither MATLAB nor .NET;
  the MATLAB ZIP requires 64-bit MATLAB Runtime R2023b.
- Release verification: MATLAB self-test/UI smoke passed; C# tests passed
  Core 58, IO 44, Desktop 130 (232 total, zero failures/skips); both archives
  were extracted and smoke-checked.
- External acceptance gates remain pending: clean Windows 10/11 machine/VM
  without MATLAB/.NET, and physical plot review at 100%, 125%, and 150%
  Windows scaling.

## Git collaboration rules

- Local edits do not automatically synchronize with GitHub. A file changes
  GitHub only after it is committed and pushed to the selected remote branch.
- Before editing, run `git pull --rebase origin main` when the working tree is
  clean. After editing, inspect `git status` and `git diff`, then stage only
  intended files, commit with a descriptive message, and run `git push origin
  main`.
- Keep generated build output ignored. Build ZIPs locally and attach them to a
  new GitHub Release/tag rather than committing `dist/` trees.
- If Git reports dubious ownership for this Windows workspace, run once:
  `git config --global --add safe.directory "E:/Research Library/Data/Codes/IPCE measurement"`.
- Do not overwrite remote work. If push reports `fetch first`, run
  `git fetch origin main`, inspect `git log main..origin/main`, then merge or
  rebase deliberately before pushing.
- Future pushes, tags, Releases, or visibility changes still require explicit
  owner approval. The v1.0.0 push/release is already complete.
## Current handoff — read this first

The MATLAB application under `matlab/` remains the numerical oracle and
compatibility reference. The current compact Windows deliverable is the
self-contained C# WPF application under the exact folder `csharp`. Shared
startup files are under `data/defaults/`; current public sample inputs are under
`data/examples/`.

Future agents should begin in this order:

1. Read `AGENTS.md`.
2. Read this file.
3. Read `README_CN.md` for workflows, formulas, and user-facing behavior.
4. Read `docs/superpowers/progress/ipce-csharp-migration-progress.md` for the
   latest commands, counts, artifact hash, and pending release gates.
5. Open only the source and tests routed by the task; do not scan `bin`, `obj`,
   `dist`, or all `ipce*.m` files indiscriminately.

The folder is a Git repository on `main` with origin
`https://github.com/ZiYingZhang/IPCE-measurement.git`. Local commits are
authorized for the approved normalization plan. Future pushes, tags, Releases, and visibility changes require explicit owner approval; the v1.0.0 push and Release are complete.

## Current implementation map

### MATLAB reference

- `matlab/IPCEApp.m` is the programmatic UI.
- `matlab/run_ipce_selftest.m` is the numerical regression entry point.
- `matlab/ipceReadIT.m`, `matlab/ipceCalculate.m`, and
  `matlab/ipceIntegrateSpectrum.m` remain the reference
  import/calculation/integration path.
- `matlab/ipceRepositoryPaths.m` owns repository and shared-data roots.
- The C# migration work did not intentionally modify the MATLAB application.

## Current MATLAB maintenance workflow

MATLAB remains a supported implementation, not disposable migration debris. It
serves three roles:

1. numerical oracle for formulas and golden parity;
2. original programmatic UI for direct MATLAB use;
3. Runtime-based fallback deployment route built with MATLAB Compiler.

### Source routing

- `matlab/IPCEApp.m`: UI composition, state, file selection, plotting, export actions,
  and recoverable user warnings.
- `matlab/ipceReadIT.m`, `matlab/ipceReadReference.m`,
  `matlab/ipceReadExternalIPCE.m`, `matlab/ipceReadSpectrum*.m`, and
  `matlab/ipceReadAnchors.m`: input parsing and metadata.
- `matlab/ipceBuildSchedule.m`, `matlab/ipceExtractSchedule.m`, and
  `matlab/ipceExtractScan.m`:
  wavelength/time mapping and averaging windows.
- `matlab/ipceCalculate.m`: power-density and IPCE calculations.
- `matlab/ipceIntegrateSpectrum.m` and
  `matlab/ipceResolveIPCESource.m`: selected-source
  spectrum integration without extrapolation.
- `matlab/ipceBuildPostprocessExportItems.m` and
  `matlab/ipceWriteExport.m`: independent
  post-processing and verified XLSX/CSV/MAT output.
- `matlab/run_ipce_selftest.m`: numerical, import, export, default-data, and packaging
  regression coverage.
- `matlab/runIPCEApp.m`: deployed zero-output launcher and real-UI smoke entry point.

Open only the narrowest source file required by the task. Do not move unrelated
MATLAB logic into `IPCEApp.m`.

### MATLAB change protocol

1. Add or adjust a regression in `run_ipce_selftest.m`; verify RED when
   production behavior is changing.
2. Make the smallest MATLAB production change.
3. Run the focused reproduction, then `run_ipce_selftest`.
4. For UI changes, construct `IPCEApp`, call `drawnow`, assert `isvalid`, and
   close it.
5. If numerical formulas, defaults, unit conversion, interpolation, scheduling,
   or exported semantics change, review and deliberately update the C# golden
   fixtures/tests or document an intentional divergence.
6. For deployment changes, rebuild the MATLAB ZIP and verify the extracted
   executable on a matching Runtime installation.

Commands:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
matlab -batch "cd('matlab'); build_ipce_portable"
```

### MATLAB Compiler packaging

- `matlab/ipcePortablePackageConfig.m` defines
  `IPCEApp_R2023b_Windows_x64`, the ZIP/executable names, the portable readme,
  and four embedded defaults: calibration, spectrum, silicon i-t, and silicon
  anchors.
- `matlab/build_ipce_portable.m` requires MATLAB Compiler and a Compiler license,
  runs `run_ipce_selftest`, invokes `mcc -e` on `runIPCEApp.m`, copies the
  Chinese runtime instructions, rejects Runtime installer payloads, creates the
  archive, extracts it to a GUID temporary directory, and verifies the
  executable exists.
- MATLAB-generated releases are authoritative under `matlab/dist/`, not
  `csharp/dist` and not `dist APP`.
- The MATLAB ZIP deliberately excludes MATLAB Runtime. The target computer
  needs 64-bit MATLAB Runtime R2023b Update 6 or a later R2023b update.
- The MATLAB deployment gate is a clean Windows machine with no MATLAB
  installation and only the matching R2023b Runtime. Do not confuse it with
  the C# gate, which requires neither MATLAB nor .NET Runtime.

The C# self-contained package remains the primary compact Windows deliverable.
The MATLAB Compiler route remains supported as a reference and fallback route.

### C# Windows application

- `csharp/IPCE.slnx` is the solution root.
- `csharp/src/IPCE.Core` contains calculation, extraction, scheduling, and
  numerical code.
- `csharp/src/IPCE.IO` contains import/export code; `Import/ItTraceReader.cs`
  owns i-t unit detection and conversion.
- `csharp/src/IPCE.Desktop` contains the WPF UI, state, view models, and
  plotting.
- `csharp/tests` contains Core, IO, and Desktop regression suites.
- `csharp/scripts/build-portable.ps1` is the authoritative release builder.
- `csharp/dist/IPCEApp_Windows_x64.build.json` is the authoritative latest
  release manifest.
- `dist APP` contains extracted/manual distribution copies; it is not the
  authoritative source of build evidence.

Never reintroduce the former `csharp APP` path. Tests that locate the C# project
walk upward from the test base directory and identify the project root using
`IPCE.slnx` plus the expected local structure.

## Non-negotiable workflows and invariants

The measurement path is:

`silicon calibration + silicon i-t → monochromatic power density`

`power density + sample i-t → sample IPCE`

The standalone post-processing path is:

`external wavelength/IPCE + solar spectrum → integrated and cumulative current density`

External-IPCE processing must continue to work without calibration,
silicon/sample i-t, or anchors. Calculated and external IPCE remain separate
state and are selected explicitly as the integration source.

i-t internal values are always seconds/amperes. Supported header units are:

- time: `s`, `sec`, `second`, `ms`, `min`, `h`;
- current: `A`, `mA`, `uA`, `µA`, `μA`, `nA`, `pA`.

Convert recognized headers to canonical `s/A`, preserve original headers,
units, and conversion factors, and require explicit unit selection when units
are missing. Never infer units from numeric magnitude.

Time-alignment files remain wavelength `nm` and confirmed time `s`. New sample
workflows default to anchor alignment; users may explicitly choose fixed delay.
Anchor import must not reset that user choice.

Do not extrapolate beyond common IPCE/spectrum wavelength coverage, do not clip
finite external IPCE to 0–100%, and do not let display-only settings alter
calculation or export values.

## Current C# plot behavior

- Shared font sizes: title `26`, axis labels `24`, ticks `20`, legend `20`.
- Hover and toolbar text: `14`.
- Dark-current region: grey `#607D8B`, opacity `0.28`.
- Integration region: blue `#90CAF9`, opacity `0.24`.
- Each region has two boundary lines at width `3`.
- i-t mean-current windows and dark-current regions remain visible plot layers.
- Viewport filtering is display-only; users can restore the full data range.

## Required development workflow

For any functional change:

1. Locate the narrowest relevant production file and existing test class.
2. Add or adjust a behavior-level regression test first; verify RED when
   production behavior is changing.
3. Make the smallest production change.
4. Run the focused test, then the complete affected suite.
5. Before completion, run the full Release build and all .NET tests.
6. For release changes, run MATLAB regression/UI smoke and rebuild the package.
7. Read the fresh build manifest instead of copying prior evidence.
8. Keep the two external acceptance gates visibly pending until performed.

Commands:

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
```

Current expected .NET counts are Core `58`, IO `44`, Desktop `130`, total `232`.

## Latest verified C# portable artifact

Generated from the normalized 2026-08-15 source state at `d48479b`:

- path: `csharp/dist/IPCEApp_Windows_x64.zip`;
- size: `85,487,368` bytes;
- SHA-256:
  `312fbd5995489a6d4f088c6611722add8ffab3df07ae8236e1f2c4359df635c1`;
- entries: `440`;
- self-contained `win-x64`: yes;
- MATLAB Runtime included: no;
- published and extracted smoke exit codes: `0`;
- tests: Core `58`, IO `44`, Desktop `97`, total `199`, failed/skipped `0`;
- MATLAB numerical self-test and MATLAB UI smoke: passed.

Latest verified MATLAB Compiler artifact:

- path: `matlab/dist/IPCEApp_R2023b_Windows_x64.zip`;
- size: `770,752` bytes;
- SHA-256:
  `7c4242de843c963e1f1da99889483703f7c41cf5d9799266aaeadbb95d811b22`;
- entries: `7`;
- root executable: present;
- Runtime/installer markers: `0`;
- build and extraction verification: passed.

Treat this evidence as historical after any source, documentation included in
the ZIP, dependency, or build-script change. Rebuild and report a new manifest.

## Pending external acceptance

Neither item below has been claimed as passed:

1. Complete-workflow validation of the exact final ZIP/hash on a clean Windows
   10/11 x64 machine or VM with neither MATLAB nor .NET Runtime.
2. Physical visual inspection at Windows scaling 100%, 125%, and 150% on
   representative 16–24 inch displays.

## Stable purpose

The project is a MATLAB IPCE measurement and analysis tool. The primary
measurement path is:

`silicon calibration + silicon i-t → monochromatic power density`

`power density + sample i-t → sample IPCE`

It also has a standalone post-processing path:

`external wavelength/IPCE + solar spectrum → integrated and cumulative current density`

## 2026-07-26 confirmed decisions

- Automatically load the exact silicon trace
  `Si-i t [300 1100] nm-grating 2-filter.txt`.
- Automatically load its exact time-alignment file
  `Si-i t [300 1100] nm-grating 2-filter-time match.txt` into silicon anchors.
- Enable dark-current subtraction by default.
- Default dark ranges are silicon `0.1–10 s` and sample `50–60 s`.
- Detect i-t header units, convert to canonical `s/A`, and retain headers,
  source units, and conversion factors.
- If i-t units cannot be recognized, require an explicit user selection.
- Keep time-alignment units fixed at `nm/s`.
- Keep calculated and external IPCE simultaneously; select the integration
  source with a dropdown.
- External IPCE uses the first two numeric columns as `nm` and `%`.
- External post-processing must not require any measurement inputs.
- Export external IPCE, integration summary, and cumulative curve independently.

## Implemented structure

- `ipceReadIT.m` now performs unit-aware import and metadata preservation.
- `ipceReadExternalIPCE.m` imports, sorts, and averages duplicate wavelengths.
- `ipceDefaultConfig.m` owns requested startup defaults.
- `ipceResolveIPCESource.m` centralizes source selection and source-specific
  missing-data errors.
- `ipceBuildPostprocessExportItems.m` builds standalone exports.
- `IPCEApp.m` contains the external-IPCE import/source UI and independent export.
- `run_ipce_selftest.m` covers unit conversion, external import, defaults,
  independent integration, and standalone export.
- `README_CN.md` documents complete measurement and external post-processing.

## Numerical invariants

- The integration formula and interpolation remain unchanged:
  `pchip` for IPCE, linear for the spectrum, no extrapolation.
- The cumulative curve's final value equals the reported integrated current
  density.
- The application retains signed photocurrent columns but uses absolute
  photocurrent for power density and IPCE.
- Sample i-t is total current, not already area-normalized current density.

## Verification

Run:

```matlab
run_ipce_selftest
```

Then construct and close `IPCEApp` for a UI smoke test. MATLAB R2023b is
available on the current machine.

## Environment note

The folder was not a Git repository when these changes were made, so there is
no commit history for this work. The detailed approved design and execution
plan are under `docs/superpowers/specs/` and `docs/superpowers/plans/`.

## Historical: 2026-07-26 MATLAB portable deployment

This section records the earlier MATLAB-compiled packaging route. It is
retained for history but is superseded as the primary compact Windows
deliverable by the self-contained C# package documented above.

- The approved deliverable is `IPCEApp_R2023b_Windows_x64.zip`.
- The ZIP intentionally excludes MATLAB Runtime. End users install 64-bit
  MATLAB Runtime R2023b Update 6 or a later R2023b update themselves.
- `runIPCEApp.m` is the zero-output deployed entry point. Its
  `--smoke-test` mode creates, processes, validates, and closes the real UI.
- `ipceResolveStartupFile.m` prefers current-folder files and falls back to
  defaults embedded under `ctfroot`.
- `ipcePortablePackageConfig.m` owns the portable manifest.
- `build_ipce_portable.m` runs self-tests, compiles with `mcc -e`, embeds the
  four approved defaults, rejects Runtime payloads, creates the ZIP, and
  verifies it by extraction.
- Generated artifacts are under `dist/`. The build and compiled smoke test
  passed on the development computer.
- Final release acceptance still requires a clean Windows machine or VM with
  no MATLAB installation and only the matching R2023b Runtime.

## 2026-08-15 bilingual applications

- The C# WPF and MATLAB programmatic-UI applications now each ship as one
  bilingual build with live self-labelled `English` / `中文` switching.
- First launch follows `zh-*` versus other system locales. A saved choice in
  `%LOCALAPPDATA%\IPCEApp\settings.json` overrides the system locale; corrupt
  or inaccessible preferences do not block startup; writes are atomic.
- Language switching is presentation-only. It preserves imported files,
  spectrum headers and selections, scan profiles, anchors, dark-current
  intervals, calculated/external IPCE, integrations, export schemas, and all
  numeric values.
- C# release evidence: 231 tests passed (58 Core, 44 IO, 129 Desktop), ZIP
  85,510,823 bytes, SHA-256
  `6c2fb6c32c3637bb80c1c9f4ab305afa5ee6d1a82bb309db37a35b3ed116a004`.
- MATLAB release evidence: complete self-test and real UI smoke passed; ZIP
  790,342 bytes, 7 entries, SHA-256
  `a0ec3317f41a01b258f1af4a380d452b569655668744b875f74db8c866c461e5`;
  compiled smoke exit code 0; no Runtime installer is bundled.
- External clean-machine acceptance and Windows 100/125/150% physical-display
  review remain pending. The v1.0.0 tag, GitHub Release, and public repository
  upload are complete; future releases still require owner approval.
- Both applications now write the canonical JSON property `Language` and read
  the older lowercase `language` property for migration. MATLAB first-launch
  culture uses Windows/.NET `CurrentUICulture` with Java/LANG fallbacks.
