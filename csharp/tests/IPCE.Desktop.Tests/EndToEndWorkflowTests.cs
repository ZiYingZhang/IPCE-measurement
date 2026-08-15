using System.IO;
using System.Text;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;
using IPCE.IO.Export;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class EndToEndWorkflowTests
{
    private const double HcOverQElectronVoltNanometres =
        1239.8419843320026;

    [TestMethod]
    public async Task StartupDefaults_Calculate161PositivePowerDensityPoints()
    {
        var viewModel = new MainViewModel();
        await viewModel.LoadStartupDefaultsAsync();

        Assert.IsTrue(
            viewModel.Silicon.CalculatePowerDensityCommand.CanExecute(null));
        IReadOnlyList<PowerDensityPoint> result =
            viewModel.Silicon.CalculatePowerDensity();

        Assert.AreEqual(161, result.Count);
        Assert.IsTrue(result.All(point =>
            point.IncidentPowerDensityWattsPerSquareCentimetre > 0));
        Assert.AreSame(result, viewModel.Session.PowerDensity);
    }

    [TestMethod]
    public void SyntheticSampleTrace_CalculatesTwentyFiftyEightyPercent()
    {
        double[] wavelengths = [400, 500, 600];
        double[] powerDensities = [10e-6, 15e-6, 12e-6];
        double[] expectedPercent = [20, 50, 80];
        const double sampleArea = 0.75;
        var session = new SessionState();
        session.SetPowerDensity(wavelengths
            .Select((wavelength, index) =>
                PowerPoint(wavelength, powerDensities[index]))
            .ToArray());
        session.SetSampleTrace(CreateFixedDelayTrace(
            wavelengths.Select((wavelength, index) =>
            {
                double fraction = expectedPercent[index] / 100;
                double density = powerDensities[index] * fraction *
                    wavelength / HcOverQElectronVoltNanometres;
                return density * sampleArea;
            }).ToArray()));
        var viewModel = new SampleWorkflowViewModel(session)
        {
            WavelengthStartNanometres = 400,
            WavelengthEndNanometres = 600,
            WavelengthStepNanometres = 100,
            AlignmentMode = AlignmentMode.FixedDelay,
            FixedStartTimeSeconds = 0,
            NominalDelaySeconds = 10,
            AveragingDurationSeconds = 1,
            SubtractDark = false,
            AreaSquareCentimetres = sampleArea,
        };

        Assert.IsTrue(viewModel.CalculateIpceCommand.CanExecute(null));
        IReadOnlyList<IpcePoint> result = viewModel.CalculateIpce();

        for (int index = 0; index < expectedPercent.Length; index++)
        {
            AssertClose(expectedPercent[index], result[index].IpcePercent);
        }
        Assert.AreSame(result, session.CalculatedIpce);
    }

    [TestMethod]
    public async Task EmptyMeasurementSession_ExternalPostprocessIntegratesAndExports()
    {
        using var files = new TemporaryDirectory();
        string externalPath = files.WriteText(
            "external.csv",
            "Wavelength (nm),IPCE (%)\n400,20\n500,50\n600,80\n");
        var session = new SessionState();
        session.SetSpectrum(CreateSpectrum());
        var viewModel = new SpectrumWorkflowViewModel(session)
        {
            IntegrationMinimumNanometres = 400,
            IntegrationMaximumNanometres = 600,
            IncludeExternalIpceExport = true,
            IncludeIntegrationExport = true,
        };

        await viewModel.ImportExternalIpceAsync(externalPath);
        viewModel.SelectedIpceSource = IpceSource.External;
        Assert.IsTrue(viewModel.IntegrateCommand.CanExecute(null));
        IntegrationResult result = viewModel.IntegrateSelectedSource();
        Assert.IsTrue(viewModel.ExportCommand.CanExecute(
            new ExportRequest(
                Path.Combine(files.Path, "postprocess.xlsx"),
                ExportFormat.Xlsx)));
        string outputPath = Path.Combine(files.Path, "postprocess.xlsx");
        IReadOnlyList<string> written = viewModel.ExportSelected(
            outputPath,
            ExportFormat.Xlsx);

        Assert.IsNull(session.SiliconTrace);
        Assert.IsNull(session.SampleTrace);
        Assert.IsNull(session.CalculatedIpce);
        Assert.IsTrue(
            result.Summary.IntegratedCurrentDensityMilliamperePerSquareCentimetre
            > 0);
        Assert.AreEqual(1, written.Count);
        Assert.IsTrue(File.Exists(written[0]));
    }

    [TestMethod]
    public async Task BothIpceSources_SourceSwitchRetainsEachResult()
    {
        using var files = new TemporaryDirectory();
        string externalPath = files.WriteText(
            "external.csv",
            "Wavelength (nm),IPCE (%)\n400,25\n500,55\n600,85\n");
        var session = new SessionState();
        IReadOnlyList<IpcePoint> calculated =
        [
            IpcePoint(400, 20),
            IpcePoint(500, 50),
            IpcePoint(600, 80),
        ];
        session.SetCalculatedIpce(calculated);
        IReadOnlyList<IpcePoint> retainedCalculated =
            session.CalculatedIpce!;
        var viewModel = new SpectrumWorkflowViewModel(session);
        await viewModel.ImportExternalIpceAsync(externalPath);
        ExternalIpceData external = session.ExternalIpce!;

        viewModel.SelectedIpceSource = IpceSource.External;
        viewModel.SelectedIpceSource = IpceSource.Calculated;

        Assert.AreSame(retainedCalculated, session.CalculatedIpce);
        Assert.AreSame(external, session.ExternalIpce);
        Assert.AreEqual(IpceSource.Calculated, session.SelectedIpceSource);
    }

    [TestMethod]
    public void ExportSelection_UsesExactPostprocessNamesInEveryFormat()
    {
        using var files = new TemporaryDirectory();
        var session = new SessionState();
        session.SetExternalIpce(new ExternalIpceData(
            [
                new IpceValue(400, 20),
                new IpceValue(500, 50),
                new IpceValue(600, 80),
            ],
            "Wavelength (nm)",
            "IPCE (%)"));
        session.SetSpectrum(CreateSpectrum());
        session.SelectIpceSource(IpceSource.External);
        session.Integrate(400, 600);
        var viewModel = new SpectrumWorkflowViewModel(session)
        {
            IncludeExternalIpceExport = true,
            IncludeIntegrationExport = true,
        };
        IReadOnlyList<ExportTable> tables =
            viewModel.BuildSelectedExportTables();

        CollectionAssert.AreEqual(
            new[]
            {
                "ExternalIPCE",
                "SpectrumSummary",
                "SpectrumCurve",
                "MeasurementSettings",
                "InputMetadata",
            },
            tables.Select(table => table.Name).ToArray());

        foreach ((ExportFormat format, string extension) in new[]
        {
            (ExportFormat.Xlsx, ".xlsx"),
            (ExportFormat.Csv, ".csv"),
            (ExportFormat.Mat, ".mat"),
        })
        {
            string outputPath = Path.Combine(
                files.Path,
                $"postprocess{extension}");
            IReadOnlyList<string> written =
                viewModel.ExportSelected(outputPath, format);
            Assert.IsTrue(written.All(File.Exists));
            if (format == ExportFormat.Csv)
            {
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "postprocess_ExternalIPCE.csv",
                        "postprocess_SpectrumSummary.csv",
                        "postprocess_SpectrumCurve.csv",
                        "postprocess_MeasurementSettings.csv",
                        "postprocess_InputMetadata.csv",
                    },
                    written.Select(Path.GetFileName).ToArray());
            }
        }
    }

    [TestMethod]
    public void StaleResults_AreExcludedFromEveryExportFormat()
    {
        using var files = new TemporaryDirectory();
        var session = new SessionState();
        session.SetPowerDensity(
        [
            PowerPoint(400, 1e-5),
            PowerPoint(500, 1e-5),
        ]);
        session.SetCalculatedIpce(
        [
            IpcePoint(400, 20),
            IpcePoint(500, 50),
        ]);
        session.SetExternalIpce(new ExternalIpceData(
            [
                new IpceValue(400, 25),
                new IpceValue(500, 75),
            ],
            "Wavelength (nm)",
            "IPCE (%)"));
        session.SetSpectrum(CreateSpectrum());
        session.Integrate(400, 500);
        session.MarkPowerDensityStale("硅面积已改变");
        var viewModel = new SpectrumWorkflowViewModel(session)
        {
            IncludePowerDensityExport = true,
            IncludeCalculatedIpceExport = true,
            IncludeExternalIpceExport = true,
            IncludeIntegrationExport = true,
        };

        CollectionAssert.AreEqual(
            new[]
            {
                "ExternalIPCE",
                "MeasurementSettings",
                "InputMetadata",
            },
            viewModel.BuildSelectedExportTables()
                .Select(table => table.Name)
                .ToArray());

        foreach ((ExportFormat format, string extension) in new[]
        {
            (ExportFormat.Xlsx, ".xlsx"),
            (ExportFormat.Csv, ".csv"),
            (ExportFormat.Mat, ".mat"),
        })
        {
            IReadOnlyList<string> written = viewModel.ExportSelected(
                Path.Combine(files.Path, $"current-only{extension}"),
                format);
            Assert.IsTrue(written.All(File.Exists));
            if (format == ExportFormat.Csv)
            {
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "current-only_ExternalIPCE.csv",
                        "current-only_MeasurementSettings.csv",
                        "current-only_InputMetadata.csv",
                    },
                    written.Select(Path.GetFileName).ToArray());
            }
        }

        viewModel.IncludeExternalIpceExport = false;
        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => viewModel.BuildSelectedExportTables());
        Assert.AreEqual(
            "IPCE:NoCurrentExportSelection",
            exception.Code);
    }

    private static TraceData CreateFixedDelayTrace(
        IReadOnlyList<double> currents)
    {
        var times = new List<double> { 0 };
        var values = new List<double> { 0 };
        for (int index = 0; index < currents.Count; index++)
        {
            double windowEnd = (index + 1) * 10;
            times.Add(windowEnd - 1);
            values.Add(currents[index]);
            times.Add(windowEnd - 0.5);
            values.Add(currents[index]);
            times.Add(windowEnd);
            values.Add(0);
        }

        return new TraceData(times, values, TraceMetadata.Unknown);
    }

    private static IReadOnlyList<SpectrumPoint> CreateSpectrum() =>
    [
        new SpectrumPoint(400, 1),
        new SpectrumPoint(450, 1),
        new SpectrumPoint(500, 1),
        new SpectrumPoint(550, 1),
        new SpectrumPoint(600, 1),
    ];

    private static PowerDensityPoint PowerPoint(
        double wavelength,
        double powerDensity) =>
        new(wavelength, 1, 1, 1, 1, 0, 1, powerDensity, 0, 2);

    private static IpcePoint IpcePoint(
        double wavelength,
        double ipcePercent) =>
        new(
            wavelength,
            1e-5,
            0,
            false,
            1e-6,
            1e-6,
            1e-6,
            0,
            1,
            1e-6,
            0,
            2,
            ipcePercent,
            0);

    private static void AssertClose(double expected, double actual)
    {
        double tolerance = Math.Max(1e-12, Math.Abs(expected) * 1e-9);
        Assert.IsTrue(
            Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, actual {actual:R}");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteText(string fileName, string contents)
        {
            string path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(
                path,
                contents,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
