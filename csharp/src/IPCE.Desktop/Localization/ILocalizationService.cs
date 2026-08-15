using System.ComponentModel;

namespace IPCE.Desktop.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    AppLanguage CurrentLanguage { get; set; }

    string CurrentCultureName { get; }

    string this[string key] { get; }

    string Format(string key, params object?[] arguments);

    event EventHandler? LanguageChanged;
}
