using Microsoft.Win32;

namespace IPCE.Desktop.Services;

public interface IFileDialogService
{
    string? OpenFile(string title, string filter);

    string? SaveFile(
        string title,
        string filter,
        string defaultExtension);
}

public sealed class FileDialogService : IFileDialogService
{
    public string? OpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(
        string title,
        string filter,
        string defaultExtension)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
