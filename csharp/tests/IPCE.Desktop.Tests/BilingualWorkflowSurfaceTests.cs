using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Services;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.Desktop.Views;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BilingualWorkflowSurfaceTests
{
    [TestMethod]
    public void FreshWorkflowMessages_SwitchBetweenEnglishAndChinese()
    {
        using var fixture = new LocalizationFixture("en-US");
        var main = new MainViewModel(
            new SessionState(),
            localization: fixture.Service);

        Assert.AreEqual(
            "Missing: silicon i-t trace",
            main.Silicon.PrerequisiteMessage);
        Assert.AreEqual(
            "Power density has not been generated",
            main.Silicon.ResultStatusMessage);
        Assert.AreEqual(
            "Missing: sample i-t trace",
            main.Sample.PrerequisiteMessage);
        Assert.AreEqual(
            "Missing: solar spectrum",
            main.Spectrum.PrerequisiteMessage);

        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;

        Assert.AreEqual(
            "缺少：硅 i-t",
            main.Silicon.PrerequisiteMessage);
        Assert.AreEqual(
            "尚未生成功率密度",
            main.Silicon.ResultStatusMessage);
        Assert.AreEqual(
            "缺少：样品 i-t",
            main.Sample.PrerequisiteMessage);
        Assert.AreEqual(
            "缺少：太阳光谱",
            main.Spectrum.PrerequisiteMessage);
    }

    [TestMethod]
    public void ExpectedIpceError_UsesStableCodeInSelectedLanguage()
    {
        using var fixture = new LocalizationFixture("en-US");
        var notifications = new RecordingNotifications();
        string logDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-localized-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logDirectory);
        try
        {
            var runner = new UserOperationRunner(
                notifications,
                new LocalCrashLogger(logDirectory),
                fixture.Service);

            runner.Run(
                fixture.Service["Operation.CalculatePowerDensity"],
                () => throw new IpceException(
                    "IPCE:InvalidTrace",
                    "未本地化的原始诊断"));

            Assert.AreEqual(1, notifications.Warnings.Count);
            Assert.AreEqual(
                "Calculate incident power density",
                notifications.Warnings[0].Title);
            Assert.AreEqual(
                "The i-t trace is invalid.",
                notifications.Warnings[0].Message);
            Assert.IsFalse(
                notifications.Warnings[0].Message.Contains(
                    "未本地化",
                    StringComparison.Ordinal));

            fixture.Service.CurrentLanguage =
                AppLanguage.SimplifiedChinese;
            runner.Run(
                fixture.Service["Operation.CalculatePowerDensity"],
                () => throw new IpceException(
                    "IPCE:InvalidTrace",
                    "raw diagnostic"));

            Assert.AreEqual(
                "计算入射光功率密度",
                notifications.Warnings[1].Title);
            Assert.AreEqual(
                "i-t 轨迹无效。",
                notifications.Warnings[1].Message);
        }
        finally
        {
            Directory.Delete(logDirectory, true);
        }
    }

    [TestMethod]
    public void StoredFreshnessReason_RerendersWithoutChangingState()
    {
        using var fixture = new LocalizationFixture("en-US");
        var formatter = new LocalizedReasonFormatter(fixture.Service);

        Assert.AreEqual(
            "Silicon area changed",
            formatter.Format("硅面积已改变"));

        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;

        Assert.AreEqual(
            "硅面积已改变",
            formatter.Format("硅面积已改变"));
    }

    [TestMethod]
    public void WorkflowControlText_RerendersInPlace()
    {
        Exception? failure = null;
        using var fixture = new LocalizationFixture("en-US");
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                var main = new MainViewModel(
                    new SessionState(),
                    localization: fixture.Service);
                window = new MainWindow(main, false);
                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);
                var workflow = Assert.IsInstanceOfType<WorkflowControls>(
                    window.FindName("WorkflowPanel"));
                var calculate = Assert.IsInstanceOfType<Button>(
                    workflow.FindName("CalculatePowerButton"));
                Assert.AreEqual(
                    "Calculate power density",
                    calculate.Content);

                fixture.Service.CurrentLanguage =
                    AppLanguage.SimplifiedChinese;
                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);

                Assert.AreEqual("计算功率密度", calculate.Content);
                Assert.AreSame(main, window.DataContext);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"Workflow localization failed: {failure}",
                failure);
        }
    }

    private sealed class LocalizationFixture : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-workflow-language-{Guid.NewGuid():N}.json");

        public LocalizationFixture(string cultureName) =>
            Service = new LocalizationService(
                new LanguagePreferenceStore(_path),
                CultureInfo.GetCultureInfo(cultureName));

        public LocalizationService Service { get; }

        public void Dispose()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    private sealed class RecordingNotifications : IUserNotificationService
    {
        public List<(string Title, string Message)> Warnings { get; } = [];

        public void ShowWarning(string title, string message) =>
            Warnings.Add((title, message));

        public void ShowError(string title, string message) =>
            Assert.Fail($"Unexpected error notification: {title}: {message}");
    }
}
