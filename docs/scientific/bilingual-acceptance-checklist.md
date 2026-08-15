# C# Bilingual Acceptance Checklist

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

## External gates

- [ ] Pending: complete exact-ZIP workflow on clean Windows 10/11 x64 with
  neither MATLAB nor .NET Runtime.
- [ ] Pending: inspect representative plots and both languages at Windows
  scaling 100%, 125%, and 150% on representative 16–24 inch displays.
