using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class ResultFreshnessTests
{
    [TestMethod]
    public void PowerDensityStale_RetainsDataAndCascadesToCalculatedResults()
    {
        var state = CreateCalculatedIntegrationSession();
        state.SetExternalIpce(CreateExternalIpce());
        IReadOnlyList<PowerDensityPoint> power = state.PowerDensity!;
        IReadOnlyList<IpcePoint> calculated = state.CalculatedIpce!;
        IntegrationResult integration = state.IntegrationResult!;
        ExternalIpceData external = state.ExternalIpce!;

        state.MarkPowerDensityStale("硅面积已改变");

        Assert.AreEqual(
            ResultFreshness.Stale,
            state.PowerDensityStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.CalculatedIpceStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.IntegrationStatus.Freshness);
        Assert.AreSame(power, state.PowerDensity);
        Assert.AreSame(calculated, state.CalculatedIpce);
        Assert.AreSame(integration, state.IntegrationResult);
        Assert.AreSame(external, state.ExternalIpce);
        StringAssert.Contains(
            state.PowerDensityStatus.Reason,
            "硅面积");
    }

    [TestMethod]
    public void CalculatedIpceStale_DoesNotInvalidatePowerDensity()
    {
        var state = CreateCalculatedIntegrationSession();

        state.MarkCalculatedIpceStale("样品面积已改变");

        Assert.AreEqual(
            ResultFreshness.Current,
            state.PowerDensityStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.CalculatedIpceStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.IntegrationStatus.Freshness);
        Assert.IsNotNull(state.CalculatedIpce);
        Assert.IsNotNull(state.IntegrationResult);
    }

    [TestMethod]
    public void IntegrationStale_RetainsPriorCurve()
    {
        var state = CreateExternalIntegrationSession();
        IntegrationResult prior = state.IntegrationResult!;

        state.MarkIntegrationStale("积分范围已改变");

        Assert.AreEqual(
            ResultFreshness.Stale,
            state.IntegrationStatus.Freshness);
        Assert.AreSame(prior, state.IntegrationResult);
        StringAssert.Contains(
            state.IntegrationStatus.Reason,
            "积分范围");
    }

    [TestMethod]
    public void ExternalIntegration_IsIndependentOfMeasurementStaleness()
    {
        var state = CreateExternalIntegrationSession();
        state.SetPowerDensity(CreatePowerDensity());
        state.SetCalculatedIpce(CreateCalculatedIpce());
        IntegrationResult prior = state.IntegrationResult!;

        state.MarkPowerDensityStale("硅面积已改变");

        Assert.AreEqual(
            ResultFreshness.Stale,
            state.PowerDensityStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Stale,
            state.CalculatedIpceStatus.Freshness);
        Assert.AreEqual(
            ResultFreshness.Current,
            state.IntegrationStatus.Freshness);
        Assert.AreSame(prior, state.IntegrationResult);
        Assert.IsNotNull(state.ExternalIpce);
    }

    [TestMethod]
    public void CalculatedIntegration_RejectsStaleIpceWithStableError()
    {
        var state = CreateCalculatedIntegrationSession();
        state.MarkCalculatedIpceStale("样品面积已改变");

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => state.Integrate(400, 500));

        Assert.AreEqual("IPCE:StaleResult", exception.Code);
        StringAssert.Contains(exception.Message, "样品面积");
    }

    [TestMethod]
    public void SampleCalculation_RejectsStalePowerDensity()
    {
        var state = new SessionState();
        state.SetPowerDensity(CreatePowerDensity());
        state.MarkPowerDensityStale("硅面积已改变");
        state.SetSampleTrace(new TraceData(
            [0d, 1d],
            [0d, 1e-6],
            TraceMetadata.Unknown));
        var viewModel = new SampleWorkflowViewModel(state);

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => viewModel.CalculateIpce());

        Assert.AreEqual("IPCE:StaleResult", exception.Code);
        StringAssert.Contains(exception.Message, "硅面积");
    }

    private static SessionState CreateCalculatedIntegrationSession()
    {
        var state = new SessionState();
        state.SetPowerDensity(CreatePowerDensity());
        state.SetCalculatedIpce(CreateCalculatedIpce());
        state.SetSpectrum(CreateSpectrum());
        state.Integrate(400, 500);
        return state;
    }

    private static SessionState CreateExternalIntegrationSession()
    {
        var state = new SessionState();
        state.SetExternalIpce(CreateExternalIpce());
        state.SetSpectrum(CreateSpectrum());
        state.SelectIpceSource(IpceSource.External);
        state.Integrate(400, 500);
        return state;
    }

    private static IReadOnlyList<PowerDensityPoint>
        CreatePowerDensity() =>
    [
        PowerPoint(400),
        PowerPoint(500),
    ];

    private static PowerDensityPoint PowerPoint(double wavelengthNm) =>
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

    private static IReadOnlyList<IpcePoint> CreateCalculatedIpce() =>
    [
        IpcePoint(400, 20),
        IpcePoint(500, 50),
    ];

    private static IpcePoint IpcePoint(
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
    [
        new SpectrumPoint(400, 1),
        new SpectrumPoint(500, 1),
    ];
}
