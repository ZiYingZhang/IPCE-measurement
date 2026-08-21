using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using IPCE.Core.Domain;
using IPCE.Desktop.Localization;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.Desktop.Views;
using IPCE.Desktop.Views.Plots;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LanguageSwitchingTests
{
    [TestMethod]
    public void MainWindow_LiveSwitchPreservesStateAndKeepsPlotCanvasEnglishArial()
    {
        Exception? failure = null;
        string preferencePath = Path.Combine(
            Path.GetTempPath(),
            $"ipce-window-language-{Guid.NewGuid():N}.json");
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                var localization = new LocalizationService(
                    new LanguagePreferenceStore(preferencePath),
                    CultureInfo.GetCultureInfo("en-US"));
                var session = new SessionState();
                var external = new ExternalIpceData(
                    [
                        new IpceValue(500, 42),
                        new IpceValue(600, 43),
                    ],
                    "Wavelength (nm)",
                    "IPCE (%)");
                session.SetExternalIpce(external);
                var viewModel = new MainViewModel(
                    session,
                    localization: localization);
                window = new MainWindow(
                    viewModel,
                    loadStartupDefaults: false);
                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);

                Assert.AreEqual(
                    "IPCE Measurement and Spectrum Integration",
                    window.Title);
                Assert.AreEqual("Arial", window.FontFamily.Source);
                Assert.AreEqual("Ready", viewModel.StartupStatusMessage);
                var selector = Assert.IsInstanceOfType<ComboBox>(
                    window.FindName("LanguageSelector"));
                Assert.AreEqual(2, selector.Items.Count);
                Assert.AreEqual(
                    "English",
                    Assert.IsInstanceOfType<ComboBoxItem>(
                        selector.Items[0]).Content);
                Assert.AreEqual(
                    "中文",
                    Assert.IsInstanceOfType<ComboBoxItem>(
                        selector.Items[1]).Content);
                Assert.AreEqual(
                    AppLanguage.English,
                    selector.SelectedValue);
                var results = Assert.IsInstanceOfType<ResultTabs>(
                    window.FindName("ResultsPanel"));
                var schedule = Assert.IsInstanceOfType<SchedulePlotView>(
                    results.FindName("SchedulePlotView"));
                var powerDensity =
                    Assert.IsInstanceOfType<PowerDensityPlotView>(
                        results.FindName("PowerDensityPlotView"));
                var ipcePlot = Assert.IsInstanceOfType<IpcePlotView>(
                    results.FindName("IpcePlotView"));
                var spectrum =
                    Assert.IsInstanceOfType<SpectrumIntegrationPlotView>(
                        results.FindName("SpectrumIntegrationPlotView"));
                var hover = Assert.IsInstanceOfType<TextBlock>(
                    schedule.FindName("HoverText"));
                var toolbar = Assert.IsInstanceOfType<PlotToolbar>(
                    schedule.FindName("Toolbar"));
                Assert.AreEqual("Arial", hover.FontFamily.Source);
                Assert.AreEqual("Arial", toolbar.FontFamily.Source);
                Assert.AreEqual(
                    "Wavelength–time schedule preview",
                    schedule.InteractionController.Model?.Title);

                object originalDataContext = window.DataContext;
                SessionState originalSession = viewModel.Session;
                ExternalIpceData? originalExternal =
                    viewModel.Session.ExternalIpce;
                Assert.IsNotNull(originalExternal);

                localization.CurrentLanguage =
                    AppLanguage.SimplifiedChinese;
                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);

                Assert.AreEqual("IPCE 测量与光谱积分", window.Title);
                Assert.AreEqual(
                    "Microsoft YaHei",
                    window.FontFamily.Source);
                Assert.AreEqual(
                    "Microsoft YaHei",
                    hover.FontFamily.Source);
                Assert.AreEqual(
                    "Microsoft YaHei",
                    toolbar.FontFamily.Source);
                Assert.AreEqual("就绪", viewModel.StartupStatusMessage);
                Assert.AreEqual(
                    AppLanguage.SimplifiedChinese,
                    selector.SelectedValue);
                Assert.AreEqual(
                    "Wavelength–time schedule preview",
                    schedule.InteractionController.Model?.Title);
                Assert.AreEqual(
                    "Arial",
                    schedule.PlotControl.Plot.Axes.Title.Label.FontName);
                Assert.AreEqual(
                    "Monochromatic incident power density",
                    powerDensity.InteractionController.Model?.Title);
                Assert.AreEqual(
                    "Power density (µW·cm⁻²)",
                    powerDensity.InteractionController.Model?.YLabel);
                Assert.AreEqual(
                    "Arial",
                    powerDensity.PlotControl.Plot.Axes.Left.Label.FontName);
                Assert.AreEqual(
                    "IPCE comparison",
                    ipcePlot.InteractionController.Model?.Title);
                Assert.AreEqual(
                    "Arial",
                    ipcePlot.PlotControl.Plot.Axes.Title.Label.FontName);
                Assert.AreEqual(
                    "Solar spectrum",
                    spectrum.IrradiancePlotControl.Plot.Axes.Title.Label.Text);
                Assert.AreEqual(
                    "Irradiance (W·m⁻²·nm⁻¹)",
                    spectrum.IrradiancePlotControl.Plot.Axes.Left.Label.Text);
                Assert.AreEqual(
                    "Arial",
                    spectrum.IrradiancePlotControl.Plot.Axes.Left.Label.FontName);
                Assert.AreSame(originalDataContext, window.DataContext);
                Assert.AreSame(originalSession, viewModel.Session);
                Assert.AreSame(
                    originalExternal,
                    viewModel.Session.ExternalIpce);
                Assert.AreEqual(42d,
                    viewModel.Session.ExternalIpce!.Points[0].IpcePercent);

                localization.CurrentLanguage = AppLanguage.English;
                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);

                Assert.AreEqual(
                    "IPCE Measurement and Spectrum Integration",
                    window.Title);
                Assert.AreEqual("Arial", window.FontFamily.Source);
                Assert.AreEqual("Arial", hover.FontFamily.Source);
                Assert.AreEqual("Arial", toolbar.FontFamily.Source);
                Assert.AreEqual("Ready", viewModel.StartupStatusMessage);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(10)),
            "Language-switch WPF thread did not finish.");
        if (File.Exists(preferencePath)) File.Delete(preferencePath);
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"Live language switching failed: {failure}",
                failure);
        }
    }
}
