using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace IPCE.Desktop.Localization;

internal sealed class EnglishPlotLocalizationService : ILocalizationService
{
    private const string EnglishCultureName = "en-US";
    private static readonly CultureInfo EnglishCulture =
        CultureInfo.GetCultureInfo(EnglishCultureName);
    private static readonly ResourceManager Resources = new(
        "IPCE.Desktop.Resources.Strings",
        typeof(EnglishPlotLocalizationService).Assembly);

    private EnglishPlotLocalizationService()
    {
    }

    public static ILocalizationService Instance { get; } =
        new EnglishPlotLocalizationService();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public event EventHandler? LanguageChanged
    {
        add { }
        remove { }
    }

    public AppLanguage CurrentLanguage
    {
        get => AppLanguage.English;
        set
        {
            if (value != AppLanguage.English)
            {
                throw new InvalidOperationException(
                    "Plot-internal localization is fixed to English.");
            }
        }
    }

    public string CurrentCultureName => EnglishCultureName;

    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return Resources.GetString(key, EnglishCulture) ?? $"[{key}]";
        }
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(EnglishCulture, this[key], arguments);
}