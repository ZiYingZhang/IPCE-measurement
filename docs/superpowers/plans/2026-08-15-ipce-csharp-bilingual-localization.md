# IPCE C# Bilingual Localization Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver one C# WPF build whose complete user-facing interface can switch live between English and Simplified Chinese, remembers the choice, and preserves all scientific calculations, state, units, and exported numeric values.

**Architecture:** Add a Desktop-only localization boundary backed by neutral-English `.resx` resources and a `zh-CN` satellite resource. A single observable `LocalizationService` owns culture selection, English fallback, safe JSON preference persistence, formatting, and language-change notifications. XAML binds through the service indexer; view models, dialogs, plotting builders, and error presentation receive or use the same service so an in-place language change refreshes every display layer without recreating the `MainViewModel` or changing Core/IO numerical behavior.

**Tech Stack:** .NET 10, C# 14, WPF, `System.Resources.ResourceManager`, `.resx`, `System.Text.Json`, MSTest, ScottPlot.WPF.

**Authoritative contracts:** `AGENTS.md`, `docs/superpowers/specs/2026-08-15-github-publication-bilingual-v1-design.md`, `README_CN.md`, `PROJECT_MEMORY.md`, and `docs/superpowers/progress/ipce-csharp-migration-progress.md`.

---

## Task 1: Record the scientific localization contract

**Files:**
- Create: `docs/scientific/research-app-spec.md`
- Create: `docs/scientific/numerical-contract.md`
- Create: `docs/scientific/bilingual-acceptance-checklist.md`
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`

**Step 1: Write the research application specification**

Document the four supported workflows, their inputs/outputs, standalone external-IPCE post-processing, startup defaults, Windows/offline deployment assumptions, bilingual personas, and the invariant that a language switch never clears imported paths, traces, anchors, selections, calculated results, integration results, or plot viewport settings.

**Step 2: Write the numerical contract**

Copy the canonical units and non-negotiable numerical behavior from `AGENTS.md`, identify MATLAB as the numerical oracle, record the existing Core/IO/Desktop parity gates, and state explicitly that localization may change only presentation strings and culture-specific UI formatting—not stored doubles, interpolation, wavelength coverage, error codes, tabular export schemas, filenames chosen by the user, or calculation/export results.

**Step 3: Write the bilingual acceptance checklist**

Include executable gates for resource parity, English fallback, first-launch system-language selection, preference persistence, corrupt-preference recovery, live switching with object-identity/state preservation, dialogs, plot labels, hover text, errors, startup status, and release smoke. Keep the two external clean-machine/scaling gates marked pending.

**Step 4: Update progress tracking**

Add a dated C# bilingualization section linking the three contracts and this plan; mark implementation and verification pending.

**Step 5: Commit the contract batch**

```powershell
git add docs/scientific docs/superpowers/progress/ipce-csharp-migration-progress.md docs/superpowers/plans/2026-08-15-ipce-csharp-bilingual-localization.md
git commit -m "docs: define C# bilingual localization contract"
```

## Task 2: Build the resource and preference foundation with TDD

**Files:**
- Create: `csharp/src/IPCE.Desktop/Localization/AppLanguage.cs`
- Create: `csharp/src/IPCE.Desktop/Localization/ILocalizationService.cs`
- Create: `csharp/src/IPCE.Desktop/Localization/LocalizationService.cs`
- Create: `csharp/src/IPCE.Desktop/Localization/LanguagePreferenceStore.cs`
- Create: `csharp/src/IPCE.Desktop/Localization/LocalizedMessageFormatter.cs`
- Create: `csharp/src/IPCE.Desktop/Resources/Strings.resx`
- Create: `csharp/src/IPCE.Desktop/Resources/Strings.zh-CN.resx`
- Create: `csharp/tests/IPCE.Desktop.Tests/LocalizationServiceTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/LanguagePreferenceStoreTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/LocalizedMessageFormatterTests.cs`

**Step 1: Write failing language-selection and resource-parity tests**

Tests must independently assert:

- system culture `zh-CN`, `zh-TW`, or another `zh-*` selects `zh-CN` only when no valid preference exists;
- every non-Chinese system culture selects `en-US`;
- valid persisted `en-US`/`zh-CN` overrides system culture;
- unsupported persisted values and corrupt JSON recover to system selection without throwing;
- changing `CurrentLanguage` raises indexer and language notifications;
- a missing Chinese value returns the neutral English value;
- both catalogs expose the same non-empty key set and neither catalog contains blank values.

Run and confirm RED because the localization types/resources do not exist:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~LocalizationServiceTests|FullyQualifiedName~LanguagePreferenceStoreTests"
```

