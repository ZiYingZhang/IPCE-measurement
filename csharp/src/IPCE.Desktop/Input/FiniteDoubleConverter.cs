using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Data;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Input;

public sealed class FiniteDoubleConverter : IValueConverter
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowExponent;
    private readonly ILocalizationService _localization;

    public FiniteDoubleConverter()
        : this(LocalizationService.Current)
    {
    }

    public FiniteDoubleConverter(ILocalizationService localization) =>
        _localization = localization ??
            throw new ArgumentNullException(nameof(localization));

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not double number || !double.IsFinite(number))
        {
            throw InvalidValue();
        }

        return number.ToString("G17", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        string? text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw InvalidValue();
        }

        if (TryParseFinite(text, culture, out double number) ||
            (!Equals(culture, CultureInfo.InvariantCulture) &&
                TryParseFinite(
                    text,
                    CultureInfo.InvariantCulture,
                    out number)))
        {
            return number;
        }

        throw InvalidValue();
    }

    private static bool TryParseFinite(
        string text,
        CultureInfo culture,
        out double number) =>
        double.TryParse(text, Styles, culture, out number) &&
        double.IsFinite(number);

    private ValidationException InvalidValue() =>
        new(_localization["Validation.FiniteNumber"]);
}
