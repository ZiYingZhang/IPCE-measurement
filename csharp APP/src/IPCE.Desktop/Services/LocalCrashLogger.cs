using System.Globalization;
using System.IO;
using System.Text;

namespace IPCE.Desktop.Services;

public sealed class LocalCrashLogger
{
    private readonly string _logDirectory;

    public LocalCrashLogger(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "IPCEApp",
            "Logs");
    }

    public string Log(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Directory.CreateDirectory(_logDirectory);
        string timestamp = DateTime.Now.ToString(
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture);
        string path = Path.Combine(
            _logDirectory,
            $"IPCEApp-{timestamp}.log");
        for (int suffix = 1; File.Exists(path); suffix++)
        {
            path = Path.Combine(
                _logDirectory,
                $"IPCEApp-{timestamp}-{suffix}.log");
        }

        string contents = new StringBuilder()
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine("Application: IPCEApp")
            .AppendLine($"Runtime: {Environment.Version}")
            .AppendLine($"OS: {Environment.OSVersion}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .ToString();
        File.WriteAllText(path, contents, new UTF8Encoding(true));
        return path;
    }
}
