# GitHub Publication and Bilingual v1.0.0 Design

Date: 2026-08-15
Status: Approved
Repository: `https://github.com/ZiYingZhang/IPCE-measurement`

## Purpose

Prepare the existing IPCE measurement project as a polished private GitHub
repository that can later be made public. The repository will contain the
MATLAB reference application, the C# WPF application, tests, public experimental
data, documentation, and reproducible build scripts. The first public release
will be `v1.0.0` and will provide verified Windows packages for both
implementations.

Before `v1.0.0`, both applications will support complete Chinese and English
user interfaces from one codebase. English support must not fork calculation
logic, file formats, numerical behavior, or release packaging.

## Confirmed Decisions

- The GitHub repository is private during preparation and may become public
  only after the publication gates in this document pass.
- The original project code is licensed under the MIT License.
- The existing calibration, spectrum, silicon-detector, anchor, and MBVO
  experimental files may be published.
- MATLAB and C# WPF remain supported implementations.
- Source, tests, data, documentation, and build scripts are tracked with Git.
- Compiled ZIP files and extracted build trees are distributed through GitHub
  Releases and are not committed to Git history.
- The first formal release tag is `v1.0.0`.
- Both MATLAB and C# are bilingual before `v1.0.0`.
- The existing numerical behavior and MATLAB/C# parity invariants remain
  unchanged.

## Scope

### In scope

- Normalize the repository structure and remove spaces from the C# project
  path.
- Centralize shared default and example data.
- Update all resource discovery, tests, documentation, and build scripts for
  the new paths.
- Add Chinese/English localization to every user-visible surface in both
  applications.
- Add English-first GitHub documentation, Chinese documentation, MIT licensing,
  citation metadata, data provenance notes, and third-party notices.
- Add C# continuous integration and a documented MATLAB validation route.
- Rebuild and verify both Windows packages.
- Create a `v1.0.0` GitHub Release with packages, manifests, and checksums.

### Out of scope

- Changing physical formulas, interpolation, integration, scheduling, unit
  conversion, defaults, or export values.
- Combining the MATLAB and C# implementations into one runtime.
- Maintaining separate Chinese and English source branches or binaries.
- Bundling MATLAB Runtime into the MATLAB package.
- Committing generated exports, build directories, extracted applications, or
  release ZIP files.
- Claiming the clean-machine or Windows-scaling release gates before they are
  actually completed.

## Target Repository Structure

```text
IPCE-measurement/
|-- README.md
|-- README_CN.md
|-- LICENSE
|-- CITATION.cff
|-- .gitignore
|-- AGENTS.md
|-- matlab/
|   |-- *.m
|   `-- PORTABLE_README_CN.txt
|-- csharp/
|   |-- IPCE.slnx
|   |-- src/
|   |-- tests/
|   |-- tools/
|   |-- scripts/
|   `-- PORTABLE_README_CN.txt
|-- data/
|   |-- defaults/
|   `-- examples/
|-- docs/
`-- .github/
    `-- workflows/
