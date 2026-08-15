using System.IO;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;

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
    private readonly ILocalizationService _localization;
    private readonly UserMessageLocalizer _messageLocalizer;

    public UserOperationRunner(
        IUserNotificationService notifications,
        LocalCrashLogger crashLogger,
        ILocalizationService? localization = null)
    {
        _notifications = notifications ??
            throw new ArgumentNullException(nameof(notifications));
        _crashLogger = crashLogger ??
            throw new ArgumentNullException(nameof(crashLogger));
        _localization = localization ?? LocalizationService.Current;
        _messageLocalizer = new UserMessageLocalizer(_localization);
    }

    public static IUserOperationRunner CreateDefault(
        ILocalizationService? localization = null) =>
        new UserOperationRunner(
            new UserNotificationService(),
            new LocalCrashLogger(),
            localization ?? LocalizationService.Current);

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
        if (exception is IpceException ipceException)
        {
            _notifications.ShowWarning(
                title,
                _messageLocalizer.Localize(ipceException));
            return;
        }

        if (IsExpected(exception))
        {
            _notifications.ShowWarning(
                title,
                _messageLocalizer.Localize(exception));
            return;
        }

        string path = TryLog(exception);
        _notifications.ShowError(
            title,
            _localization.Format("Error.Unexpected", path));
    }

    private string TryLog(Exception exception)
    {
        try
        {
            return _crashLogger.Log(exception);
        }
        catch
        {
            return _localization["Error.LogWriteFailed"];
        }
    }

    private static bool IsExpected(Exception exception) =>
        exception is IOException or
            UnauthorizedAccessException;
}
