using System.IO;
using System.Text.Json;

namespace IPCE.Desktop.Localization;

public sealed class LanguagePreferenceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> SupportedCultures =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "en-US",
            "zh-CN",
        };

    public LanguagePreferenceStore(string? path = null)
    {
        PreferencePath = path ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "IPCEApp",
            "settings.json");
    }

    public string PreferencePath { get; }

    public string? Load()
    {
        try
        {
            if (!File.Exists(PreferencePath))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(PreferencePath);
            Preference? preference =
                JsonSerializer.Deserialize<Preference>(stream, SerializerOptions);
            return preference?.Language is { } language &&
                SupportedCultures.Contains(language)
                    ? Normalize(language)
                    : null;
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                NotSupportedException)
        {
            return null;
        }
    }

    public void Save(string cultureName)
    {
        if (!SupportedCultures.Contains(cultureName))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(PreferencePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(PreferencePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                JsonSerializer.Serialize(
                    stream,
                    new Preference(Normalize(cultureName)));
                stream.Flush(true);
            }

            File.Move(temporaryPath, PreferencePath, true);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException)
        {
            // Preference persistence must never prevent application use.
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static string Normalize(string cultureName) =>
        cultureName.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";

    private sealed record Preference(string Language);
}
