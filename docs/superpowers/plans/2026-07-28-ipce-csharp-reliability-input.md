# IPCE C# Reliability and Numeric Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all expected user errors recoverable, accept scientific decimal input reliably, and prevent stale results from being used or exported.

**Architecture:** Add a testable user-operation boundary around commands, a culture-aware finite-double converter for WPF input, and explicit `Missing/Current/Stale` result status in `SessionState`. ViewModel parameter setters invalidate only their dependent results, while retaining stale data for grey comparison.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, MSTest 4, existing `IPCE.Core`, `IPCE.IO`, and `IPCE.Desktop`.

## Global Constraints

- Work only in `E:\Research Library\Data\Codes\IPCE measurement`.
- Preserve existing MATLAB numerical formulas, interpolation, integration, units, and tolerances.
- External-IPCE post-processing must remain independent of calibration, traces, and anchors.
- Expected input/data errors must not terminate the WPF application or create crash logs.
- Never use a stale result as calculation input or export content.
- The repository is not a Git repository. Do not initialize Git or add commit steps.
- Every behavior change requires a failing regression test before production code.
- After every functional checkpoint, run the focused tests and the full affected test project.

---

## File Structure

**Create**

- `csharp/src/IPCE.Desktop/Services/UserOperationRunner.cs` — classifies expected versus unexpected operation failures and publishes user-facing messages.
- `csharp/src/IPCE.Desktop/Services/UserNotificationService.cs` — WPF MessageBox adapter behind a testable interface.
- `csharp/src/IPCE.Desktop/Input/FiniteDoubleConverter.cs` — finite decimal conversion without per-keystroke model mutation.
- `csharp/src/IPCE.Desktop/State/ResultStatus.cs` — result freshness enum and immutable status record.
- `csharp/tests/IPCE.Desktop.Tests/UserOperationRunnerTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/FiniteDoubleConverterTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/ResultFreshnessTests.cs`

**Modify**

- `csharp/src/IPCE.Desktop/App.xaml`
- `csharp/src/IPCE.Desktop/App.xaml.cs`
- `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- `csharp/src/IPCE.Desktop/State/SessionState.cs`
- `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`
- `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml.cs`
- `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/SessionStateTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`
- `docs/superpowers/progress/ipce-csharp-migration-progress.md`

---

### Task 1: Testable User-Operation Boundary

**Files:**

- Create: `csharp/src/IPCE.Desktop/Services/UserOperationRunner.cs`
- Create: `csharp/src/IPCE.Desktop/Services/UserNotificationService.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/UserOperationRunnerTests.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/ViewModelBase.cs`

**Interfaces:**

- Produces:

```csharp
public interface IUserNotificationService
{
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
}

public interface IUserOperationRunner
{
    bool Run(string title, Action operation);
    Task<bool> RunAsync(string title, Func<Task> operation);
}

public sealed class UserOperationRunner : IUserOperationRunner
{
    public UserOperationRunner(
        IUserNotificationService notifications,
        LocalCrashLogger crashLogger);
}
```

- `IpceException`, `IOException`, and `UnauthorizedAccessException` are expected user-operation failures.
- Other exceptions are unexpected: log them locally, show the log path, return `false`, and do not rethrow through a command.

- [ ] **Step 1: Write expected-error tests**

Add tests proving both synchronous and asynchronous `IpceException` calls:

```csharp
[TestMethod]
public async Task ExpectedErrors_ShowWarningWithoutWritingCrashLog()
{
    var notifications = new RecordingNotifications();
    using var directory = new TemporaryDirectory();
    var logger = new LocalCrashLogger(directory.Path);
    var runner = new UserOperationRunner(notifications, logger);

    bool sync = runner.Run(
        "计算功率密度",
        () => throw new IpceException("IPCE:InvalidSchedule", "范围越界"));
    bool asyncResult = await runner.RunAsync(
        "导入样品 i-t",
        () => Task.FromException(
            new IpceException("IPCE:InvalidTrace", "数据不足")));

    Assert.IsFalse(sync);
    Assert.IsFalse(asyncResult);
    Assert.AreEqual(2, notifications.Warnings.Count);
    Assert.AreEqual(0, Directory.GetFiles(directory.Path).Length);
}
```

Use a temporary directory with the real `LocalCrashLogger` for the unexpected-error test and assert one log file is created.

- [ ] **Step 2: Run the focused test and capture RED**

Run:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter UserOperationRunnerTests
```

