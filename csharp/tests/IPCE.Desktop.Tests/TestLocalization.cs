using System.Globalization;
using System.IO;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Tests;

internal static class TestLocalization
{
    public static ILocalizationService Chinese() => Create("zh-CN");

    public static ILocalizationService English() => Create("en-US");

    private static ILocalizationService Create(string cultureName) =>
        new LocalizationService(
            new LanguagePreferenceStore(Path.Combine(
                Path.GetTempPath(),
                $"ipce-test-localization-{Guid.NewGuid():N}",
                "settings.json")),
            CultureInfo.GetCultureInfo(cultureName));
}
