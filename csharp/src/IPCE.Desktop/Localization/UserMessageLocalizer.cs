using System.IO;
using IPCE.Core.Errors;

namespace IPCE.Desktop.Localization;

public sealed class UserMessageLocalizer(
    ILocalizationService localization)
{
    private readonly ILocalizationService _localization = localization ??
        throw new ArgumentNullException(nameof(localization));

    public string Localize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is IpceException ipceException)
        {
            string key = $"Error.{ipceException.Code.Replace(':', '.')}";
            string message = _localization[key];
            return message == $"[{key}]"
                ? _localization.Format(
                    "Error.GenericDomain",
                    ipceException.Code)
                : message;
        }

        return exception switch
        {
            UnauthorizedAccessException => _localization["Error.AccessDenied"],
            IOException => _localization["Error.FileOperation"],
            _ => _localization["Error.GenericUnexpected"],
        };
    }
}
