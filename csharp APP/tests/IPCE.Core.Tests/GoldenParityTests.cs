using System.Globalization;
using System.Text.Json;
using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Extraction;
using IPCE.Core.Scheduling;
using IPCE.IO.Import;
using IPCE.IO.Startup;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class GoldenParityTests
{
    private const double AbsoluteTolerance = 1e-12;
    private const double RelativeTolerance = 1e-9;

    [TestMethod]
    public void AllMatlabGoldenColumns_ProducePassingMachineReadableReport()
    {
        using var defaults = new MaterializedDefaults();
        CalibrationData calibration =
            CalibrationReader.Read(defaults.CalibrationPath);
        TraceData siliconTrace =
            ItTraceReader.Read(defaults.SiliconTracePath);
        IReadOnlyList<AnchorPoint> anchors =
            AnchorReader.Read(defaults.SiliconAnchorPath);
        double[] wavelengths =
            Enumerable.Range(0, 161).Select(index => 300d + 5 * index)
                .ToArray();
        IReadOnlyList<SchedulePoint> schedule = ScheduleBuilder.Build(
            wavelengths,
            AlignmentMode.Anchors,
            anchors,
            fixedStartTimeSeconds: 50,
            nominalDelaySeconds: 8);
        IReadOnlyList<ExtractedPoint> extracted = TraceExtractor.Extract(
            siliconTrace,
            schedule,
            averagingDurationSeconds: 4,
            new DarkCorrection(true, 0.1, 10));
        IReadOnlyList<PowerDensityPoint> power =
            IpceCalculator.CalculatePowerDensity(
                calibration,
                extracted,
                siliconAreaSquareCentimetres: 0.36);

        var extractedGolden =
            GoldenCsv.ReadNumeric("default_silicon_extracted.csv");
        var powerGolden =
            GoldenCsv.ReadNumeric("default_power_density.csv");
        var ipceGolden =
            GoldenCsv.ReadNumeric("synthetic_sample_ipce.csv");
        var curveGolden =
            GoldenCsv.ReadNumeric("integration_curve.csv");
        string[] summaryFields =
            ReadSingleCsvRow("integration_summary.csv");

        IReadOnlyList<IpcePoint> ipce = CalculateSyntheticIpce(ipceGolden);
        IntegrationResult integration = SpectrumIntegrator.Integrate(
            [
                new IpceValue(400, 100),
                new IpceValue(500, 100),
                new IpceValue(600, 100),
            ],
            Enumerable.Range(0, 9)
                .Select(index => new SpectrumPoint(400 + 25 * index, 1))
                .ToArray(),
            400,
            600);

        var comparisons = new List<ParityColumnResult>();
        CompareSchedule(comparisons, schedule, extractedGolden);
        CompareExtracted(comparisons, extracted, extractedGolden);
        ComparePower(comparisons, power, powerGolden);
        CompareIpce(comparisons, ipce, ipceGolden);
        CompareIntegrationSummary(
            comparisons,
            integration.Summary,
            summaryFields);
        CompareIntegrationCurve(
            comparisons,
            integration.Curve,
            curveGolden);

        var report = new
        {
            schemaVersion = 1,
            absoluteTolerance = AbsoluteTolerance,
            relativeTolerance = RelativeTolerance,
            passed = comparisons.All(item => item.Passed),
            columns = comparisons,
        };
        string reportPath = Path.Combine(
            FindCSharpProjectRoot(),
            "tests",
            "TestData",
            "Golden",
            "parity-report.json");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true }));

        ParityColumnResult[] failed = comparisons
            .Where(item => !item.Passed)
            .ToArray();
        Assert.AreEqual(
            0,
            failed.Length,
            string.Join(
                Environment.NewLine,
                failed.Select(item =>
                    $"{item.Dataset}.{item.Column}: abs={item.MaxAbsoluteError:R}, rel={item.MaxRelativeError:R}")));
        Assert.IsTrue(File.Exists(reportPath));
    }

    private static IReadOnlyList<IpcePoint> CalculateSyntheticIpce(
        IReadOnlyList<IReadOnlyDictionary<string, double>> golden)
    {
        PowerDensityPoint[] power = golden.Select(row =>
            new PowerDensityPoint(
                row["Wavelength_nm"],
                1,
                0,
                0,
                1,
                0,
                1,
                row["IncidentPowerDensity_W_cm2"],
                row["IncidentPowerDensitySE_W_cm2"],
                1)).ToArray();
        ExtractedPoint[] sample = golden.Select(row =>
            new ExtractedPoint(
                row["Wavelength_nm"],
                row["SampleMeanCurrent_A"],
                row["SamplePhotoCurrentSigned_A"],
                row["SamplePhotocurrent_A"],
                row["SamplePhotoCurrentSE_A"],
                (int)row["SampleSampleCount"])).ToArray();
        return IpceCalculator.CalculateIpce(power, sample, 0.75);
    }

    private static void CompareSchedule(
        List<ParityColumnResult> report,
        IReadOnlyList<SchedulePoint> actual,
        IReadOnlyList<IReadOnlyDictionary<string, double>> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        Add(report, "DefaultSchedule", "Wavelength_nm",
            expected.Select(row => row["Wavelength_nm"]),
            actual.Select(point => point.WavelengthNm));
        Add(report, "DefaultSchedule", "ReferenceTime_s",
            expected.Select(row => row["WindowStart_s"]),
            actual.Select(point => point.ReferenceTimeSeconds));
        Add(report, "DefaultSchedule", "DwellStart_s",
            expected.Select(row => row["DwellStart_s"]),
            actual.Select(point => point.WindowStartSeconds));
        Add(report, "DefaultSchedule", "DwellEnd_s",
            expected.Select(row =>
                row["DwellStart_s"] + row["DwellDuration_s"]),
            actual.Select(point => point.WindowEndSeconds));
        Add(report, "DefaultSchedule", "AverageEnd_s",
            expected.Select(row => row["WindowEnd_s"]),
            actual.Select(point => Math.Min(
                point.ReferenceTimeSeconds + 4,
                point.WindowEndSeconds)));
    }

    private static void CompareExtracted(
        List<ParityColumnResult> report,
        IReadOnlyList<ExtractedPoint> actual,
        IReadOnlyList<IReadOnlyDictionary<string, double>> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        Add(report, "DefaultExtraction", "Wavelength_nm",
            expected.Select(row => row["Wavelength_nm"]),
            actual.Select(point => point.WavelengthNm));
        Add(report, "DefaultExtraction", "MeanCurrent_A",
            expected.Select(row => row["MeanCurrent_A"]),
            actual.Select(point => point.MeanCurrentAmperes));
        Add(report, "DefaultExtraction", "PhotoCurrent_A",
            expected.Select(row => row["PhotoCurrent_A"]),
            actual.Select(point => point.PhotoCurrentSignedAmperes));
        Add(report, "DefaultExtraction", "AbsPhotoCurrent_A",
            expected.Select(row => row["AbsPhotoCurrent_A"]),
            actual.Select(point => point.AbsolutePhotoCurrentAmperes));
        Add(report, "DefaultExtraction", "PhotoCurrentSE_A",
            expected.Select(row => row["PhotoCurrentSE_A"]),
            actual.Select(point => point.PhotoCurrentStandardErrorAmperes));
        Add(report, "DefaultExtraction", "SampleCount",
            expected.Select(row => row["SampleCount"]),
            actual.Select(point => (double)point.SampleCount));
    }

    private static void ComparePower(
        List<ParityColumnResult> report,
        IReadOnlyList<PowerDensityPoint> actual,
        IReadOnlyList<IReadOnlyDictionary<string, double>> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        Add(report, "PowerDensity", "Wavelength_nm",
            expected.Select(row => row["Wavelength_nm"]),
            actual.Select(point => point.WavelengthNm));
        Add(report, "PowerDensity", "SiResponsivity_A_per_W",
            expected.Select(row => row["SiResponsivity_A_per_W"]),
            actual.Select(point => point.SiliconResponsivityAmperesPerWatt));
        Add(report, "PowerDensity", "SiMeanCurrent_A",
            expected.Select(row => row["SiMeanCurrent_A"]),
            actual.Select(point => point.SiliconMeanCurrentAmperes));
        Add(report, "PowerDensity", "SiPhotoCurrentSigned_A",
            expected.Select(row => row["SiPhotoCurrentSigned_A"]),
            actual.Select(point => point.SiliconPhotoCurrentSignedAmperes));
        Add(report, "PowerDensity", "SiPhotocurrent_A",
            expected.Select(row => row["SiPhotocurrent_A"]),
            actual.Select(point => point.SiliconPhotocurrentAmperes));
        Add(report, "PowerDensity", "SiPhotoCurrentSE_A",
            expected.Select(row => row["SiPhotoCurrentSE_A"]),
            actual.Select(point =>
                point.SiliconPhotoCurrentStandardErrorAmperes));
        Add(report, "PowerDensity", "SiliconIlluminatedArea_cm2",
            expected.Select(row => row["SiliconIlluminatedArea_cm2"]),
            actual.Select(point =>
                point.SiliconIlluminatedAreaSquareCentimetres));
        Add(report, "PowerDensity", "IncidentPowerDensity_W_cm2",
            expected.Select(row => row["IncidentPowerDensity_W_cm2"]),
            actual.Select(point =>
                point.IncidentPowerDensityWattsPerSquareCentimetre));
        Add(report, "PowerDensity", "IncidentPowerDensitySE_W_cm2",
            expected.Select(row => row["IncidentPowerDensitySE_W_cm2"]),
            actual.Select(point => point.IncidentPowerDensityStandardError));
        Add(report, "PowerDensity", "SiSampleCount",
            expected.Select(row => row["SiSampleCount"]),
            actual.Select(point => (double)point.SampleCount));
    }

    private static void CompareIpce(
        List<ParityColumnResult> report,
        IReadOnlyList<IpcePoint> actual,
        IReadOnlyList<IReadOnlyDictionary<string, double>> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        Add(report, "SampleIPCE", "Wavelength_nm",
            expected.Select(row => row["Wavelength_nm"]),
            actual.Select(point => point.WavelengthNm));
        Add(report, "SampleIPCE", "IncidentPowerDensity_W_cm2",
            expected.Select(row => row["IncidentPowerDensity_W_cm2"]),
            actual.Select(point =>
                point.IncidentPowerDensityWattsPerSquareCentimetre));
        Add(report, "SampleIPCE", "IncidentPowerDensitySE_W_cm2",
            expected.Select(row => row["IncidentPowerDensitySE_W_cm2"]),
            actual.Select(point => point.IncidentPowerDensityStandardError));
        Add(report, "SampleIPCE", "PowerDensityInterpolated",
            expected.Select(row => row["PowerDensityInterpolated"]),
            actual.Select(point =>
                point.PowerDensityInterpolated ? 1d : 0d));
        Add(report, "SampleIPCE", "SampleMeanCurrent_A",
            expected.Select(row => row["SampleMeanCurrent_A"]),
            actual.Select(point => point.SampleMeanCurrentAmperes));
        Add(report, "SampleIPCE", "SamplePhotoCurrentSigned_A",
            expected.Select(row => row["SamplePhotoCurrentSigned_A"]),
            actual.Select(point => point.SamplePhotoCurrentSignedAmperes));
        Add(report, "SampleIPCE", "SamplePhotocurrent_A",
            expected.Select(row => row["SamplePhotocurrent_A"]),
            actual.Select(point => point.SamplePhotocurrentAmperes));
        Add(report, "SampleIPCE", "SamplePhotoCurrentSE_A",
            expected.Select(row => row["SamplePhotoCurrentSE_A"]),
            actual.Select(point => point.SamplePhotoCurrentStandardErrorAmperes));
        Add(report, "SampleIPCE", "SampleIlluminatedArea_cm2",
            expected.Select(row => row["SampleIlluminatedArea_cm2"]),
            actual.Select(point =>
                point.SampleIlluminatedAreaSquareCentimetres));
        Add(report, "SampleIPCE", "SamplePhotocurrentDensity_A_cm2",
            expected.Select(row => row["SamplePhotocurrentDensity_A_cm2"]),
            actual.Select(point =>
                point.SamplePhotocurrentDensityAmperesPerSquareCentimetre));
        Add(report, "SampleIPCE", "SamplePhotoCurrentDensitySE_A_cm2",
            expected.Select(row =>
                row["SamplePhotoCurrentDensitySE_A_cm2"]),
            actual.Select(point =>
                point.SamplePhotoCurrentDensityStandardError));
        Add(report, "SampleIPCE", "SampleSampleCount",
            expected.Select(row => row["SampleSampleCount"]),
            actual.Select(point => (double)point.SampleCount));
        Add(report, "SampleIPCE", "IPCE_percent",
            expected.Select(row => row["IPCE_percent"]),
            actual.Select(point => point.IpcePercent));
        Add(report, "SampleIPCE", "IPCE_EstimatedSE_percent",
            expected.Select(row => row["IPCE_EstimatedSE_percent"]),
            actual.Select(point => point.IpceEstimatedStandardErrorPercent));
    }

    private static void CompareIntegrationSummary(
        List<ParityColumnResult> report,
        IntegrationSummary actual,
        IReadOnlyList<string> fields)
    {
        Add(report, "IntegrationSummary", "MinimumWavelength_nm",
            [Parse(fields[0])], [actual.MinimumWavelengthNm]);
        Add(report, "IntegrationSummary", "MaximumWavelength_nm",
            [Parse(fields[1])], [actual.MaximumWavelengthNm]);
        Add(report, "IntegrationSummary",
            "IntegratedCurrentDensity_mA_cm2",
            [Parse(fields[2])],
            [actual.IntegratedCurrentDensityMilliamperePerSquareCentimetre]);
        Add(report, "IntegrationSummary", "IntegratedPower_W_m2",
            [Parse(fields[3])],
            [actual.IntegratedPowerWattsPerSquareMetre]);
        Add(report, "IntegrationSummary", "IntegrationGridPoints",
            [Parse(fields[4])],
            [actual.IntegrationGridPoints]);
        report.Add(new ParityColumnResult(
            "IntegrationSummary",
            "Interpolation",
            1,
            0,
            0,
            0,
            0,
            0,
            [],
            string.Equals(
                fields[5].Trim('"'),
                actual.Interpolation,
                StringComparison.Ordinal)));
    }

    private static void CompareIntegrationCurve(
        List<ParityColumnResult> report,
        IReadOnlyList<IntegrationCurvePoint> actual,
        IReadOnlyList<IReadOnlyDictionary<string, double>> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        Add(report, "IntegrationCurve", "Wavelength_nm",
            expected.Select(row => row["Wavelength_nm"]),
            actual.Select(point => point.WavelengthNm));
        Add(report, "IntegrationCurve", "Irradiance_W_m2_nm",
            expected.Select(row => row["Irradiance_W_m2_nm"]),
            actual.Select(point =>
                point.IrradianceWattsPerSquareMetrePerNanometre));
        Add(report, "IntegrationCurve", "IPCE_percent",
            expected.Select(row => row["IPCE_percent"]),
            actual.Select(point => point.IpcePercent));
        Add(report, "IntegrationCurve", "EQE_fraction",
            expected.Select(row => row["EQE_fraction"]),
            actual.Select(point => point.EqeFraction));
        Add(report, "IntegrationCurve", "PhotonFlux_m2_s_nm",
            expected.Select(row => row["PhotonFlux_m2_s_nm"]),
            actual.Select(point =>
                point.PhotonFluxPerSquareMetreSecondNanometre));
        Add(report, "IntegrationCurve", "SpectralCurrent_mA_cm2_nm",
            expected.Select(row => row["SpectralCurrent_mA_cm2_nm"]),
            actual.Select(point =>
                point.SpectralCurrentMilliamperePerSquareCentimetreNanometre));
        Add(report, "IntegrationCurve",
            "CumulativeCurrentDensity_mA_cm2",
            expected.Select(row =>
                row["CumulativeCurrentDensity_mA_cm2"]),
            actual.Select(point =>
                point.CumulativeCurrentDensityMilliamperePerSquareCentimetre));
    }

    private static void Add(
        List<ParityColumnResult> report,
        string dataset,
        string column,
        IEnumerable<double> expectedValues,
        IEnumerable<double> actualValues)
    {
        double[] expected = expectedValues.ToArray();
        double[] actual = actualValues.ToArray();
        Assert.AreEqual(expected.Length, actual.Length);
        double maxAbsolute = 0;
        double maxRelative = 0;
        int maxErrorRow = 0;
        double expectedAtMaxError = 0;
        double actualAtMaxError = 0;
        var examples = new List<ParityDifferenceExample>();
        bool passed = true;
        for (int index = 0; index < expected.Length; index++)
        {
            double absolute = Math.Abs(actual[index] - expected[index]);
            double relative = expected[index] == 0
                ? absolute == 0 ? 0 : double.MaxValue
                : absolute / Math.Abs(expected[index]);
            if (absolute > maxAbsolute)
            {
                maxAbsolute = absolute;
                maxErrorRow = index;
                expectedAtMaxError = expected[index];
                actualAtMaxError = actual[index];
            }
            maxRelative = Math.Max(maxRelative, relative);
            if (absolute > 0 && examples.Count < 8)
            {
                examples.Add(new ParityDifferenceExample(
                    index,
                    expected[index],
                    actual[index],
                    actual[index] - expected[index]));
            }
            passed &= absolute <= AbsoluteTolerance ||
                relative <= RelativeTolerance;
        }

        report.Add(new ParityColumnResult(
            dataset,
            column,
            expected.Length,
            maxAbsolute,
            maxRelative,
            maxErrorRow,
            expectedAtMaxError,
            actualAtMaxError,
            examples,
            passed));
    }

    private static string[] ReadSingleCsvRow(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Golden",
            fileName);
        return File.ReadLines(path).Skip(1).First().Split(',');
    }

    private static double Parse(string value) =>
        double.Parse(
            value.Trim('"'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);

    private static string FindCSharpProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IPCE.slnx")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName,
                    "tests",
                    "TestData",
                    "Golden")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the IPCE C# project root.");
    }

    private sealed record ParityColumnResult(
        string Dataset,
        string Column,
        int Rows,
        double MaxAbsoluteError,
        double MaxRelativeError,
        int MaxErrorRow,
        double ExpectedAtMaxError,
        double ActualAtMaxError,
        IReadOnlyList<ParityDifferenceExample> DifferenceExamples,
        bool Passed);

    private sealed record ParityDifferenceExample(
        int Row,
        double Expected,
        double Actual,
        double Difference);

    private sealed class MaterializedDefaults : IDisposable
    {
        public MaterializedDefaults()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"ipce-parity-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
            DefaultConfiguration config = DefaultConfiguration.Current;
            CalibrationPath = Materialize(config.CalibrationFileName);
            SiliconTracePath = Materialize(config.SiliconTraceFileName);
            SiliconAnchorPath = Materialize(config.SiliconAnchorFileName);
        }

        private string DirectoryPath { get; }

        public string CalibrationPath { get; }

        public string SiliconTracePath { get; }

        public string SiliconAnchorPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private string Materialize(string fileName)
        {
            string path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllBytes(
                path,
                StartupDataResolver.Resolve(fileName).Content);
            return path;
        }
    }
}
