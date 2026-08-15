using System.Collections;
using System.Globalization;
using System.IO;
using System.Resources;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalizationServiceTests
{
    [TestMethod]
    [DataRow("zh-CN", AppLanguage.SimplifiedChinese)]
    [DataRow("zh-TW", AppLanguage.SimplifiedChinese)]
    [DataRow("zh-HK", AppLanguage.SimplifiedChinese)]
    [DataRow("en-US", AppLanguage.English)]
    [DataRow("de-DE", AppLanguage.English)]
    public void NoPreference_SelectsLanguageFromSystemCulture(
        string cultureName,
        AppLanguage expected)
    {
        using var temporary = new TemporaryDirectory();
        var service = new LocalizationService(
            new LanguagePreferenceStore(temporary.File("settings.json")),
            CultureInfo.GetCultureInfo(cultureName));

        Assert.AreEqual(expected, service.CurrentLanguage);
    }

    [TestMethod]
    public void ValidPreference_OverridesSystemCulture()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LanguagePreferenceStore(
            temporary.File("settings.json"));
        store.Save("en-US");

        var service = new LocalizationService(
            store,
            CultureInfo.GetCultureInfo("zh-CN"));

        Assert.AreEqual(AppLanguage.English, service.CurrentLanguage);
        Assert.AreEqual("IPCE Measurement and Spectrum Integration",
            service["App.Title"]);
    }

    [TestMethod]
    public void UnsupportedPreference_RecoversToSystemLanguage()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(
            temporary.File("settings.json"),
            "{\"language\":\"fr-FR\"}");

        var service = new LocalizationService(
            new LanguagePreferenceStore(temporary.File("settings.json")),
            CultureInfo.GetCultureInfo("zh-CN"));

        Assert.AreEqual(
            AppLanguage.SimplifiedChinese,
            service.CurrentLanguage);
    }

    [TestMethod]
    public void LanguageChange_NotifiesIndexerAndPersistsCulture()
    {
        using var temporary = new TemporaryDirectory();
        var store = new LanguagePreferenceStore(
            temporary.File("settings.json"));
        var service = new LocalizationService(
            store,
            CultureInfo.GetCultureInfo("en-US"));
        var changed = new List<string?>();
        int languageChanged = 0;
        service.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        service.LanguageChanged += (_, _) => languageChanged++;

        service.CurrentLanguage = AppLanguage.SimplifiedChinese;

        CollectionAssert.Contains(changed, nameof(service.CurrentLanguage));
        CollectionAssert.Contains(changed, nameof(service.CurrentCultureName));
        CollectionAssert.Contains(changed, "Item[]");
        Assert.AreEqual(1, languageChanged);
        Assert.AreEqual("zh-CN", store.Load());
        Assert.AreEqual("IPCE 测量与光谱积分", service["App.Title"]);
    }

    [TestMethod]
    public void UnsupportedResourceCulture_FallsBackToNeutralEnglish()
    {
        using var temporary = new TemporaryDirectory();
        var service = new LocalizationService(
            new LanguagePreferenceStore(temporary.File("settings.json")),
            CultureInfo.GetCultureInfo("fr-FR"));

        Assert.AreEqual("Ready", service["Common.Ready"]);
        Assert.AreEqual("[Missing.Key]", service["Missing.Key"]);
    }

    [TestMethod]
    public void ResourceCatalogs_HaveIdenticalNonEmptyKeys()
    {
        var manager = new ResourceManager(
            "IPCE.Desktop.Resources.Strings",
            typeof(LocalizationService).Assembly);
        Dictionary<string, string> english = ReadCatalog(
            manager,
            CultureInfo.InvariantCulture);
        Dictionary<string, string> chinese = ReadCatalog(
            manager,
            CultureInfo.GetCultureInfo("zh-CN"));

        CollectionAssert.AreEquivalent(
            english.Keys.ToArray(),
            chinese.Keys.ToArray());
        Assert.IsTrue(english.Count >= 10);
        Assert.IsFalse(english.Values.Any(string.IsNullOrWhiteSpace));
        Assert.IsFalse(chinese.Values.Any(string.IsNullOrWhiteSpace));
    }

    private static Dictionary<string, string> ReadCatalog(
        ResourceManager manager,
        CultureInfo culture)
    {
        ResourceSet? candidate =
            manager.GetResourceSet(culture, true, false);
        Assert.IsNotNull(candidate);
        ResourceSet set = candidate;
        return set.Cast<DictionaryEntry>().ToDictionary(
            entry => Assert.IsInstanceOfType<string>(entry.Key),
            entry => Assert.IsInstanceOfType<string>(entry.Value));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-localization-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string File(string name) => Path.Combine(_path, name);

        public void Dispose() => Directory.Delete(_path, true);
    }
}