**Step 2: Implement language value and safe preference storage**

Use `AppLanguage` values `English` and `SimplifiedChinese`, culture names `en-US` and `zh-CN`, and a `LanguagePreferenceStore` that reads/writes `{ "language": "..." }` at `%LOCALAPPDATA%\IPCEApp\settings.json` in production. Tests inject a temporary absolute file path. Write atomically through a same-directory temporary file and replacement/move; ignore malformed, inaccessible, or unsupported preference content and preserve application startup.

**Step 3: Implement the observable localization service**

Expose:

```csharp
public interface ILocalizationService : INotifyPropertyChanged
{
    AppLanguage CurrentLanguage { get; set; }
    string CurrentCultureName { get; }
    string this[string key] { get; }
    string Format(string key, params object?[] arguments);
}
```

`LocalizationService.Current` is the application singleton. The constructor accepts the preference store and system `CultureInfo` for deterministic tests. On switch, set `CurrentUICulture` for the current/default threads, persist the culture name, raise `PropertyChanged` for `CurrentLanguage`, `CurrentCultureName`, and `Item[]`, then raise `LanguageChanged`. Resource lookup uses `ResourceManager`; absent `zh-CN` entries fall back to neutral English; absent neutral keys return `[key]` so omissions remain visible.

**Step 4: Add the complete neutral-English and `zh-CN` catalogs**

Create stable semantic keys grouped by prefix:

- `App.*`, `Language.*`, `Common.*`;
- `Workflow.*`, `Silicon.*`, `Sample.*`, `Spectrum.*`, `Summary.*`;
- `Dialog.*`, `FileFilter.*`, `Import.*`, `Export.*`;
- `Status.*`, `Prerequisite.*`, `Freshness.*`, `Validation.*`;
- `Plot.*`, `PlotToolbar.*`, `TraceOverlay.*`;
- `Error.*` including a key for every stable `IPCE:*` code shown at the Desktop boundary.

Keep scientific symbols identical in both catalogs: `nm`, `s`, `A`, `W m⁻² nm⁻¹`, `µW cm⁻²`, `mA cm⁻²`, and `%`.

**Step 5: Write failing formatter tests, then implement formatting**

The formatter must use the active UI culture for displayed numbers and localized templates while retaining invariant scientific export serialization elsewhere. Cover count, file/sheet summary, coverage ranges, current-density summary, and stale-result reason composition. Confirm RED before implementation, then GREEN:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~LocalizationServiceTests|FullyQualifiedName~LanguagePreferenceStoreTests|FullyQualifiedName~LocalizedMessageFormatterTests"
```

**Step 6: Commit the localization foundation**

```powershell
git add csharp/src/IPCE.Desktop/Localization csharp/src/IPCE.Desktop/Resources csharp/tests/IPCE.Desktop.Tests/LocalizationServiceTests.cs csharp/tests/IPCE.Desktop.Tests/LanguagePreferenceStoreTests.cs csharp/tests/IPCE.Desktop.Tests/LocalizedMessageFormatterTests.cs
git commit -m "feat(csharp): add bilingual localization foundation"
```

## Task 3: Localize the main window and prove live state preservation

**Files:**
- Modify: `csharp/src/IPCE.Desktop/App.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml`
- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/LanguageSwitchingTests.cs`

