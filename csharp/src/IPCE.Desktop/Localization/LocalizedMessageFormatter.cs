namespace IPCE.Desktop.Localization;

public sealed class LocalizedMessageFormatter(
    ILocalizationService localization)
{
    private readonly ILocalizationService _localization = localization ??
        throw new ArgumentNullException(nameof(localization));

    public string Format(string key, params object?[] arguments) =>
        _localization.Format(key, arguments);
}