Expected: compile failure because the operation interfaces and runner do not exist.

- [ ] **Step 3: Implement the notification interface and runner**

Implement the exact classification:

```csharp
private static bool IsExpected(Exception exception) =>
    exception is IpceException or IOException or UnauthorizedAccessException;
```

For expected failures, call `ShowWarning(title, exception.Message)`. For unexpected failures, call `_crashLogger.Log(exception)` and
`ShowError(title, $"发生未预料的错误。诊断日志：\n{path}")`.

- [ ] **Step 4: Add operation-aware command wrappers**

Add `SafeRelayCommand` and `SafeAsyncRelayCommand` in `ViewModelBase.cs`.
They accept `IUserOperationRunner`, an operation title, and the existing execute/can-execute delegates. Their public `Execute` methods call `Run` or `RunAsync`; exceptions must not leave the command boundary.

- [ ] **Step 5: Run focused GREEN**

Run the command from Step 2. Expected: all `UserOperationRunnerTests` pass.

- [ ] **Step 6: Run the Desktop regression**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore
```

Expected: all existing Desktop tests still pass.

---

### Task 2: Wire Safe Commands and Keep the Application Alive

**Files:**

- Modify: `csharp/src/IPCE.Desktop/App.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/MainWindow.xaml.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/MainViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/MainWindowSmokeTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`

**Interfaces:**

- Consumes: `IUserOperationRunner` from Task 1.
- `MainViewModel` gains:

```csharp
public MainViewModel(
    SessionState session,
    SynchronizationContext? synchronizationContext = null,
    IUserOperationRunner? operations = null);
```

- All import, calculation, and integration commands use safe command wrappers.

- [ ] **Step 1: Write a live-WPF survival test**

On an STA thread, create the real `App` and `MainWindow`, inject a `MainViewModel`
whose runner records warnings, invoke a calculation command that throws
`IPCE:ScheduleOutsideTrace`, pump the dispatcher, and assert:

```csharp
Assert.IsTrue(window.IsVisible);
Assert.AreEqual(1, notifications.Warnings.Count);
Assert.IsFalse(application.Dispatcher.HasShutdownStarted);
```

- [ ] **Step 2: Run the survival test and capture RED**

Run:

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "MainWindowSmokeTests|WorkflowViewModelTests"
```

Expected: failure because workflow commands still throw through `RelayCommand`.

- [ ] **Step 3: Inject one operation runner through the ViewModel tree**

Construct the default WPF runner in `MainWindow`:

```csharp
var operations = new UserOperationRunner(
    new UserNotificationService(),
    new LocalCrashLogger());
DataContext = new MainViewModel(
    new SessionState(),
    SynchronizationContext.Current,
    operations);
```

Preserve the existing constructor overload used by tests and smoke mode.

- [ ] **Step 4: Replace unsafe workflow commands**

Use `SafeRelayCommand` for power calculation, sample IPCE, integration, and
non-dialog export commands. Use `SafeAsyncRelayCommand` for every import.
Keep command `CanExecute` predicates unchanged in this step.

- [ ] **Step 5: Mark dispatcher exceptions handled**

In `App.OnDispatcherUnhandledException`, after logging and notifying, set:

```csharp
eventArgs.Handled = true;
```

Do not classify routine `IpceException` here; Task 1 command boundaries must
catch those first.

- [ ] **Step 6: Run focused and full Desktop GREEN**

Run Step 2, then the full Desktop project. Expected: all tests pass and the
live window remains open after the injected expected error.

---

### Task 3: Culture-Aware Finite Decimal Input

**Files:**

- Create: `csharp/src/IPCE.Desktop/Input/FiniteDoubleConverter.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/FiniteDoubleConverterTests.cs`
- Modify: `csharp/src/IPCE.Desktop/App.xaml`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`

**Interfaces:**

```csharp
public sealed class FiniteDoubleConverter : IValueConverter
{
    public object Convert(
        object value, Type targetType, object parameter, CultureInfo culture);

