using System.Globalization;
using System.IO;
using IPCE.Core.Domain;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.State;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BilingualPlotSurfaceTests
{
    [TestMethod]
    public void SpectrumModels_TranslateTextAndPreserveAllScientificValues()
    {
        using var fixture = new LocalizationFixture();
        SpectrumPoint[] spectrum =
        [
            new SpectrumPoint(400, 1.5),
            new SpectrumPoint(500, 2.5),
        ];
        IpceValue[] ipce =
        [
            new IpceValue(400, 25),
            new IpceValue(500, 50),
        ];

        SpectrumPlotModels english =
            ResultPlotModelBuilder.BuildSpectrumIntegration(
                spectrum,
                ipce,
                null,
                420,
                480,
                fixture.Service);

        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;
        SpectrumPlotModels chinese =
            ResultPlotModelBuilder.BuildSpectrumIntegration(
                spectrum,
                ipce,
                null,
                420,
                480,
                fixture.Service);

        Assert.AreEqual("Solar spectrum", english.Irradiance.Title);
        Assert.AreEqual("太阳光谱", chinese.Irradiance.Title);
        Assert.AreEqual("Wavelength (nm)", english.Irradiance.XLabel);
        Assert.AreEqual("波长 (nm)", chinese.Irradiance.XLabel);
        Assert.AreEqual(
            "Irradiance (W·m^-2·nm^-1)",
            english.Irradiance.YLabel);
        Assert.AreEqual(
            "辐照度 (W/(m²·nm))",
            chinese.Irradiance.YLabel);
        Assert.AreEqual(
            "Cumulative Jsc (mA·cm^-2)",
            english.Cumulative.YLabel);
        Assert.AreEqual(
            "累计 Jsc (mA/cm²)",
            chinese.Cumulative.YLabel);
        fixture.Service.CurrentLanguage = AppLanguage.English;
        Assert.AreEqual(
            "Power density (µW·cm^-2)",
            fixture.Service["Plot.PowerDensityAxis"]);
        fixture.Service.CurrentLanguage = AppLanguage.SimplifiedChinese;
        Assert.AreEqual(
            "功率密度 (µW/cm²)",
            fixture.Service["Plot.PowerDensityAxis"]);
        AssertNumericParity(english.Irradiance, chinese.Irradiance);
        AssertNumericParity(english.SelectedIpce, chinese.SelectedIpce);
        AssertNumericParity(english.Cumulative, chinese.Cumulative);
    }

    [TestMethod]
    public void TraceOverlay_TranslatesStructuredHoverWithoutChangingWindow()
    {
        using var fixture = new LocalizationFixture();
        var preview = new SchedulePreview(
            [new SchedulePoint(500, 0, 0, 10, "fixed-delay")],
            [],
            new CoveragePreview(0, 10, 0, 10, true, "covered"));
        TraceMeanResult[] means =
            [new TraceMeanResult(500, -1e-5, 9)];

        PlotIntervalMarker english = TraceOverlayBuilder.BuildMeans(
            preview,
            4,
            means,
            fixture.Service)[0];
        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;
        PlotIntervalMarker chinese = TraceOverlayBuilder.BuildMeans(
            preview,
            4,
            means,
            fixture.Service)[0];

        Assert.AreEqual("Mean current", english.Label);
        StringAssert.Contains(english.HoverDetails, "Wavelength: 500 nm");
        Assert.AreEqual("平均电流", chinese.Label);
        StringAssert.Contains(chinese.HoverDetails, "波长：500 nm");
        Assert.AreEqual(english.MinimumX, chinese.MinimumX);
        Assert.AreEqual(english.MaximumX, chinese.MaximumX);
        Assert.AreEqual(english.Y, chinese.Y);
        Assert.AreEqual(english.ColorHex, chinese.ColorHex);
    }

    [TestMethod]
    public void TraceLayerIdentity_DoesNotDependOnTranslatedLabel()
    {
        using var fixture = new LocalizationFixture();
        var trace = new TraceData(
            [0d, 1d],
            [0d, 1e-6],
            TraceMetadata.Unknown);

        PlotModel english = ResultPlotModelBuilder.BuildTrace(
            "Silicon i-t",
            trace,
            [],
            false,
            0,
            0,
            null,
            1,
            [],
            new ResultStatus(ResultFreshness.Missing, ""),
            fixture.Service);
        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;
        PlotModel chinese = ResultPlotModelBuilder.BuildTrace(
            "硅 i-t",
            trace,
            [],
            false,
            0,
            0,
            null,
            1,
            [],
            new ResultStatus(ResultFreshness.Missing, ""),
            fixture.Service);

        Assert.AreEqual("raw-trace", english.Series[0].Id);
        Assert.AreEqual(english.Series[0].Id, chinese.Series[0].Id);
        Assert.AreEqual("Raw trace", english.Series[0].Label);
        Assert.AreEqual("原始轨迹", chinese.Series[0].Label);
        CollectionAssert.AreEqual(
            english.Series[0].X.ToArray(),
            chinese.Series[0].X.ToArray());
        CollectionAssert.AreEqual(
            english.Series[0].Y.ToArray(),
            chinese.Series[0].Y.ToArray());
    }

    [TestMethod]
    public void CoveragePreview_TranslatesMessageAndPreservesBounds()
    {
        using var fixture = new LocalizationFixture();
        IpceValue[] ipce =
        [
            new IpceValue(400, 25),
            new IpceValue(500, 50),
        ];
        SpectrumPoint[] spectrum =
        [
            new SpectrumPoint(420, 1),
            new SpectrumPoint(520, 1),
        ];

        CoveragePreview english =
            WorkflowPreviewBuilder.BuildIntegrationCoverage(
                ipce,
                spectrum,
                410,
                510,
                fixture.Service);
        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;
        CoveragePreview chinese =
            WorkflowPreviewBuilder.BuildIntegrationCoverage(
                ipce,
                spectrum,
                410,
                510,
                fixture.Service);

        StringAssert.Contains(english.Message, "Data range 420–500 nm");
        StringAssert.Contains(chinese.Message, "数据范围 420–500 nm");
        Assert.AreEqual(english.DataMinimum, chinese.DataMinimum);
        Assert.AreEqual(english.DataMaximum, chinese.DataMaximum);
        Assert.AreEqual(english.RequestedMinimum, chinese.RequestedMinimum);
        Assert.AreEqual(english.RequestedMaximum, chinese.RequestedMaximum);
        Assert.AreEqual(english.IsWithinCoverage, chinese.IsWithinCoverage);
    }

    private static void AssertNumericParity(
        PlotModel expected,
        PlotModel actual)
    {
        Assert.AreEqual(expected.Series.Count, actual.Series.Count);
        Assert.AreEqual(expected.Bands.Count, actual.Bands.Count);
        for (int index = 0; index < expected.Series.Count; index++)
        {
            CollectionAssert.AreEqual(
                expected.Series[index].X.ToArray(),
                actual.Series[index].X.ToArray());
            CollectionAssert.AreEqual(
                expected.Series[index].Y.ToArray(),
                actual.Series[index].Y.ToArray());
            CollectionAssert.AreEqual(
                expected.Series[index].YErrors?.ToArray(),
                actual.Series[index].YErrors?.ToArray());
            Assert.AreEqual(
                expected.Series[index].ColorHex,
                actual.Series[index].ColorHex);
        }

        for (int index = 0; index < expected.Bands.Count; index++)
        {
            Assert.AreEqual(
                expected.Bands[index].MinimumX,
                actual.Bands[index].MinimumX);
            Assert.AreEqual(
                expected.Bands[index].MaximumX,
                actual.Bands[index].MaximumX);
            Assert.AreEqual(
                expected.Bands[index].ColorHex,
                actual.Bands[index].ColorHex);
            Assert.AreEqual(
                expected.Bands[index].Opacity,
                actual.Bands[index].Opacity);
        }
    }

    private sealed class LocalizationFixture : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-plot-language-{Guid.NewGuid():N}.json");

        public LocalizationFixture() => Service = new LocalizationService(
            new LanguagePreferenceStore(_path),
            CultureInfo.GetCultureInfo("en-US"));

        public LocalizationService Service { get; }

        public void Dispose()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
