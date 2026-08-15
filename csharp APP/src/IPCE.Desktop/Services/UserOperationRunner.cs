using System.IO;
using IPCE.Core.Errors;

namespace IPCE.Desktop.Services;

public interface IUserOperationRunner
{
    bool Run(string title, Action operation);

    Task<bool> RunAsync(string title, Func<Task> operation);
}

public sealed class UserOperationRunner : IUserOperationRunner
{
    private readonly IUserNotificationService _notifications;
    private readonly LocalCrashLogger _crashLogger;

    public UserOperationRunner(
        IUserNotificationService notifications,
        LocalCrashLogger crashLogger)
    {
        _notifications = notifications ??
            throw new ArgumentNullException(nameof(notifications));
        _crashLogger = crashLogger ??
            throw new ArgumentNullException(nameof(crashLogger));
    }

    public static IUserOperationRunner CreateDefault() =>
        new UserOperationRunner(
            new UserNotificationService(),
            new LocalCrashLogger());

    public bool Run(string title, Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            operation();
            return true;
        }
        catch (Exception exception)
        {
            Report(title, exception);
            return false;
        }
    }

    public async Task<bool> RunAsync(
        string title,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            await operation();
            return true;
        }
        catch (Exception exception)
        {
            Report(title, exception);
            return false;
        }
    }

    private void Report(string title, Exception exception)
    {
        if (IsExpected(exception))
        {
            _notifications.ShowWarning(title, exception.Message);
            return;
        }

        string path = TryLog(exception);
        _notifications.ShowError(
            title,
            $"发生未预料的错误。诊断日志：\n{path}");
    }

    private string TryLog(Exception exception)
    {
        try
        {
            return _crashLogger.Log(exception);
        }
        catch
        {
            return "日志写入失败";
        }
    }

    private static bool IsExpected(Exception exception) =>
        exception is IpceException or
            IOException or
            UnauthorizedAccessException;
}