    public object ConvertBack(
        object value, Type targetType, object parameter, CultureInfo culture);
}
```

- Output formatting uses `"G17"` and invariant storage.
- Input parsing uses `NumberStyles.AllowLeadingSign |
  NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent`.
- Try the binding culture first, then invariant culture.
- Reject non-finite results and do not allow thousands separators.

- [ ] **Step 1: Write converter RED tests**

Cover:

```csharp
[DataRow("0.36", "zh-CN", 0.36)]
[DataRow(".36", "zh-CN", 0.36)]
[DataRow("0,36", "de-DE", 0.36)]
[DataRow("-2.5e-6", "zh-CN", -2.5e-6)]
```

Also assert `"NaN"`, `"Infinity"`, `"1,000.2"` under `en-US`, and blank input
throw a validation exception with a Chinese message.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter FiniteDoubleConverterTests
```

Expected: compile failure because the converter does not exist.

- [ ] **Step 3: Implement the converter**

Use:

```csharp
private const NumberStyles Styles =
    NumberStyles.AllowLeadingSign |
    NumberStyles.AllowDecimalPoint |
    NumberStyles.AllowExponent;
```

Throw `ValidationException("请输入有限数值。")` when neither culture parses or
the parsed value is not finite.

- [ ] **Step 4: Register the converter**

Add to `App.xaml`:

```xml
<input:FiniteDoubleConverter x:Key="FiniteDoubleConverter" />
```

Add the `input` namespace for `IPCE.Desktop.Input`.

- [ ] **Step 5: Replace all scientific numeric bindings**

For every numeric `TextBox` in `WorkflowControls.xaml`, use:

```xml
Text="{Binding AreaSquareCentimetres,
    Converter={StaticResource FiniteDoubleConverter},
    UpdateSourceTrigger=LostFocus,
    ValidatesOnExceptions=True,
    NotifyOnValidationError=True}"
```

Do not retain `UpdateSourceTrigger=PropertyChanged` on scientific numeric
fields.

- [ ] **Step 6: Add a real binding test**

On an STA thread, bind a `TextBox` to `Silicon.AreaSquareCentimetres`, set
`Text = "0.36"`, call `GetBindingExpression(TextBox.TextProperty).UpdateSource()`,
and assert the ViewModel value is `0.36` with no validation error.

- [ ] **Step 7: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all tests pass.

---

### Task 4: Explicit Result Freshness

**Files:**

- Create: `csharp/src/IPCE.Desktop/State/ResultStatus.cs`
- Create: `csharp/tests/IPCE.Desktop.Tests/ResultFreshnessTests.cs`
- Modify: `csharp/src/IPCE.Desktop/State/SessionState.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/SessionStateTests.cs`

**Interfaces:**

```csharp
public enum ResultFreshness
{
    Missing,
    Current,
    Stale,
}

public sealed record ResultStatus(
    ResultFreshness Freshness,
    string Reason)
{
    public bool CanUse => Freshness == ResultFreshness.Current;
}
```

`SessionState` produces:

```csharp
public ResultStatus PowerDensityStatus { get; }
public ResultStatus CalculatedIpceStatus { get; }
public ResultStatus IntegrationStatus { get; }

public void MarkPowerDensityStale(string reason);
public void MarkCalculatedIpceStale(string reason);
public void MarkIntegrationStale(string reason);
```

- [ ] **Step 1: Write dependency RED tests**

Create valid power, calculated IPCE, external IPCE, and integration state.
Assert:

```csharp
state.MarkPowerDensityStale("硅面积已改变");

Assert.AreEqual(ResultFreshness.Stale, state.PowerDensityStatus.Freshness);
Assert.AreEqual(ResultFreshness.Stale, state.CalculatedIpceStatus.Freshness);
Assert.IsNotNull(state.PowerDensity);
Assert.IsNotNull(state.CalculatedIpce);
Assert.IsNotNull(state.ExternalIpce);
```