**Step 1: Write failing WPF tests**

On an STA thread, construct the real `MainWindow`, capture its `DataContext` and representative session objects/values, switch the shared service from English to Chinese, and assert:

- title and startup/status text switch language;
- the same `MainViewModel`, `SessionState`, imported data references, and calculated-result references remain attached;
- switching back restores English;
- the language selector offers exactly self-labelled `English` and `中文` choices and selects the persisted value.

Run and verify RED because the selector and live bindings are absent:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~MainWindowSmokeTests|FullyQualifiedName~LanguageSwitchingTests"
```

**Step 2: Bind the main window to localization resources**

Add a compact top-right language selector above the existing workflow/result layout. Bind `Title`, visible label, startup text, and selector state to `LocalizationService.Current`. Use self-labelled entries so users can recover from an accidental language selection. Do not replace the window `DataContext` or recreate `MainViewModel` on a switch.

**Step 3: Refresh derived view-model text on language changes**

Subscribe `MainViewModel` and child view models through a single owned subscription. Raise property changes for derived user-visible strings when language changes. Ensure unload/close removes subscriptions where needed and tests do not retain windows through the static service.

**Step 4: Localize startup and unhandled-error presentation**

Replace `App.xaml.cs` Chinese literals with resource lookups, retaining diagnostic log paths and exit/smoke behavior. A logging failure gets a localized display string but never prevents error reporting.

**Step 5: Verify and commit**

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~MainWindowSmokeTests|FullyQualifiedName~LanguageSwitchingTests"
git add csharp/src/IPCE.Desktop/App.xaml.cs csharp/src/IPCE.Desktop/MainWindow.xaml csharp/src/IPCE.Desktop/MainWindow.xaml.cs csharp/src/IPCE.Desktop/ViewModels csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs csharp/tests/IPCE.Desktop.Tests/LanguageSwitchingTests.cs
git commit -m "feat(csharp): add live language switching"
```

## Task 4: Localize workflows, dialogs, validation, and error presentation

**Files:**
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Services/FileDialogService.cs`
- Modify: `csharp/src/IPCE.Desktop/Services/ImportSelectionService.cs`
- Modify: `csharp/src/IPCE.Desktop/Services/UserOperationRunner.cs`
- Modify: `csharp/src/IPCE.Desktop/Services/UserNotificationService.cs`
- Modify: `csharp/src/IPCE.Desktop/Input/FiniteDoubleConverter.cs`
- Modify: `csharp/src/IPCE.Desktop/Import/SpectrumImportCoordinator.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/WorkflowCalculation.cs`
- Modify: `csharp/src/IPCE.Desktop/State/SessionState.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/UserOperationRunnerTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/FiniteDoubleConverterTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/BilingualWorkflowSurfaceTests.cs`

**Step 1: Write failing workflow-surface tests**

For both languages, exercise real view models and real user-operation handling. Assert localized operation titles, prerequisites, import summaries, freshness messages/reasons, integration/export status, finite-number validation, spectrum column validation, and expected `IpceException` presentation. For an `IpceException`, map by stable `Code`; include formatted safe context when supplied and fall back to the localized generic error plus the stable code when unmapped.

Run and verify RED:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~BilingualWorkflowSurfaceTests|FullyQualifiedName~UserOperationRunnerTests|FullyQualifiedName~WorkflowViewModelTests|FullyQualifiedName~FiniteDoubleConverterTests|FullyQualifiedName~ImportCoordinatorTests"
```

**Step 2: Replace workflow XAML literals with live bindings**

Localize all headers, labels, buttons, check boxes, combo-box entries, prerequisites, status headings, and export options in `WorkflowControls.xaml`. Preserve every binding, enum `Tag`, command, validation rule, default value, and layout behavior.

**Step 3: Localize dialogs and file filters**

Build Open/Save filters from localized display labels while retaining exact extension patterns and default extensions. Localize i-t unit-selection and spectrum-sheet/column dialogs, including confirm/cancel and validation messages. Reopening a dialog after a language switch must use the current language.

