using IPCE.Core.Domain;
using IPCE.Desktop.ViewModels;
using IPCE.IO.Export;

namespace IPCE.Desktop.Tests;

[TestClass]
public sealed class ReproducibleExportTests
{
    [TestMethod]
    public void SnapshotTables_PreserveExactSettingsAnchorsAndInputMetadata()
    {
        WorkflowExportSnapshot snapshot = new(
            [
                new SettingEntry("SampleArea", "0.36", "cm2"),
                new SettingEntry("SampleDarkRange", "50–60", "s"),
                new SettingEntry("SelectedIpceSource", "External", ""),
            ],
            [
                new AnchorPoint(400, 10),
                new AnchorPoint(500, 20),
            ],
            [],
            [
                new InputMetadataEntry(
                    "SiliconTrace",
                    "silicon.txt",
                    "Time",
                    "Current",
                    "sec/uA",
                    "s/A",
                    ""),
                new InputMetadataEntry(
                    "Spectrum",
                    "spectrum.xlsx",
                    "Wavelength",
                    "Irradiance",
                    "nm/W m^-2 nm^-1",
                    "nm/W m^-2 nm^-1",
                    "Spectra; A/C"),
            ]);

        ExportTable settings =
            WorkflowExportTables.MeasurementSettings(snapshot.Settings);
        ExportTable siliconAnchors =
            WorkflowExportTables.Anchors(
                "SiliconAnchors",
                snapshot.SiliconAnchors);
        ExportTable inputs =
            WorkflowExportTables.InputMetadata(snapshot.Inputs);

        Assert.AreEqual("MeasurementSettings", settings.Name);
        CollectionAssert.AreEqual(
            new object?[] { "SampleArea", "SampleDarkRange",
                "SelectedIpceSource" },
            settings.Columns[0].Values.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { "0.36", "50–60", "External" },
            settings.Columns[1].Values.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { "cm2", "s", "" },
            settings.Columns[2].Values.ToArray());

        Assert.AreEqual("SiliconAnchors", siliconAnchors.Name);
        CollectionAssert.AreEqual(
            new object?[] { 400d, 500d },
            siliconAnchors.Columns[0].Values.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 10d, 20d },
            siliconAnchors.Columns[1].Values.ToArray());

        Assert.AreEqual("InputMetadata", inputs.Name);
        Assert.AreEqual("sec/uA", inputs.Columns[4].Values[0]);
        Assert.AreEqual("s/A", inputs.Columns[5].Values[0]);
        Assert.AreEqual("Spectra; A/C", inputs.Columns[6].Values[1]);
    }

    [TestMethod]
    public void SelectedExport_AppendsReproducibilityTablesWithoutRenamingResults()
    {
        var session = new IPCE.Desktop.State.SessionState();
        session.SetExternalIpce(new ExternalIpceData(
            [
                new IpceValue(400, 20),
                new IpceValue(500, 50),
            ],
            "Wavelength",
            "IPCE"));
        var main = new MainViewModel(session);
        main.Spectrum.IncludePowerDensityExport = false;
        main.Spectrum.IncludeCalculatedIpceExport = false;
        main.Spectrum.IncludeExternalIpceExport = true;
        main.Spectrum.IncludeIntegrationExport = false;

        string[] names = main.Spectrum.BuildSelectedExportTables()
            .Select(table => table.Name)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ExternalIPCE",
                "MeasurementSettings",
                "InputMetadata",
            },
            names);
    }
}