```

The four startup files move to `data/defaults/`:

- `标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx`
- `标准太阳能光谱数据.xls`
- `Si-i t [300 1100] nm-grating 2-filter.txt`
- `Si-i t [300 1100] nm-grating 2-filter-time match.txt`

The MBVO files move to `data/examples/`. Generated `IPCE_export*.xlsx` files
are excluded. The legacy `dist APP`, C# `bin`, `obj`, `TestResults`, `dist`, and
`publish` trees remain local/generated and are excluded from Git.

Internal working state such as `.superpowers/` and `PROJECT_MEMORY.md` remains
local and is excluded from the public repository. `AGENTS.md` is rewritten to
describe the normalized public layout and retained because it is useful to
future development tools and contributors.

## Path Migration

Path migration is a behavior-preserving change and must be regression tested.

- MATLAB resolves repository data through a single configuration function.
  Development startup still allows an exact file in the application working
  directory to override the repository default, and compiled startup still
  falls back to files embedded under `ctfroot`.
- The MATLAB packaging manifest embeds the four files from `data/defaults/`
  while preserving their exact deployed file names.
- C# `IPCE.IO` embeds the same four shared files from `data/defaults/` with the
  existing logical resource names.
- C# tests continue to discover the solution root structurally rather than by
  hard-coding either `csharp` or the former `csharp APP` name.
- Build outputs remain under implementation-local ignored `dist/` directories,
  but only ZIP files and manifests selected for a formal release are uploaded
  to GitHub Releases.

## Localization Architecture

### Shared rules

- Supported cultures are English and Simplified Chinese.
- English is the fallback when a culture or resource key is missing.
- First launch follows the operating-system language; non-Chinese systems use
  English.
- Users can switch language in the application, and the preference persists
  locally for the next launch.
- Units, formulas, variable names, stable error identifiers, file formats, and
  numerical values are culture invariant.
- Default data keep their exact Unicode filenames. User-facing descriptions
  explain them in the selected language.
- Missing, duplicate, or empty localization keys fail automated validation.

### C# WPF

- Add centralized English and Simplified-Chinese resources to
  `IPCE.Desktop`.
- Add one localization service that owns the selected culture, persistence,
  fallback behavior, and change notifications.
- XAML labels, buttons, tabs, menus, tooltips, dialogs, file filters, status
  text, and plot text bind to localization keys.
- Domain and I/O layers retain stable error codes. User-facing messages are
  selected at the Desktop boundary so calculation and import behavior do not
  depend on UI culture.
- Switching language refreshes the current window without discarding session
  data or recalculating results.
- Plot typography and visual invariants remain unchanged except for translated
  text.

### MATLAB

- Add a centralized language catalog function with English and
  Simplified-Chinese entries.
- `IPCEApp` owns current-language state and applies catalog entries to controls,
  dialogs, status text, export choices, and plots.
- Computation and import functions continue to throw stable identifiers. The
  application maps identifiers to localized user-facing messages.
- Switching language updates the live application without clearing imported
  data, anchors, calculations, integration results, or plot viewport state.
- Language preference is stored with MATLAB preferences and falls back safely
  to the operating-system language if the stored value is invalid.

## Documentation and Licensing

- `README.md` is the English landing page and links to `README_CN.md`.
- Both READMEs explain the two implementations, measurement workflow,
  standalone external-IPCE post-processing, supported units, build commands,
  download choices, Runtime requirements, and validation status.
- `LICENSE` contains the MIT License for original project source.
- `CITATION.cff` provides project title, author, repository URL, version, and
  preferred citation metadata.
- A data note describes provenance, intended demonstration use, units, and the
  fact that the published files are real experimental/calibration inputs.
- Third-party software and data retain their own applicable terms. The project
  does not claim MATLAB Runtime or third-party .NET components as MIT-licensed.
- The C# package includes `THIRD_PARTY_NOTICES.txt`.

## Continuous Integration

### C#

A Windows GitHub Actions job runs on pushes and pull requests:

1. check out the repository;
2. install the SDK specified by `global.json`;
3. restore dependencies;
4. build `csharp/IPCE.slnx` in Release configuration;
5. run all solution tests without rebuilding; and
6. retain test reports only when useful for diagnosis.

The workflow receives read-only repository permissions unless a narrower step
requires more. Release publication is not performed by pull-request workflows.

### MATLAB

While the repository is private, MATLAB regression and UI smoke validation run
on the licensed development computer unless a MATLAB batch licensing token is
configured as a GitHub secret. After the repository becomes public, a MATLAB
Actions workflow may run the numerical self-test and UI-safe checks supported
by the hosted environment.

MATLAB Compiler packaging remains a licensed local release step. A public
repository does not remove the licensing requirement for MATLAB Compiler.

## Release Design

The `v1.0.0` GitHub Release contains:

- `IPCEApp_CSharp_Windows_x64_v1.0.0.zip`;
- `IPCEApp_MATLAB_R2023b_Windows_x64_v1.0.0.zip`;
- `SHA256SUMS.txt`;
- the C# build manifest;
- the MATLAB build/verification summary; and
- bilingual installation and release notes.

The C# package is self-contained for `win-x64` and does not require an installed
.NET Runtime. The MATLAB package excludes MATLAB Runtime and requires 64-bit
MATLAB Runtime R2023b Update 6 or a later R2023b update.

Each package is rebuilt from the exact tagged source. Archive sizes, hashes,
entry counts, test counts, and smoke results are read from fresh output and are
never copied from an older build record.

## Error Handling and Safety

- A path migration failure must leave original source and data recoverable.
- Build scripts reject output or cleanup paths outside their intended staging
  roots.
- Release scripts reject empty archives, missing executables, failed smoke
  tests, and unexpected MATLAB Runtime payloads.
- Localization lookup failures use English fallback for the running program and
  fail resource-integrity tests during development.
- Language changes never mutate calculation/export state.
- A failed import, calculation, integration, or export retains the last valid
  state according to existing transactional behavior.
- No GitHub push, visibility change, tag, or Release creation occurs without an
  explicit final review of the exact targets.

## Implementation Order

1. Record and verify the current MATLAB and C# baseline.
2. Normalize the repository structure and shared-data paths.
3. Restore the complete existing test suite after migration.
4. Add C# localization using tests first.
5. Add MATLAB localization using tests first.
6. Add public documentation, MIT licensing, citation metadata, data notes, and
   GitHub Actions.
7. Run full source and UI verification in both languages.
8. Rebuild both portable packages and generate fresh manifests and checksums.
9. Audit the Git index and complete history for excluded or sensitive content.
10. Push the reviewed history to the private repository.
11. Create and verify the `v1.0.0` Release.
12. Change visibility to public only after the owner approves the final audit.

## Verification

### C# minimum gate

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
```

