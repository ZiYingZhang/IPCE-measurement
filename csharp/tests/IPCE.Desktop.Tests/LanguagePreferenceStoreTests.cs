using System.IO;
using IPCE.Desktop.Localization;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class LanguagePreferenceStoreTests
{
    [TestMethod]
    public void SaveThenLoad_RoundTripsSupportedCulture()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "nested", "settings.json");
            var store = new LanguagePreferenceStore(path);

            store.Save("zh-CN");

            Assert.AreEqual("zh-CN", store.Load());
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("{\"language\":42}")]
    [DataRow("{\"language\":\"fr-FR\"}")]
    public void Load_InvalidContent_ReturnsNull(string content)
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, content);
            var store = new LanguagePreferenceStore(path);

            Assert.IsNull(store.Load());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void InaccessiblePath_DoesNotThrow()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var store = new LanguagePreferenceStore(directory);

            Assert.IsNull(store.Load());
            store.Save("en-US");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-language-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