Add separate tests for sample-only invalidation, integration-bound invalidation,
and external-IPCE independence.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "ResultFreshnessTests|SessionStateTests"
```

Expected: compile failure because result status does not exist.

- [ ] **Step 3: Implement status transitions**

Setting a new result marks it `Current`. An upstream replacement or explicit
parameter invalidation marks retained dependent results `Stale`; if no retained
result exists, status remains `Missing`.

Raise `PropertyChanged` for both the data property and its status property when
the status changes.

- [ ] **Step 4: Guard calculations**

Before sample IPCE calculation, require `PowerDensityStatus.CanUse`. Before
integration with calculated IPCE, require `CalculatedIpceStatus.CanUse`.
Throw a stable `IpceException` with code `IPCE:StaleResult` and the recorded
reason when a stale result is requested.

- [ ] **Step 5: Update older invalidation expectations**

Change only tests that previously required dependent data to become `null`.
They must now require retained data plus `Stale` status. Keep transactional
failed-import and failed-integration tests unchanged.

- [ ] **Step 6: Run focused and full Desktop GREEN**

Run Step 2 and the full Desktop project. Expected: all tests pass.

---

### Task 5: Parameter-Driven Invalidation and Clear Status Messages

**Files:**

- Modify: `csharp/src/IPCE.Desktop/ViewModels/SiliconWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SampleWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/ViewModels/SpectrumWorkflowViewModel.cs`
- Modify: `csharp/src/IPCE.Desktop/Views/WorkflowControls.xaml`
- Modify: `csharp/tests/IPCE.Desktop.Tests/WorkflowViewModelTests.cs`
- Modify: `csharp/tests/IPCE.Desktop.Tests/EndToEndWorkflowTests.cs`

**Interfaces:**

- Each workflow exposes:

```csharp
public string PrerequisiteMessage { get; }
public string ResultStatusMessage { get; }
```

- Every successful parameter setter calls the narrowest stale-marking method.

- [ ] **Step 1: Write parameter invalidation RED tests**

Calculate a valid result, then mutate one parameter at a time:

```csharp
viewModel.Silicon.AreaSquareCentimetres = 0.64;
Assert.AreEqual(
    ResultFreshness.Stale,
    session.PowerDensityStatus.Freshness);
StringAssert.Contains(
    session.PowerDensityStatus.Reason,
    "硅面积");
```

Repeat for wavelength grid, alignment mode, fixed start, Delay, average
duration, dark toggle/range, sample area, and integration minimum/maximum.

- [ ] **Step 2: Run and capture RED**

```powershell
dotnet test csharp/tests/IPCE.Desktop.Tests/IPCE.Desktop.Tests.csproj `
  -c Release --no-restore --filter "WorkflowViewModelTests|EndToEndWorkflowTests"
```

Expected: failures because parameter setters do not mark results stale.

- [ ] **Step 3: Add invalidation to setters**

Only invalidate after `SetProperty` returns `true`. Use parameter-specific
Chinese reasons, for example:

```csharp
if (SetProperty(ref _areaSquareCentimetres, value))
{
    Session.MarkPowerDensityStale("硅面积已改变");
}
```

Sample setters call `MarkCalculatedIpceStale`; integration-bound setters call
`MarkIntegrationStale`.

- [ ] **Step 4: Restore the sample fixed-start default**

Set the sample ViewModel default to `50d`, matching the MATLAB UI. Add an
assertion to `MainWindowSmokeTests.StartupDefaults_LoadAllFourIndependentInputs`.

- [ ] **Step 5: Add prerequisite and result messages**

Messages must name the first missing or stale prerequisite:

```text
缺少：样品 i-t
需要重新计算：硅面积已改变
可以计算：轨迹与调度覆盖将在执行前检查
```

Bind these messages below the corresponding action buttons. A disabled button
must never be the only explanation.

- [ ] **Step 6: Prevent stale export**

`BuildSelectedExportTables` must omit stale result tables. If the user's
selection contains only stale or missing results, throw:

```csharp
new IpceException(
    "IPCE:NoCurrentExportSelection",
    "所选结果已过期或尚未生成，请重新计算后导出。");
```

Add XLSX, CSV, and MAT export tests proving stale results are excluded.

- [ ] **Step 7: Run full solution verification**

```powershell
dotnet build csharp/IPCE.slnx -c Release --no-restore
dotnet test csharp/IPCE.slnx -c Release --no-build --no-restore
```

Expected: build succeeds with zero warnings/errors and every .NET test passes.

- [ ] **Step 8: Record the phase checkpoint**

Append exact commands, pass counts, and changed files to
`docs/superpowers/progress/ipce-csharp-migration-progress.md`. Explicitly state
that no Git commit exists because the project is not a Git repository.