The existing baseline is 197 tests: Core 58, IO 42, Desktop 97. Localization
adds tests, so the final count must be greater than or equal to 197 with zero
failures and skips unless a deliberate test replacement is documented.

### MATLAB minimum gate

```matlab
run_ipce_selftest
app = IPCEApp;
drawnow;
assert(isvalid(app));
close(app);
```

The UI smoke is performed for both languages. Focused regression tests cover
resource parity, live language switching, state preservation, and language-
independent numerical/export values.

### Packaging gate

- Run the MATLAB self-test and UI smoke before packaging.
- Run the complete C# portable build script.
- Build the MATLAB portable package with MATLAB Compiler.
- Extract and smoke-test both exact archives.
- Generate `SHA256SUMS.txt` from the final bytes.

## Public-Release Gates

Before the private repository becomes public:

1. tracked files contain no build trees, exports, temporary files, secrets, or
   unintended local state;
2. complete Git history passes the same audit;
3. MIT, third-party notices, data provenance, and citation metadata are present;
4. C# and MATLAB bilingual validation passes;
5. both packages are freshly built and their checksums match uploaded assets;
6. README documentation distinguishes the Runtime requirements accurately;
7. the owner reviews the exact repository, tag, and Release assets; and
8. any incomplete external acceptance gate is visibly documented.

Two external gates remain pending until explicitly performed and recorded:

- complete workflow validation of the exact C# ZIP/hash on a clean Windows
  10/11 x64 machine or VM with neither MATLAB nor .NET Runtime; and
- representative plot inspection at Windows scaling 100%, 125%, and 150%.

The MATLAB archive has its separate clean-machine gate: a Windows machine with
no MATLAB installation and only the matching R2023b Runtime.

## Acceptance Criteria

The design is implemented when:

- the normalized repository can be cloned into a clean directory;
- both implementations find the shared defaults and pass their full tests;
- every user-visible application surface is available in English and Chinese;
- switching language preserves active measurement and post-processing state;
- numerical and export parity is unchanged;
- ignored/generated content is absent from Git history;
- documentation and licensing are complete;
- both final ZIP files reproduce from the tagged source and match published
  checksums; and
- public visibility is enabled only after explicit owner approval.
