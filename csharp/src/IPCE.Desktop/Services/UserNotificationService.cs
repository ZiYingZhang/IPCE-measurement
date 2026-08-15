using System.Windows;

namespace IPCE.Desktop.Services;

public interface IUserNotificationService
{
    void ShowWarning(string title, string message);

    void ShowError(string title, string message);
}

public sealed class UserNotificationService : IUserNotificationService
{
    public void ShowWarning(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

    public void ShowError(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
