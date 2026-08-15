using IPCE.Core.Domain;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class AnchorEditingTests
{
    [TestMethod]
    public void SiliconAnchorUpdate_DoesNotModifySampleAnchors()
    {
        var state = new SessionState();
        state.SetSiliconTrace(CreateTrace([0, 1, 2]));
        state.SetSampleTrace(CreateTrace([10, 11, 12]));
        state.SetSiliconAnchors([new AnchorPoint(400, 1)]);
        state.SetSampleAnchors([new AnchorPoint(400, 11)]);
        var silicon = new SiliconWorkflowViewModel(state);

        silicon.ConfirmAnchor(400, 1.8);

        Assert.AreEqual(2d, state.SiliconAnchors![0].ConfirmedTimeSeconds);
        Assert.AreEqual(11d, state.SampleAnchors![0].ConfirmedTimeSeconds);
    }

    [TestMethod]
    public void SampleAnchorAppend_SnapsAndSortsByWavelength()
    {
        var state = new SessionState();
        state.SetSampleTrace(CreateTrace([10, 11, 12]));
        state.SetSampleAnchors([new AnchorPoint(500, 11)]);
        var sample = new SampleWorkflowViewModel(state);

        sample.ConfirmAnchor(400, 11.8);

        Assert.AreEqual(2, state.SampleAnchors!.Count);
        Assert.AreEqual(400d, state.SampleAnchors[0].WavelengthNm);
        Assert.AreEqual(12d, state.SampleAnchors[0].ConfirmedTimeSeconds);
        Assert.AreEqual(500d, state.SampleAnchors[1].WavelengthNm);
    }

    [TestMethod]
    public void NearestTime_UsesOwningTraceOnly()
    {
        var state = new SessionState();
        state.SetSiliconTrace(CreateTrace([0, 2, 4]));
        state.SetSampleTrace(CreateTrace([100, 200, 300]));
        var silicon = new SiliconWorkflowViewModel(state);
        var sample = new SampleWorkflowViewModel(state);

        Assert.AreEqual(2d, silicon.FindNearestSampleTime(2.4));
        Assert.AreEqual(200d, sample.FindNearestSampleTime(240));
    }

    [TestMethod]
    public void ConfirmAnchor_AllowsAdjustedSnappedTime()
    {
        var state = new SessionState();
        state.SetSampleTrace(CreateTrace([10, 11, 12]));
        var sample = new SampleWorkflowViewModel(state);

        sample.ConfirmAnchor(400, 11.8, adjustedTimeSeconds: 11.5);

        Assert.AreEqual(
            11.5d,
            state.SampleAnchors![0].ConfirmedTimeSeconds);
    }

    private static TraceData CreateTrace(
        IReadOnlyList<double> times) =>
        new(
            times,
            times.Select(time => time + 1).ToArray(),
            TraceMetadata.Unknown);
}
