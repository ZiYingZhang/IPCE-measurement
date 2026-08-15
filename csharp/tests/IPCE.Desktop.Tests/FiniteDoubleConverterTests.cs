using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IPCE.Desktop.Input;
using IPCE.Desktop.Localization;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FiniteDoubleConverterTests
{
    [TestMethod]
    [DataRow("0.36", "zh-CN", 0.36)]
    [DataRow(".36", "zh-CN", 0.36)]
    [DataRow("0,36", "de-DE", 0.36)]
    [DataRow("-2.5e-6", "zh-CN", -2.5e-6)]
    public void ConvertBack_AcceptsScientificDecimalInput(
        string text,
        string cultureName,
        double expected)
    {
        var converter = new FiniteDoubleConverter();

        object result = converter.ConvertBack(
            text,
            typeof(double),
            null!,
            CultureInfo.GetCultureInfo(cultureName));

        Assert.AreEqual(expected, Assert.IsInstanceOfType<double>(result));
    }

    [TestMethod]
    [DataRow("NaN", "en-US")]
    [DataRow("Infinity", "en-US")]
    [DataRow("1,000.2", "en-US")]
    [DataRow("", "zh-CN")]
    [DataRow(" ", "zh-CN")]
    public void ConvertBack_RejectsNonFiniteGroupedOrBlankInput(
        string text,
        string cultureName)
    {
        var converter = new FiniteDoubleConverter();

        ValidationException exception =
            Assert.Throws<ValidationException>(() =>
                converter.ConvertBack(
                    text,
                    typeof(double),
                    null!,
                    CultureInfo.GetCultureInfo(cultureName)));

        Assert.AreEqual("请输入有限数值。", exception.Message);
    }

    [TestMethod]
    public void Convert_UsesRoundTripSafeInvariantFormatting()
    {
        var converter = new FiniteDoubleConverter();

        object result = converter.Convert(
            0.36d,
            typeof(string),
            null!,
            CultureInfo.GetCultureInfo("de-DE"));

        Assert.AreEqual("0.35999999999999999", result);
    }

    [TestMethod]
    public void ConvertBack_ValidationUsesSelectedLanguage()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ipce-converter-language-{Guid.NewGuid():N}.json");
        try
        {
            var localization = new LocalizationService(
                new LanguagePreferenceStore(path),
                CultureInfo.GetCultureInfo("en-US"));
            var converter = new FiniteDoubleConverter(localization);

            ValidationException exception =
                Assert.Throws<ValidationException>(() =>
                    converter.ConvertBack(
                        "NaN",
                        typeof(double),
                        null!,
                        CultureInfo.GetCultureInfo("en-US")));

            Assert.AreEqual(
                "Enter a finite numeric value.",
                exception.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void RealWpfBinding_CommitsDecimalWithoutValidationError()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel =
                    new SiliconWorkflowViewModel(new SessionState());
                var textBox = new TextBox
                {
                    DataContext = viewModel,
                };
                BindingOperations.SetBinding(
                    textBox,
                    TextBox.TextProperty,
                    new Binding(
                        nameof(
                            SiliconWorkflowViewModel
                                .AreaSquareCentimetres))
                    {
                        Converter = new FiniteDoubleConverter(),
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger =
                            UpdateSourceTrigger.Explicit,
                        ValidatesOnExceptions = true,
                        NotifyOnValidationError = true,
                    });

                textBox.Text = "0.36";
                textBox
                    .GetBindingExpression(TextBox.TextProperty)!
                    .UpdateSource();

                Assert.AreEqual(
                    0.36d,
                    viewModel.AreaSquareCentimetres);
                Assert.IsFalse(Validation.GetHasError(textBox));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(10)),
            "WPF binding test thread did not finish.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"WPF decimal binding failed: {failure}",
                failure);
        }
    }
}
