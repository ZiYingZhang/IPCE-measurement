using System.Globalization;
using System.IO;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalizedMessageFormatterTests
{
    [TestMethod]
    public void Format_UsesSelectedLanguageAndCultureForDisplayedValues()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-format-{Guid.NewGuid():N}.json");
        try
        {
            var service = new LocalizationService(
                new LanguagePreferenceStore(path),
                CultureInfo.GetCultureInfo("en-US"));
            var formatter = new LocalizedMessageFormatter(service);

            Assert.AreEqual(
                "Imported 2 spectrum points",
                formatter.Format("Status.SpectrumImported", 2));

            service.CurrentLanguage = AppLanguage.SimplifiedChinese;

            Assert.AreEqual(
                "已导入 2 个光谱点",
                formatter.Format("Status.SpectrumImported", 2));
            Assert.AreEqual(
                "积分完成：12.5 mA cm⁻²",
                formatter.Format("Status.IntegrationCompleted", 12.5));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
