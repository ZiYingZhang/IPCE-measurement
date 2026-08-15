# C# Bilingual Acceptance Checklist

Last updated: 2026-08-15

## Resource and preference gates

- [ ] Neutral English and `zh-CN` catalogs have identical, non-empty key sets.
- [ ] Missing Chinese entries fall back to neutral English.
- [ ] Missing neutral keys remain visibly diagnosable.
- [ ] First launch under `zh-*` selects Chinese; other cultures select English.
- [ ] A valid saved language overrides system culture.
- [ ] Unsupported/corrupt/inaccessible preference data does not block startup.
- [ ] Language preference is written atomically and restored on next launch.

## Live UI gates

- [ ] The real WPF window switches English ↔ Chinese without recreation.
- [ ] `MainViewModel`, `SessionState`, input/result objects, values, and selected
  options retain identity and content across a switch.
- [ ] Workflow controls, statuses, prerequisites, dialogs, file filters,
  validation, errors, notifications, result tabs, summaries, plots, hover text,
  toolbars, and save UI all use the current language.
- [ ] `English` and `中文` remain self-labelled in either language.
- [ ] No user-facing Chinese or non-scientific English prose remains hard-coded
  in production XAML/C# outside the resource catalogs.

## Scientific and interoperability gates

- [ ] Language does not change Core/IO inputs, calculations, result values,
  result freshness, integration coverage, or stable error codes.
- [ ] Plot models have identical series counts, X/Y/error data, band bounds,
  colors, opacities, and boundary widths across languages.
- [ ] Existing plot typography and interaction invariants remain intact.
- [ ] Export table names, columns, order, and invariant numeric values are
  identical across languages.
- [ ] External IPCE post-processing remains standalone.
- [ ] MATLAB self-test and real UI smoke remain green.

## Automated release gates

- [ ] Release build: zero errors and warnings.
- [ ] All .NET tests: zero failures and skips; fresh counts recorded.
- [ ] Portable publish contains neutral and `zh-CN` resources.
- [ ] Published and extracted `IPCEApp.exe --smoke-test` both exit 0.
- [ ] Fresh archive bytes, SHA-256, entries, test counts, and smoke results are
  copied from `csharp/dist/IPCEApp_Windows_x64.build.json`.

## External gates

- [ ] Pending: complete exact-ZIP workflow on clean Windows 10/11 x64 with
  neither MATLAB nor .NET Runtime.
- [ ] Pending: inspect representative plots and both languages at Windows
  scaling 100%, 125%, and 150% on representative 16–24 inch displays.
