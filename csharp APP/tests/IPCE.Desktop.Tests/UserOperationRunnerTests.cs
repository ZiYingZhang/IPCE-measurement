using System.IO;
using IPCE.Core.Errors;
using IPCE.Desktop.Services;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class UserOperationRunnerTests
{
    [TestMethod]
    public async Task ExpectedErrors_ShowWarningsWithoutWritingCrashLogs()
    {
        using var directory = new TemporaryDirectory();
        var notifications = new RecordingNotifications();
        var runner = new UserOperationRunner(
            notifications,
            new LocalCrashLogger(directory.Path));

        bool synchronousResult = runner.Run(
            "计算功率密度",
            () => throw new IpceException(
                "IPCE:InvalidSchedule",
                "范围越界"));
        bool asynchronousResult = await runner.RunAsync(
            "导入样品 i-t",
            () => Task.FromException(
                new IpceException(
                    "IPCE:InvalidTrace",
                    "数据不足")));

        Assert.IsFalse(synchronousResult);
        Assert.IsFalse(asynchronousResult);
        CollectionAssert.AreEqual(
            new[]
            {
                ("计算功率密度", "范围越界"),
                ("导入样品 i-t", "数据不足"),
            },
            notifications.Warnings);
        Assert.AreEqual(0, Directory.GetFiles(directory.Path).Length);
    }

    [TestMethod]
    public void UnexpectedError_IsLoggedAndReportedWithoutRethrowing()
    {
        using var directory = new TemporaryDirectory();
        var notifications = new RecordingNotifications();
        var runner = new UserOperationRunner(
            notifications,
            new LocalCrashLogger(directory.Path));

        bool result = runner.Run(
            "计算样品 IPCE",
            () => throw new InvalidOperationException(
                "unexpected marker"));

        Assert.IsFalse(result);
        Assert.AreEqual(0, notifications.Warnings.Count);
        Assert.AreEqual(1, notifications.Errors.Count);
        Assert.AreEqual("计算样品 IPCE", notifications.Errors[0].Title);
        StringAssert.Contains(
            notifications.Errors[0].Message,
            "诊断日志");
        string[] logPaths = Directory.GetFiles(directory.Path);
        Assert.AreEqual(1, logPaths.Length);
        string logPath = logPaths[0];
        StringAssert.Contains(
            File.ReadAllText(logPath),
            "unexpected marker");
    }

    [TestMethod]
    public async Task SafeCommands_RouteFailuresThroughOperationRunner()
    {
        using var directory = new TemporaryDirectory();
        var notifications = new RecordingNotifications();
        var runner = new UserOperationRunner(
            notifications,
            new LocalCrashLogger(directory.Path));
        var synchronous = new SafeRelayCommand(
            runner,
            "计算功率密度",
            _ => throw new IpceException(
                "IPCE:InvalidSchedule",
                "同步失败"));
        var asynchronous = new SafeAsyncRelayCommand(
            runner,
            "导入样品 i-t",
            _ => Task.FromException(
                new IpceException(
                    "IPCE:InvalidTrace",
                    "异步失败")));

        synchronous.Execute(null);
        await asynchronous.ExecuteAsync(null);

        Assert.AreEqual(2, notifications.Warnings.Count);
        Assert.IsTrue(synchronous.CanExecute(null));
        Assert.IsTrue(asynchronous.CanExecute(null));
        Assert.AreEqual(0, Directory.GetFiles(directory.Path).Length);
    }

    private sealed class RecordingNotifications
        : IUserNotificationService
    {
        public List<(string Title, string Message)> Warnings { get; } =
            [];

        public List<(string Title, string Message)> Errors { get; } =
            [];

        public void ShowWarning(string title, string message) =>
            Warnings.Add((title, message));

        public void ShowError(string title, string message) =>
            Errors.Add((title, message));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-operation-runner-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
