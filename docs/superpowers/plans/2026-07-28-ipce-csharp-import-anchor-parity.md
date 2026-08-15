# IPCE C# Import and Anchor Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore MATLAB-equivalent unit selection, spreadsheet selection, four-format external-IPCE import, and transactional anchor editing.

**Architecture:** Keep parsing in `IPCE.IO`, but expose read-only discovery APIs for UI decisions. Desktop coordinators obtain user choices through injected dialog interfaces, then call the readers with explicit selections; anchor rows edit a temporary copy and replace session state only after full validation.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, MSTest 4, NPOI 2.7.5, existing import readers and `SessionState`.

## Global Constraints

- Execute only after `2026-07-28-ipce-csharp-reliability-input.md` passes.
- Preserve internal i-t units `s/A`, anchor units `nm/s`, external IPCE `nm/%`, and spectrum irradiance `W m^-2 nm^-1`.
- Never infer missing i-t units from numeric magnitude.
- Preserve prior valid state when a dialog is cancelled or any import/edit fails.
- External-IPCE post-processing must work without measurement data.
- External IPCE must not be clipped to `0–100%`.
- The repository is not a Git repository. Do not initialize Git or add commit steps.
- Every behavior change requires a failing test first.

---

## File Structure

**Create**

- `csharp/src/IPCE.IO/Import/TraceImportInspection.cs`
- `csharp/src/IPCE.Desktop/Import/TraceImportCoordinator.cs`
- `csharp/src/IPCE.Desktop/Import/SpectrumImportCoordinator.cs`
- `csharp/src/IPCE.Desktop/Services/ImportSelectionService.cs`
- `csharp/src/IPCE.Desktop/ViewModels/AnchorRowViewModel.cs`
- `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/AnchorTableEditingTests.cs`

**Modify**

- `csharp/src/IPCE.IO/Import/ItTraceReader.cs`
- `csharp/src/IPCE.IO/Import/SpectrumReader.cs`
- `csharp/src/IPCE.IO/Import/ExternalIpceReader.cs`
- `csharp/src/IPCE.IO/Tables/NpoiWorkbookReader.cs`
- `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- `csharp/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- `csharp/tests/IPCE.IO.Tests/ExternalIpceReaderTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/AnchorEditingTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

---

### Task 1: Inspect i-t Headers and Retry with Explicit Units

**Files:**

- Create: `csharp/src/IPCE.IO/Import/TraceImportInspection.cs`
- Create: `csharp/src/IPCE.Desktop/Import/TraceImportCoordinator.cs`
- Create: `csharp/src/IPCE.Desktop/Services/ImportSelectionService.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`
- Modify: `csharp/src/IPCE.IO/Import/ItTraceReader.cs`
- Modify: `csharp/tests/IPCE.IO.Tests/ItTraceReaderTests.cs`

**Interfaces:**

```csharp
public sealed record TraceImportInspection(
    string TimeHeader,
    string CurrentHeader,
    string DetectedTimeUnit,
    string DetectedCurrentUnit);

public static TraceImportInspection ItTraceReader.Inspect(string path);

public interface IImportSelectionService
{
    UnitOverrides? SelectTraceUnits(TraceImportInspection inspection);
}

public sealed class TraceImportCoordinator
{
    public TraceImportCoordinator(IImportSelectionService selections);
    public Task<TraceData?> ReadAsync(string path);
}
```

`null` means the user cancelled and no state must change.

- [ ] **Step 1: Write IO inspection RED tests**

Create one trace with `Time/sec, Current/uA` and one without units. Assert:

```csharp
TraceImportInspection detected = ItTraceReader.Inspect(withUnits);
Assert.AreEqual("sec", detected.DetectedTimeUnit);
Assert.AreEqual("uA", detected.DetectedCurrentUnit);

TraceImportInspection missing = ItTraceReader.Inspect(withoutUnits);
Assert.AreEqual("", missing.DetectedTimeUnit);
Assert.AreEqual("", missing.DetectedCurrentUnit);
```

- [ ] **Step 2: Run the IO test and capture RED**

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj `
  -c Release --no-restore --filter ItTraceReaderTests
```

Expected: compile failure because `Inspect` does not exist.

- [ ] **Step 3: Extract shared inspection logic**

Read the delimited table once in `Inspect`; return the two headers and detected
units using the same token rules as `Read`. Keep all detection methods free of
numeric-magnitude heuristics.

- [ ] **Step 4: Write coordinator RED tests**

Use a fake selector and an old valid `TraceData`. Prove:

- recognized units do not open the selector;
- missing units open it with the real headers and retry using returned units;
- cancellation returns `null`;
- invalid override throws and does not replace old state.

- [ ] **Step 5: Implement coordinator and WPF selector**

`TraceImportCoordinator.ReadAsync` first calls `Inspect`. If both units are
detected, call `ItTraceReader.Read(path)`. Otherwise call
`SelectTraceUnits`, and if non-null call `ItTraceReader.Read(path, overrides)`.

The WPF dialog offers:

- Time: `s`, `ms`, `min`, `h`
- Current: `A`, `mA`, `uA`, `nA`, `pA`

It displays both original headers and has explicit Confirm/Cancel buttons.

- [ ] **Step 6: Run focused IO and Desktop GREEN**

Run the IO command from Step 2 and:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter ImportCoordinatorTests
```

