using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class AnchorTableEditingTests
{
    [TestMethod]
    public void ReplaceAnchors_ValidatesThenSortsSessionState()
    {
        var state = new SessionState();
        state.SetSiliconAnchors(
        [
            new AnchorPoint(500, 10),
            new AnchorPoint(400, 5),
        ]);
        var viewModel = new SiliconWorkflowViewModel(state);
        viewModel.EditableAnchors[0].ConfirmedTimeSeconds = 12;

        viewModel.ReplaceAnchors(viewModel.EditableAnchors);

        CollectionAssert.AreEqual(
            new[] { 400d, 500d },
            state.SiliconAnchors!
                .Select(anchor => anchor.WavelengthNm)
                .ToArray());
        Assert.AreEqual(
            12d,
            state.SiliconAnchors!.Single(
                anchor => anchor.WavelengthNm == 500)
                .ConfirmedTimeSeconds);
    }

    [TestMethod]
    public void InvalidDuplicate_RollsBackSessionAndEditableRows()
    {
        var state = new SessionState();
        state.SetSiliconAnchors(
        [
            new AnchorPoint(400, 5),
            new AnchorPoint(500, 10),
        ]);
        var viewModel = new SiliconWorkflowViewModel(state);
        AnchorPoint[] prior = state.SiliconAnchors!.ToArray();
        viewModel.EditableAnchors[1].WavelengthNm = 400;

        IpceException exception = Assert.ThrowsExactly<IpceException>(
            () => viewModel.ReplaceAnchors(
                viewModel.EditableAnchors));

        Assert.AreEqual("IPCE:InvalidAnchorFile", exception.Code);
        CollectionAssert.AreEqual(
            prior,
            state.SiliconAnchors!.ToArray());
        CollectionAssert.AreEqual(
            new[] { 400d, 500d },
            viewModel.EditableAnchors
                .Select(row => row.WavelengthNm)
                .ToArray());
    }

    [TestMethod]
    public void DeleteAndEdit_RemainIsolatedByAnchorOwner()
    {
        var state = new SessionState();
        state.SetSiliconAnchors(
        [
            new AnchorPoint(400, 5),
            new AnchorPoint(500, 10),
        ]);
        state.SetSampleAnchors(
        [
            new AnchorPoint(400, 50),
            new AnchorPoint(500, 60),
        ]);
        var silicon = new SiliconWorkflowViewModel(state);
        var sample = new SampleWorkflowViewModel(state);

        silicon.DeleteAnchor(silicon.EditableAnchors[0]);
        sample.EditableAnchors[0].ConfirmedTimeSeconds = 55;
        sample.ReplaceAnchors(sample.EditableAnchors);

        Assert.AreEqual(1, state.SiliconAnchors!.Count);
        Assert.AreEqual(500d, state.SiliconAnchors[0].WavelengthNm);
        Assert.AreEqual(2, state.SampleAnchors!.Count);
        Assert.AreEqual(55d, state.SampleAnchors[0].ConfirmedTimeSeconds);
    }
}
