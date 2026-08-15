using System.Text;
using IPCE.IO.Startup;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class StartupDataResolverTests
{
    [TestMethod]
    public void RepositoryLayout_SeparatesDefaultsAndExamples()
    {
        Assert.AreEqual(
            Path.Combine(TestPaths.RepositoryRoot, "data", "defaults"),
            TestPaths.DefaultsRoot);
        Assert.AreEqual(
            Path.Combine(TestPaths.RepositoryRoot, "data", "examples"),
            TestPaths.ExamplesRoot);
        Assert.IsTrue(Directory.Exists(TestPaths.DefaultsRoot));
        Assert.IsTrue(Directory.Exists(TestPaths.ExamplesRoot));
    }

    [TestMethod]
    public void RepositoryLayout_UsesNormalizedCSharpDirectory()
    {
        string normalizedRoot = Path.Combine(TestPaths.RepositoryRoot, "csharp");

        Assert.IsTrue(File.Exists(Path.Combine(normalizedRoot, "IPCE.slnx")));
        Assert.IsFalse(Directory.Exists(Path.Combine(
            TestPaths.RepositoryRoot,
            "csharp APP")));
    }

    [TestMethod]
    public void Defaults_MatchMeasurementAndIntegrationWorkflow()
    {
        DefaultConfiguration defaults = DefaultConfiguration.Current;

        Assert.AreEqual(
            "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx",
            defaults.CalibrationFileName);
        Assert.AreEqual("标准太阳能光谱数据.xls", defaults.SpectrumFileName);
        Assert.IsTrue(defaults.SubtractDark);
        Assert.AreEqual(0.1, defaults.SiliconDarkStartSeconds);
        Assert.AreEqual(10, defaults.SiliconDarkEndSeconds);
        Assert.AreEqual(50, defaults.SampleDarkStartSeconds);
        Assert.AreEqual(60, defaults.SampleDarkEndSeconds);
        Assert.AreEqual(0.36, defaults.SiliconAreaSquareCentimetres);
        Assert.AreEqual(1, defaults.SampleAreaSquareCentimetres);
        Assert.AreEqual(300, defaults.WavelengthStartNanometres);
        Assert.AreEqual(1100, defaults.WavelengthEndNanometres);
        Assert.AreEqual(5, defaults.WavelengthStepNanometres);
        Assert.AreEqual(8, defaults.NominalDelaySeconds);
        Assert.AreEqual(4, defaults.PostConfirmationAverageSeconds);
        Assert.AreEqual(300, defaults.IntegrationStartNanometres);
        Assert.AreEqual(1100, defaults.IntegrationEndNanometres);
        Assert.AreEqual("Spectra", defaults.SpectrumWorksheet);
        Assert.AreEqual(1, defaults.SpectrumWavelengthColumn);
        Assert.AreEqual(3, defaults.SpectrumIrradianceColumn);
    }

    [TestMethod]
    public void ExactApplicationDirectoryFile_OverridesEmbeddedData()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string fileName = DefaultConfiguration.Current.SiliconAnchorFileName;
            byte[] expected = Encoding.UTF8.GetBytes("override");
            File.WriteAllBytes(Path.Combine(directory, fileName), expected);

            ResolvedStartupData result =
                StartupDataResolver.Resolve(fileName, directory);

            Assert.IsFalse(result.IsEmbedded);
            CollectionAssert.AreEqual(expected, result.Content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingApplicationDirectoryFile_FallsBackToEmbeddedData()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"ipce-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string fileName = DefaultConfiguration.Current.SiliconAnchorFileName;

            ResolvedStartupData result =
                StartupDataResolver.Resolve(fileName, directory);

            Assert.IsTrue(result.IsEmbedded);
            Assert.IsTrue(result.Content.Length > 0);
            StringAssert.Contains(
                Encoding.UTF8.GetString(result.Content),
                "310");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