Expected: all focused tests pass.

---

### Task 2: Wire Transactional i-t Import and Metadata Status

**Files:**

- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Modify: `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`

**Interfaces:**

- Consumes `TraceImportCoordinator` from Task 1.
- Silicon and sample workflows expose:

```csharp
public string TraceImportSummary { get; }
public async Task<bool> ImportTraceAsync(string path);
```

- [ ] **Step 1: Write ViewModel cancellation RED**

Set an existing trace, configure the coordinator to return `null`, call import,
and assert the same trace reference remains and the method returns `false`.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "ImportCoordinatorTests|WorkflowViewModelTests"
```

Expected: failure because workflows bypass the coordinator.

- [ ] **Step 3: Inject and use the coordinator**

Extend `MainViewModel` and child constructors with one shared
`TraceImportCoordinator`. Only call `Session.SetSiliconTrace` or
`SetSampleTrace` when `ReadAsync` returns non-null.

- [ ] **Step 4: Produce the success summary**

Format:

```text
Sample-i t.txt · 14002 点 · 0–1280 s · sec/µA 已换算为 s/A
```

Use metadata from `TraceData.Metadata`, not the selected ComboBox text.

- [ ] **Step 5: Bind summaries and busy state**

Show the summary below each i-t import control. While its import command is
executing, show “正在导入…” and disable only that command.

- [ ] **Step 6: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop test project. Expected: all pass.

---

### Task 3: Select Spectrum Worksheet and Columns

**Files:**

- Create: `csharp/src/IPCE.Desktop/Import/SpectrumImportCoordinator.cs`
- Modify: `csharp/src/IPCE.IO/Import/SpectrumReader.cs`
- Modify: `csharp/src/IPCE.IO/Tables/NpoiWorkbookReader.cs`
- Modify: `csharp/src/IPCE.Desktop/Services/ImportSelectionService.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Modify: `csharp/tests/IPCE.IO.Tests/SpectrumReaderTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/ImportCoordinatorTests.cs`

**Interfaces:**

```csharp
public static IReadOnlyList<string> SpectrumReader.DiscoverSheets(string path);

public sealed record SpectrumImportSelection(
    string SheetName,
    int WavelengthColumn,
    int IrradianceColumn);

public interface IImportSelectionService
{
    UnitOverrides? SelectTraceUnits(TraceImportInspection inspection);
    SpectrumImportSelection? SelectSpectrum(
        IReadOnlyList<string> sheets,
        Func<string, IReadOnlyList<SpectrumColumn>> discoverColumns,
        SpectrumImportSelection? suggested);
}

public sealed class SpectrumImportCoordinator
{
    public Task<SpectrumImportResult?> ReadAsync(string path);
}

public sealed record SpectrumImportResult(
    IReadOnlyList<SpectrumPoint> Points,
    SpectrumImportSelection Selection,
    string WavelengthHeader,
    string IrradianceHeader);
```

- [ ] **Step 1: Write sheet-discovery RED tests**

Assert the real default workbook exposes `Spectra`. Add a generated workbook
with two sheets and assert both names are returned in workbook order.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj `
  -c Release --no-restore --filter SpectrumReaderTests
```

Expected: compile failure because `DiscoverSheets` does not exist.

- [ ] **Step 3: Expose read-only sheet discovery**

Return `NpoiWorkbookReader.GetSheetNames(path)` as an immutable copy. Do not
expose NPOI workbook objects to Desktop.

- [ ] **Step 4: Write coordinator selection RED tests**

With a fake selector, choose sheet `Custom`, wavelength column 2, irradiance
column 4. Assert `SpectrumReader.Read` receives exactly those values.
Also test cancellation and rejection of identical column indices.

- [ ] **Step 5: Implement the selection dialog**

The dialog first selects a sheet, then populates both column ComboBoxes from
`DiscoverColumns`. Each item displays `[A] Header` and numeric count.

For the embedded default file suggest:

```csharp
new SpectrumImportSelection("Spectra", 1, 3)
```

For other files, suggest the first two distinct numeric columns.

- [ ] **Step 6: Store and display selection metadata**

`SpectrumWorkflowViewModel` stores the successful `SpectrumImportResult` and
shows sheet name, selected headers, point count, and wavelength range. A
cancelled or failed selection preserves the prior spectrum and metadata.

- [ ] **Step 7: Run focused IO and Desktop GREEN**

