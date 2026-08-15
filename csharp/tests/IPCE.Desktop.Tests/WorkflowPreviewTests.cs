using IPCE.Core.Domain;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class WorkflowPreviewTests
{
    [TestMethod]
    public void FixedSchedule_ReportsExactCoveredAndExceededRanges()
    {
        TraceData trace = CreateTrace(0, 100);

        SchedulePreview covered = WorkflowPreviewBuilder.BuildSchedule(
            trace,
            [400d, 500d],
            AlignmentMode.FixedDelay,
            [],
            fixedStartTimeSeconds: 10,
            nominalDelaySeconds: 40,
            TestLocalization.Chinese());
        SchedulePreview exceeded = WorkflowPreviewBuilder.BuildSchedule(
            trace,
            [400d, 500d],
            AlignmentMode.FixedDelay,
            [],
            fixedStartTimeSeconds: 10,
            nominalDelaySeconds: 50,
            TestLocalization.Chinese());

        Assert.IsTrue(covered.Coverage.IsWithinCoverage);
        Assert.AreEqual(
            "数据范围 0–100 s；请求范围 10–90 s，完整覆盖。",
            covered.Coverage.Message);
        Assert.IsFalse(exceeded.Coverage.IsWithinCoverage);
        StringAssert.Contains(exceeded.Coverage.Message, "超出 10");
    }

    [TestMethod]
    public void AnchorSchedule_PreservesAnchorsAndUsesWindowCoverage()
    {
        TraceData trace = CreateTrace(0, 100);
        AnchorPoint[] anchors =
        [
            new AnchorPoint(400, 20),
            new AnchorPoint(500, 80),
        ];

        SchedulePreview preview = WorkflowPreviewBuilder.BuildSchedule(
            trace,
            [400d, 500d],
            AlignmentMode.Anchors,
            anchors,
            fixedStartTimeSeconds: 0,
            nominalDelaySeconds: 10);

        Assert.AreEqual(2, preview.Points.Count);
        Assert.AreEqual(2, preview.Anchors.Count);
        Assert.AreEqual(-10d, preview.Coverage.RequestedMinimum);
        Assert.AreEqual(110d, preview.Coverage.RequestedMaximum);
        Assert.IsFalse(preview.Coverage.IsWithinCoverage);
    }

    [TestMethod]
    public void IntegrationCoverage_UsesCommonIpceSpectrumRange()
    {
        IpceValue[] ipce =
        [
            new IpceValue(400, 20),
            new IpceValue(700, 50),
        ];
        SpectrumPoint[] spectrum =
        [
            new SpectrumPoint(300, 1),
            new SpectrumPoint(600, 1),
        ];

        CoveragePreview preview =
            WorkflowPreviewBuilder.BuildIntegrationCoverage(
                ipce,
                spectrum,
                requestedMinimumNm: 450,
                requestedMaximumNm: 650,
                TestLocalization.Chinese());

        Assert.AreEqual(400d, preview.DataMinimum);
        Assert.AreEqual(600d, preview.DataMaximum);
        Assert.IsFalse(preview.IsWithinCoverage);
        StringAssert.Contains(preview.Message, "超出 50");
    }

    [TestMethod]
    public void WorkflowViewModels_UpdateLiveCoverageWithoutCalculating()
    {
        var state = new SessionState();
        state.SetSiliconTrace(CreateTrace(0, 100));
        var silicon = new SiliconWorkflowViewModel(
            state,
            localization: TestLocalization.Chinese())
        {
            AlignmentMode = AlignmentMode.FixedDelay,
            WavelengthStartNanometres = 400,
            WavelengthEndNanometres = 500,
            WavelengthStepNanometres = 100,
            FixedStartTimeSeconds = 10,
            NominalDelaySeconds = 40,
        };

        Assert.IsNotNull(silicon.Preview);
        Assert.IsTrue(silicon.Preview.Coverage.IsWithinCoverage);
        string covered = silicon.CoverageMessage;

        silicon.NominalDelaySeconds = 50;

        Assert.IsNotNull(silicon.Preview);
        Assert.IsFalse(silicon.Preview.Coverage.IsWithinCoverage);
        Assert.AreNotEqual(covered, silicon.CoverageMessage);
        Assert.IsNull(state.PowerDensity);
    }

    [TestMethod]
    public void SpectrumViewModel_UsesSelectedCommonCoverage()
    {
        var state = new SessionState();
        state.SetExternalIpce(new ExternalIpceData(
            [
                new IpceValue(400, 20),
                new IpceValue(700, 50),
            ],
            "",
            ""));
        state.SetSpectrum(
        [
            new SpectrumPoint(300, 1),
            new SpectrumPoint(600, 1),
        ]);
        state.SelectIpceSource(IpceSource.External);
        var spectrum = new SpectrumWorkflowViewModel(
            state,
            localization: TestLocalization.Chinese())
        {
            IntegrationMinimumNanometres = 450,
            IntegrationMaximumNanometres = 550,
        };

        Assert.IsNotNull(spectrum.Coverage);
        Assert.IsTrue(spectrum.Coverage.IsWithinCoverage);

        spectrum.IntegrationMaximumNanometres = 650;

        Assert.IsNotNull(spectrum.Coverage);
        Assert.IsFalse(spectrum.Coverage.IsWithinCoverage);
        StringAssert.Contains(spectrum.CoverageMessage, "超出 50");
    }

    private static TraceData CreateTrace(double minimum, double maximum) =>
        new(
            [minimum, maximum],
            [0d, 1d],
            TraceMetadata.Unknown);
}
