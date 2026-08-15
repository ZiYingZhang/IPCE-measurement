using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace IPCE.Desktop.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private const string EnglishCulture = "en-US";
    private const string ChineseCulture = "zh-CN";
    private static readonly ResourceManager Resources = new(
        "IPCE.Desktop.Resources.Strings",
        typeof(LocalizationService).Assembly);
    private readonly LanguagePreferenceStore _preferenceStore;
    private AppLanguage _currentLanguage;

    public LocalizationService(
        LanguagePreferenceStore preferenceStore,
        CultureInfo systemCulture)
    {
        _preferenceStore = preferenceStore ??
            throw new ArgumentNullException(nameof(preferenceStore));
        ArgumentNullException.ThrowIfNull(systemCulture);

        _currentLanguage = FromCultureName(
            _preferenceStore.Load() ?? systemCulture.Name);
        ApplyThreadCulture();
    }

    public static LocalizationService Current { get; } = new(
        new LanguagePreferenceStore(),
        CultureInfo.CurrentUICulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
            {
                return;
            }

            _currentLanguage = value;
            ApplyThreadCulture();
            _preferenceStore.Save(CurrentCultureName);
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(CurrentCultureName)));
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string CurrentCultureName =>
        CurrentLanguage == AppLanguage.SimplifiedChinese
            ? ChineseCulture
            : EnglishCulture;

    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return Resources.GetString(key, CurrentCulture) ?? $"[{key}]";
        }
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(CurrentCulture, this[key], arguments);

    private CultureInfo CurrentCulture =>
        CultureInfo.GetCultureInfo(CurrentCultureName);

    private static AppLanguage FromCultureName(string cultureName) =>
        cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.SimplifiedChinese
            : AppLanguage.English;

    private void ApplyThreadCulture()
    {
        CultureInfo culture = CurrentCulture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
