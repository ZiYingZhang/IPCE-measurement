# IPCE MATLAB Bilingual Localization Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add complete live English/Simplified-Chinese switching to the MATLAB programmatic UI while preserving all measurement state, numerical behavior, file interoperability, and MATLAB Compiler packaging.

**Architecture:** Keep numerical/import/export helpers unchanged. Add a centralized localization catalog and preference service under `matlab/`; `IPCEApp.m` owns the selected language, exposes self-labelled `English` / `中文` controls, recursively reapplies localized properties to existing UI objects, and routes runtime dialogs/status templates through the catalog. Language switching updates existing handles rather than rebuilding the application or its nested `state` structure.

**Tech Stack:** MATLAB R2023b programmatic UI, tables/string arrays, JSON preference storage, MATLAB Compiler, existing `run_ipce_selftest` and deployed `--smoke-test` route.

---

## Task 1: Add catalog, preference, and integrity tests

**Files:**
- Create: `matlab/ipceLanguageCatalog.m`
- Create: `matlab/ipceLanguagePreference.m`
- Modify: `matlab/run_ipce_selftest.m`

1. Add failing self-tests for identical non-empty English/Chinese keys, English fallback, `zh-*` versus other first-launch selection, valid saved override, corrupt preference recovery, and atomic round-trip persistence using a temporary path.
2. Implement `ipceLanguageCatalog(language)` with neutral English and `zh-CN` catalogs and a visible missing-key fallback.
3. Implement `ipceLanguagePreference(action, ...)` for safe load/save/resolve behavior at `%LOCALAPPDATA%/IPCEApp/settings.json`, with injectable paths/system locale for tests.
4. Run `matlab -batch "cd('matlab'); run_ipce_selftest"` and commit.

## Task 2: Add live language switching without state recreation

**Files:**
- Modify: `matlab/IPCEApp.m`
- Modify: `matlab/run_ipce_selftest.m`

1. Add a failing real-UI test that captures the figure, state-bearing handles/values, switches English → Chinese → English through a test hook, and proves the same figure and imported/result state remain attached.
2. Add a top-level self-labelled language dropdown.
3. Store current language and catalog in the existing closure; expose read-only test hooks through `appFigure.UserData` for language selection and state snapshots.
4. Implement `applyLanguage` to update existing `Name`, `Title`, `Text`, `Tooltip`, `Items`, `ColumnName`, axes titles/labels, status text, and language control without reconstructing `state`.
5. Verify the focused UI test and commit.

## Task 3: Localize runtime dialogs, status, plots, and export UI

**Files:**
- Modify: `matlab/IPCEApp.m`
- Modify: `matlab/ipceLanguageCatalog.m`
- Modify: `matlab/run_ipce_selftest.m`

1. Add failing bilingual tests for representative import filters/titles, unit prompts, recoverable alerts, source/status messages, plot labels, anchor/dark/axis dialogs, export dialog, and formatted numerical statuses.
2. Route runtime text through `text(key)`, `formatText(key, ...)`, and localized alert/file-dialog helpers.
3. Keep stable error identifiers, units, file extensions, table schemas, and numeric formatting semantics unchanged.
4. Audit all English-mode visible UI strings through real component handles; allow only `中文` and canonical scientific symbols to contain non-English/invariant text.
5. Run the complete self-test and real UI smoke, then commit.

## Task 4: Verify MATLAB/C# numerical and packaging parity

**Files:**
- Modify: `matlab/ipcePortablePackageConfig.m` only if a new localization file must be included explicitly
- Modify: `docs/scientific/bilingual-acceptance-checklist.md`
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Modify: `AGENTS.md`

1. Run MATLAB self-test plus real UI smoke.
2. Run the full C# Release build and all 230 tests to prove no cross-implementation regression.
3. Run `matlab -batch "cd('matlab'); build_ipce_portable"`; inspect the new ZIP, verify localization sources are compiled/included, and record fresh bytes/hash/entry count.
4. Mark MATLAB automated bilingual gates complete, retain clean-machine/scaling gates as pending, update local `PROJECT_MEMORY.md`, commit, and stop before any push/tag/Release/visibility change.