**Step 4: Localize derived workflow messages without changing state semantics**

Use stable reason identifiers or localized factories instead of storing irrevocably rendered Chinese sentences. Preserve `ResultFreshness` transitions and invalidation triggers exactly. On a live switch, current/prerequisite/import/result/export messages must rerender from unchanged state.

**Step 5: Localize errors at the Desktop boundary**

`UserOperationRunner` uses `IpceException.Code` and resource keys for expected domain/import/export errors. Raw exception messages remain diagnostic log material, not primary UI text. `IOException` and `UnauthorizedAccessException` use localized actionable generic messages; unexpected exceptions retain diagnostic logging and localized log-path notification. Do not modify Core/IO exception codes or numerical paths.

**Step 6: Verify and commit**

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~BilingualWorkflowSurfaceTests|FullyQualifiedName~UserOperationRunnerTests|FullyQualifiedName~WorkflowViewModelTests|FullyQualifiedName~FiniteDoubleConverterTests|FullyQualifiedName~ImportCoordinatorTests|FullyQualifiedName~ResultFreshnessTests|FullyQualifiedName~SessionStateTests"
git add csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs csharp/src/IPCE.Desktop/Services csharp/src/IPCE.Desktop/Input csharp/src/IPCE.Desktop/Import csharp/src/IPCE.Desktop/ViewModels csharp/src/IPCE.Desktop/State csharp/tests/IPCE.Desktop.Tests
git commit -m "feat(csharp): localize workflows and user messages"
```

## Task 5: Localize results, plots, hover text, and plot controls

**Files:**
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/ResultPlotModelBuilder.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/TraceOverlayBuilder.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/WorkflowPreviewBuilder.cs`
- Modify: `csharp/src/IPCE.Desktop/Plotting/PlotInteractionController.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotToolbar.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/TracePlotView.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/TracePlotView.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/IpcePlotView.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PowerDensityPlotView.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/SchedulePlotView.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/SpectrumIntegrationPlotView.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/Plots/PlotViewSaveHelper.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/ResultPlotModelBuilderTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/TraceOverlayTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/WorkflowPreviewTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/PlotRenderingTests.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/BilingualPlotSurfaceTests.cs`

**Step 1: Write failing plot-surface tests**

Build the same trace, schedule, power-density, IPCE, spectrum-integration, and cumulative models under both languages. Assert that titles, axis labels, series labels, empty-state text, integration bands, stale overlays, source badges, coverage text, and hover details switch language while every X/Y/error/band boundary value and series count remains exactly equal. Include a test that a live language switch rerenders the already displayed `ResultTabs` without replacing its `MainViewModel`.

Run and verify RED:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~BilingualPlotSurfaceTests|FullyQualifiedName~ResultPlotModelBuilderTests|FullyQualifiedName~TraceOverlayTests|FullyQualifiedName~WorkflowPreviewTests|FullyQualifiedName~PlotRenderingTests"
```

**Step 2: Localize result tabs and summaries**

Replace tab, expander, group, button, grid-column, and summary labels with live resource bindings. Replace localized `StringFormat` literals with converter/view-model formatted properties so the active language can change at runtime without reloading results.

**Step 3: Localize plot model builders**

Pass `ILocalizationService` into builders or use a shared injected text provider at the view boundary. Keep plot typography `26/24/20/20`, hover/toolbar size `14`, dark bands `#607D8B`/`0.28`, integration bands `#90CAF9`/`0.24`, and boundary widths `3`. Keep layer visibility logic independent of translated labels by replacing label-string comparisons with stable series/layer identifiers.

**Step 4: Localize hover, coverage, viewport, toolbar, and save UI**

All displayed values use the active UI culture and the unchanged canonical units. File-save filters retain `.png`. Validation remains the same. Viewport, auto-range, log-axis, save, and source-selection behavior must not depend on translated text.

