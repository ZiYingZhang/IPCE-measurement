using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.IO.Export;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed partial class LocalizationCompletenessTests
{
    private static readonly string[] StableErrorCodes =
    [
        "IPCE:AnchorFileNotFound", "IPCE:CalibrationRange",
        "IPCE:DarkRangeOutsideTrace", "IPCE:DuplicateAnchors",
        "IPCE:DuplicateExportNames", "IPCE:EmptyWavelengths",
        "IPCE:EmptyWindow", "IPCE:ExportFailed",
        "IPCE:ExportVerificationFailed", "IPCE:FileNotFound",
        "IPCE:InsufficientCoverage", "IPCE:InsufficientDarkData",
        "IPCE:IntegrationCoverage", "IPCE:IntegrationInterpolation",
        "IPCE:InterpolationCoverage", "IPCE:InvalidAnchorFile",
        "IPCE:InvalidArea", "IPCE:InvalidAxisLimits",
        "IPCE:InvalidDarkRange", "IPCE:InvalidExportTable",
        "IPCE:InvalidExternalIPCE", "IPCE:InvalidHitTestRadius",
        "IPCE:InvalidIntegrationGrid", "IPCE:InvalidIntegrationRange",
        "IPCE:InvalidInterpolatedPowerDensity",
        "IPCE:InvalidInterpolationInput", "IPCE:InvalidIPCEResult",
        "IPCE:InvalidLogAxis", "IPCE:InvalidPlotCoordinate",
        "IPCE:InvalidPlotData", "IPCE:InvalidPlotSeries",
        "IPCE:InvalidPowerDensity", "IPCE:InvalidPreview",
        "IPCE:InvalidReference", "IPCE:InvalidResponsivity",
        "IPCE:InvalidSchedule", "IPCE:InvalidSiliconResult",
        "IPCE:InvalidSpectrum", "IPCE:InvalidSpectrumSelection",
        "IPCE:InvalidTrace", "IPCE:InvalidTraceOverlay",
        "IPCE:InvalidViewportPolicy", "IPCE:InvalidWavelengthGrid",
        "IPCE:MissingAnchors", "IPCE:MissingCalculatedIPCE",
        "IPCE:MissingCalibration", "IPCE:MissingExternalIPCE",
        "IPCE:MissingPowerDensity", "IPCE:MissingSampleTrace",
        "IPCE:MissingSiliconTrace", "IPCE:MissingSpectrum",
        "IPCE:NoCurrentExportSelection", "IPCE:NoExportSelection",
        "IPCE:NonMonotonicSchedule", "IPCE:NoNumericSpectrumColumns",
        "IPCE:PowerInterpolationRange", "IPCE:ReferenceImportFailed",
        "IPCE:SpectrumColumnMissing", "IPCE:SpectrumImportFailed",
        "IPCE:SpectrumSheetNotFound", "IPCE:StaleResult",
        "IPCE:StartupDataNotFound", "IPCE:TableImportFailed",
        "IPCE:TraceUnitsRequired", "IPCE:UnknownAlignmentMode",
        "IPCE:UnknownIPCESource", "IPCE:UnsupportedCurrentUnit",
        "IPCE:UnsupportedExternalIPCE", "IPCE:UnsupportedTimeUnit",
        "IPCE:WorkbookImportFailed", "IPCE:WorkbookSheetNotFound",
    ];

    [TestMethod]
    public void StableErrorCodes_HaveSpecificMessagesInBothLanguages()
    {
        using var fixture = new LocalizationFixture();
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            fixture.Service.CurrentLanguage = language;
            var localizer = new UserMessageLocalizer(fixture.Service);
            foreach (string code in StableErrorCodes)
            {
                string message = localizer.Localize(
                    new IpceException(code, "raw diagnostic"));
                Assert.IsFalse(
                    message.Contains(code, StringComparison.Ordinal),
                    $"Missing specific {language} message for {code}.");
                Assert.AreNotEqual("raw diagnostic", message);
                Assert.IsFalse(string.IsNullOrWhiteSpace(message));
            }
        }
    }

    [TestMethod]
    public void EnglishWindow_HasNoChineseUserFacingTextExceptLanguageName()
    {
        Exception? failure = null;
        using var fixture = new LocalizationFixture();
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                var main = new MainViewModel(
                    new SessionState(),
                    localization: fixture.Service);
                window = new MainWindow(main, false);
                window.Dispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.DataBind);

                string[] unexpected = EnumerateUserFacingStrings(window)
                    .Where(text => text != "中文" && HanRegex().IsMatch(text))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                Assert.AreEqual(
                    0,
                    unexpected.Length,
                    $"Unlocalized English-window text: {string.Join(" | ", unexpected)}");
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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"English UI completeness failed: {failure}",
                failure);
        }
    }

    [TestMethod]
    public void LanguageSwitch_DoesNotChangeExportTablesOrValues()
    {
        using var fixture = new LocalizationFixture();
        var session = new SessionState();
        session.SetExternalIpce(new ExternalIpceData(
        [
            new IpceValue(400, 25),
            new IpceValue(500, 125),
        ],
        "Wavelength (nm)",
        "IPCE (%)"));
        var main = new MainViewModel(
            session,
            localization: fixture.Service);
        main.Spectrum.IncludePowerDensityExport = false;
        main.Spectrum.IncludeCalculatedIpceExport = false;
        main.Spectrum.IncludeIntegrationExport = false;

        IReadOnlyList<ExportTable> english =
            main.Spectrum.BuildSelectedExportTables();
        fixture.Service.CurrentLanguage =
            AppLanguage.SimplifiedChinese;
        IReadOnlyList<ExportTable> chinese =
            main.Spectrum.BuildSelectedExportTables();

        Assert.AreEqual(english.Count, chinese.Count);
        for (int tableIndex = 0;
             tableIndex < english.Count;
             tableIndex++)
        {
            Assert.AreEqual(
                english[tableIndex].Name,
                chinese[tableIndex].Name);
            CollectionAssert.AreEqual(
                english[tableIndex].Columns
                    .Select(column => column.Name).ToArray(),
                chinese[tableIndex].Columns
                    .Select(column => column.Name).ToArray());
            Assert.AreEqual(
                english[tableIndex].RowCount,
                chinese[tableIndex].RowCount);
            for (int column = 0;
                 column < english[tableIndex].Columns.Count;
                 column++)
            {
                CollectionAssert.AreEqual(
                    english[tableIndex].Columns[column].Values.ToArray(),
                    chinese[tableIndex].Columns[column].Values.ToArray());
            }
        }
    }

    private static IEnumerable<string> EnumerateUserFacingStrings(
        DependencyObject root)
    {
        if (root is Window window && !string.IsNullOrWhiteSpace(window.Title))
        {
            yield return window.Title;
        }
        if (root is TextBlock textBlock &&
            !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            yield return textBlock.Text;
        }
        if (root is ContentControl contentControl &&
            contentControl.Content is string content &&
            !string.IsNullOrWhiteSpace(content))
        {
            yield return content;
        }
        if (root is HeaderedContentControl headered &&
            headered.Header is string header &&
            !string.IsNullOrWhiteSpace(header))
        {
            yield return header;
        }
        if (root is ItemsControl items)
        {
            foreach (object item in items.Items)
            {
                if (item is string itemText &&
                    !string.IsNullOrWhiteSpace(itemText))
                {
                    yield return itemText;
                }
                else if (item is DependencyObject dependencyObject)
                {
                    foreach (string nested in
                             EnumerateUserFacingStrings(dependencyObject))
                    {
                        yield return nested;
                    }
                }
            }
        }

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dependencyObject)
            {
                foreach (string nested in
                         EnumerateUserFacingStrings(dependencyObject))
                {
                    yield return nested;
                }
            }
        }
    }

    [GeneratedRegex("[\\p{IsCJKUnifiedIdeographs}]")]
    private static partial Regex HanRegex();

    private sealed class LocalizationFixture : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-completeness-{Guid.NewGuid():N}.json");

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
