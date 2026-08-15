using System.IO;
using System.Text;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.State;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class SessionStateTests
{
    [TestMethod]
    public void FailedSiliconTraceImport_LeavesPriorValidTraceUnchanged()
    {
        using var files = new TemporaryFiles();
        string validPath = files.Write(
            "valid.txt",
            "Time (s)\tCurrent (A)\n0\t1e-6\n1\t2e-6\n");
        string invalidPath = files.Write(
            "invalid.txt",
            "Time\tCurrent\n0\t1\n1\t2\n");
        var state = new SessionState();
        state.ImportSiliconTrace(validPath);
        TraceData priorTrace = state.SiliconTrace!;

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => state.ImportSiliconTrace(invalidPath));

        Assert.AreEqual("IPCE:TraceUnitsRequired", exception.Code);
        Assert.AreSame(priorTrace, state.SiliconTrace);
    }

    [TestMethod]
    public void ImportingExternalIpce_DoesNotClearCalculatedIpce()
    {
        using var files = new TemporaryFiles();
        string path = files.Write(
            "external.csv",
            "Wavelength (nm),IPCE (%)\n400,20\n500,120\n");
        var state = new SessionState();
        state.SetCalculatedIpce(CreateCalculatedIpce());
        IReadOnlyList<IpcePoint> priorCalculated = state.CalculatedIpce!;

        ExternalIpceData imported = state.ImportExternalIpce(path);

        Assert.AreSame(imported, state.ExternalIpce);
        Assert.AreSame(priorCalculated, state.CalculatedIpce);
        Assert.AreEqual(120d, state.ExternalIpce!.Points[1].IpcePercent);
    }

    [TestMethod]
    public void SelectingSource_ChangesOnlySelectionAndRetainsBothDatasets()
    {
        var state = new SessionState();
        state.SetCalculatedIpce(CreateCalculatedIpce());
        state.SetExternalIpce(CreateExternalIpce());
        IReadOnlyList<IpcePoint> calculated = state.CalculatedIpce!;
        ExternalIpceData external = state.ExternalIpce!;

        state.SelectIpceSource(IpceSource.External);

        Assert.AreEqual(IpceSource.External, state.SelectedIpceSource);
        Assert.AreSame(calculated, state.CalculatedIpce);
        Assert.AreSame(external, state.ExternalIpce);
    }

    [TestMethod]
    public void ExternalIntegration_WorksInOtherwiseEmptyMeasurementSession()
    {
        var state = new SessionState();
        state.SetExternalIpce(CreateExternalIpce());
        state.SetSpectrum(CreateSpectrum());
        state.SelectIpceSource(IpceSource.External);

        IntegrationResult result = state.Integrate(400, 500);

        Assert.IsTrue(
            result.Summary
                .IntegratedCurrentDensityMilliamperePerSquareCentimetre > 0);
        Assert.AreSame(result, state.IntegrationResult);
        Assert.IsNull(state.SiliconTrace);
        Assert.IsNull(state.Calibration);
        Assert.IsNull(state.PowerDensity);
        Assert.IsNull(state.SampleTrace);
        Assert.IsNull(state.CalculatedIpce);
    }

    [TestMethod]
    public void ReplacingPowerDensity_InvalidatesCalculatedIpceButKeepsExternal()
    {
        var state = new SessionState();
        state.SetCalculatedIpce(CreateCalculatedIpce());
        state.SetExternalIpce(CreateExternalIpce());
        IReadOnlyList<IpcePoint> calculated = state.CalculatedIpce!;
        ExternalIpceData external = state.ExternalIpce!;

        state.SetPowerDensity(CreatePowerDensity());

        Assert.AreSame(calculated, state.CalculatedIpce);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.CalculatedIpceStatus.Freshness);
        Assert.AreSame(external, state.ExternalIpce);
        Assert.AreEqual(2, state.PowerDensity!.Count);
        Assert.AreEqual(
            ResultFreshness.Current,
            state.PowerDensityStatus.Freshness);
    }

    [TestMethod]
    public void FailedIntegration_LeavesPriorValidIntegrationUnchanged()
    {
        var state = new SessionState();
        state.SetExternalIpce(CreateExternalIpce());
        state.SetSpectrum(CreateSpectrum());
        state.SelectIpceSource(IpceSource.External);
        state.Integrate(400, 500);
        IntegrationResult prior = state.IntegrationResult!;

        Assert.ThrowsExactly<IpceException>(
            () => state.Integrate(300, 500));

        Assert.AreSame(prior, state.IntegrationResult);
    }

    private static IReadOnlyList<IpcePoint> CreateCalculatedIpce() =>
        Array.AsReadOnly(new[]
        {
            CreateIpcePoint(400, 20),
            CreateIpcePoint(500, 50),
        });

    private static IpcePoint CreateIpcePoint(
        double wavelengthNm,
        double ipcePercent) =>
        new(
            wavelengthNm,
            1e-4,
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

    private static ExternalIpceData CreateExternalIpce() =>
        new(
            [
                new IpceValue(400, 25),
                new IpceValue(500, 75),
            ],
            "Wavelength (nm)",
            "IPCE (%)");

    private static IReadOnlyList<SpectrumPoint> CreateSpectrum() =>
        Array.AsReadOnly(new[]
        {
            new SpectrumPoint(400, 1),
            new SpectrumPoint(500, 1),
        });

    private static IReadOnlyList<PowerDensityPoint>
        CreatePowerDensity() =>
        Array.AsReadOnly(new[]
        {
            CreatePowerDensityPoint(400),
            CreatePowerDensityPoint(500),
        });

    private static PowerDensityPoint CreatePowerDensityPoint(
        double wavelengthNm) =>
        new(
            wavelengthNm,
            0.5,
            1e-6,
            1e-6,
            1e-6,
            0,
            0.36,
            1e-5,
            0,
            2);

    private sealed class TemporaryFiles : IDisposable
    {
        public TemporaryFiles()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"ipce-session-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        private string DirectoryPath { get; }

        public string Write(string fileName, string contents)
        {
            string path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(
                path,
                contents,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