Run the IO command from Step 2, `ImportCoordinatorTests`, and the full Desktop
project. Expected: all pass.

---

### Task 4: External IPCE in TXT, CSV, XLS, and XLSX

**Files:**

- Modify: `csharp/src/IPCE.IO/Import/ExternalIpceReader.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/tests/IPCE.IO.Tests/ExternalIpceReaderTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Interfaces:**

- `ExternalIpceReader.Read(string path)` accepts `.txt`, `.csv`, `.xls`, and
  `.xlsx` without changing its return type.

- [ ] **Step 1: Write four-format RED tests**

Create the same data in all formats:

```text
Wavelength_nm,IPCE_percent
500,120
400,40
500,80
600,120
```

Assert each produces sorted points `(400, 40)`, `(500, 100)`, and `(600, 120)`.
The last point proves a finite value above `100%` is preserved.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.IO.Tests/IPCE.IO.Tests.csproj `
  -c Release --no-restore --filter ExternalIpceReaderTests
```

Expected: XLS and XLSX cases fail with `IPCE:UnsupportedExternalIPCE`.

- [ ] **Step 3: Implement workbook first-two-numeric-column reading**

For `.xls` and `.xlsx`, read the first sheet through
`NpoiWorkbookReader.ReadFirstSheet`. Identify the first two columns containing
at least two numeric values. Pair numeric wavelength/IPCE cells by row, then
apply the same sorting and duplicate averaging as text input.

If fewer than two numeric columns exist, throw:

```csharp
new IpceException(
    "IPCE:InvalidExternalIPCE",
    "未能识别出外部 IPCE 的波长列和 IPCE 列。");
```

- [ ] **Step 4: Expand the file dialog filter**

Use:

```text
IPCE 数据|*.txt;*.csv;*.xls;*.xlsx|所有文件|*.*
```

- [ ] **Step 5: Auto-select external source**

After successful import, call:

```csharp
Session.SelectIpceSource(IpceSource.External);
```

Do not clear `Session.CalculatedIpce`.

- [ ] **Step 6: Run focused and end-to-end GREEN**

Run Step 2 and:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter EndToEndWorkflowTests
```

Expected: all tests pass.

---

### Task 5: Transactional Anchor Table Editing

**Files:**

- Create: `csharp/src/IPCE.Desktop/ViewModels/AnchorRowViewModel.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/AnchorTableEditingTests.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/ResultTabs.xaml.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/AnchorEditingTests.cs`

**Interfaces:**

```csharp
public sealed class AnchorRowViewModel : ViewModelBase
{
    public double WavelengthNm { get; set; }
    public double ConfirmedTimeSeconds { get; set; }
}
```

Each measurement workflow produces:

```csharp
public ObservableCollection<AnchorRowViewModel> EditableAnchors { get; }
public void ReplaceAnchors(IEnumerable<AnchorRowViewModel> rows);
public void DeleteAnchor(AnchorRowViewModel row);
public void ConfirmAnchor(
    double wavelengthNm,
    double clickedTimeSeconds,
    double? adjustedTimeSeconds = null);
```

- [ ] **Step 1: Write edit/delete RED tests**

Start with two valid anchors. Edit a cloned row, call `ReplaceAnchors`, and
assert session order. Then attempt a duplicate wavelength and assert the
exception leaves session anchors and editable rows unchanged. Test deletion
and silicon/sample isolation.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "AnchorTableEditingTests|AnchorEditingTests"
```

Expected: compile failure because editable rows and replacement methods do not
exist.

- [ ] **Step 3: Implement clone-validate-replace**

Convert rows to an array of `AnchorPoint`, validate through
`Session.SetSiliconAnchors` or `SetSampleAnchors`, and only after success
resynchronize `EditableAnchors`. If validation fails, immediately rebuild
`EditableAnchors` from the unchanged session collection before rethrowing the
validation exception. Never leave partially edited rows visible as accepted
state.

- [ ] **Step 4: Make only anchor grids editable**

Override the global read-only DataGrid style:

```xml
<DataGrid IsReadOnly="False"
          AutoGenerateColumns="False"
          ItemsSource="{Binding Silicon.EditableAnchors}">
```

Define two numeric columns with Chinese headers `波长 (nm)` and `确认时间 (s)`,
using the finite-double converter. Add explicit Add, Apply Changes, and Delete
buttons for each owner.

- [ ] **Step 5: Upgrade graph confirmation**

Replace the wavelength-only dialog with two fields: wavelength and snapped
time. Allow time adjustment. Pass the adjusted time into `ConfirmAnchor`.
If the wavelength already exists, the dialog states that the row will be
updated.

- [ ] **Step 6: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all tests pass.

- [ ] **Step 7: Run full solution verification and record checkpoint**

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Append exact results and changed files to
`docs/superpowers/progress/ipce-csharp-migration-progress.md`.
