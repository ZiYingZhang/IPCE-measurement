# IPCE Bilingual Acceptance Checklist

Last updated: 2026-08-15

## Resource and preference gates

- [x] Neutral English and `zh-CN` catalogs have identical, non-empty key sets.
- [x] Missing Chinese entries fall back to neutral English.
- [x] Missing neutral keys remain visibly diagnosable.
- [x] First launch under `zh-*` selects Chinese; other cultures select English.
- [x] A valid saved language overrides system culture.
- [x] Unsupported/corrupt/inaccessible preference data does not block startup.
- [x] Language preference is written atomically and restored on next launch.

## Live UI gates

- [x] The real WPF window switches English ↔ Chinese without recreation.
- [x] `MainViewModel`, `SessionState`, input/result objects, values, and selected
  options retain identity and content across a switch.
- [x] Workflow controls, statuses, prerequisites, dialogs, file filters,
  validation, errors, notifications, result tabs, summaries, plots, hover text,
  toolbars, and save UI all use the current language.
- [x] `English` and `中文` remain self-labelled in either language.
- [x] No user-facing Chinese or non-scientific English prose remains hard-coded
  in production XAML/C# outside the resource catalogs.

## Scientific and interoperability gates

- [x] Language does not change Core/IO inputs, calculations, result values,
  result freshness, integration coverage, or stable error codes.
- [x] Plot models have identical series counts, X/Y/error data, band bounds,
  colors, opacities, and boundary widths across languages.
- [x] Existing plot typography and interaction invariants remain intact.
- [x] Export table names, columns, order, and invariant numeric values are
  identical across languages.
- [x] External IPCE post-processing remains standalone.
- [x] MATLAB self-test and real UI smoke remain green.

## Automated release gates

- [x] Release build: zero errors and warnings.
- [x] All .NET tests: zero failures and skips; fresh counts recorded.
- [x] Portable publish contains neutral and `zh-CN` resources.
- [x] Published and extracted `IPCEApp.exe --smoke-test` both exit 0.
- [x] Fresh archive bytes, SHA-256, entries, test counts, and smoke results are
  copied from `csharp/dist/IPCEApp_Windows_x64.build.json`.

## MATLAB bilingual gates

- [x] The real programmatic UI switches live between `English` and `中文`
  without recreating the figure or measurement state.
- [x] Static controls, dynamic statuses, file dialogs, unit prompts, alerts,
  anchor/dark-current workflows, export UI, axes, legends, and calculated plot
  labels use the selected language.
- [x] Imported filenames and spectrum worksheet/header values remain source
  data and are not translated or reset during a switch.
- [x] Scan profiles, file selections, spectrum-column items/data/values,
  imported tables, result tables, and integration source survive
  English ↔ Chinese switching unchanged.
- [x] First launch follows `zh-*` versus other system locales; valid saved
  preference wins; corrupt/inaccessible settings recover safely; writes are
  atomic.
- [x] MATLAB and C# share canonical JSON property `Language`, accept legacy
  lowercase `language`, and reject non-scalar preference values.
- [x] Recoverable MATLAB errors use stable identifiers; unknown literals and
  error identifiers are visibly diagnostic rather than silently truncated.
- [x] Complete MATLAB numerical self-test and real-UI smoke pass.
- [x] C# Release build remains at 0 warnings/errors and all 231 tests pass
  (Core 58, IO 44, Desktop 129).
- [x] MATLAB Compiler build, archive extraction verification, Runtime-payload
  rejection, and compiled `--smoke-test` pass.
- [x] UI regression tests use an injected temporary preference path and real
  temporary external-IPCE/spectrum files through production import/integration
  paths; no test hook writes fabricated scientific results directly.
- [x] Fresh MATLAB ZIP: 790,342 bytes, 7 entries, Runtime markers 0,
  SHA-256 `a0ec3317f41a01b258f1af4a380d452b569655668744b875f74db8c866c461e5`.

## External gates

- [ ] Pending: complete exact-ZIP workflow on clean Windows 10/11 x64 with
  neither MATLAB nor .NET Runtime.
- [ ] Pending: complete the exact MATLAB ZIP/hash workflow on clean Windows
  10/11 x64 with no MATLAB installation and matching 64-bit MATLAB Runtime
  R2023b Update 6 or later R2023b update.
- [ ] Pending: inspect representative plots and both languages at Windows
  scaling 100%, 125%, and 150% on representative 16–24 inch displays.