**Step 5: Verify and commit**

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~BilingualPlotSurfaceTests|FullyQualifiedName~ResultPlotModelBuilderTests|FullyQualifiedName~TraceOverlayTests|FullyQualifiedName~WorkflowPreviewTests|FullyQualifiedName~PlotRenderingTests|FullyQualifiedName~PlotControllerTests|FullyQualifiedName~PlotViewportTests"
git add csharp/src/IPCE.Desktop/Views/ResultTabs.xaml csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs csharp/src/IPCE.Desktop/Plotting csharp/src/IPCE.Desktop/Views/Plots csharp/tests/IPCE.Desktop.Tests
git commit -m "feat(csharp): localize results and scientific plots"
```

## Task 6: Audit completeness and prove scientific parity

**Files:**
- Create: `csharp/tests/IPCE.Desktop.Tests/LocalizationCompletenessTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/ReproducibleExportTests.cs`
- Modify: `docs/scientific/bilingual-acceptance-checklist.md`
- Modify: `docs/superpowers/progress/ipce-csharp-migration-progress.md`
- Modify: `PROJECT_MEMORY.md` (local project memory; do not force-add if ignored)

**Step 1: Add failing completeness and parity tests**

Tests must catch these concrete regressions:

- a key exists in only one catalog or has an empty value;
- a user-facing XAML `Text`, `Content`, `Header`, or `Title` literal contains Chinese or non-scientific English prose instead of a localization binding;
- Desktop production C# retains a directly displayed Chinese literal outside resource files (allow internal test data, stable scientific symbols, and diagnostic-only strings via a documented allowlist);
- switching language changes any end-to-end calculation point, integrated summary number, export table value, export column schema, or byte-for-byte invariant CSV generated under invariant export rules.

Run and verify RED on remaining literals before cleanup:

```powershell
dotnet test "csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj" -c Release --no-restore --filter "FullyQualifiedName~LocalizationCompletenessTests|FullyQualifiedName~EndToEndWorkflowTests|FullyQualifiedName~ReproducibleExportTests"
```

**Step 2: Remove all remaining user-facing literals**

Use the completeness report to route every residual string to a resource key or document it as internal diagnostic/scientific invariant. Do not localize property names used as programmatic export schema without an explicit compatibility decision.

**Step 3: Run the complete C# verification**

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
```

Expected baseline plus new bilingual tests: at least 199 total tests, zero failures, zero skips. Record the fresh per-project and total counts; do not hard-code an assumed final count before the run.

**Step 4: Run MATLAB oracle regression**

No MATLAB production file should change in this C# phase, but run the oracle and UI smoke to demonstrate repository-wide scientific stability:

```powershell
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

**Step 5: Build and inspect a fresh portable C# package**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "csharp/scripts/build-portable.ps1"
```

Read `csharp/dist/IPCEApp_Windows_x64.build.json`, confirm both neutral English and `zh-CN` resources are present in the publish/ZIP, and record the fresh archive size, SHA-256, test count, smoke exit codes, and resource evidence. Never reuse prior release evidence.

**Step 6: Update acceptance and progress records**

Mark automated gates complete only where evidenced. Leave the clean Windows 10/11 x64 full-workflow test and 100%/125%/150% scaling inspection pending. Update `PROJECT_MEMORY.md` with localization decisions and verification evidence while respecting its current ignore status.

**Step 7: Commit the verified C# bilingual phase**

```powershell
git add csharp/src csharp/tests docs/scientific/bilingual-acceptance-checklist.md docs/superpowers/progress/ipce-csharp-migration-progress.md csharp/dist/IPCEApp_Windows_x64.build.json
git commit -m "test(csharp): verify bilingual release parity"
git status --short
git log -6 --oneline
```

Stop before any remote push, tag, GitHub Release, or repository visibility change. Those remain separate owner-approved actions. After this C# phase, create and execute a separate MATLAB bilingualization plan against `matlab/IPCEApp.m` and its narrow helper functions.
