using IPCE.Core.Errors;
using IPCE.IO.Import;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class ItTraceReaderTests
{
    [TestMethod]
    [DataRow("ms", 1e-3)]
    [DataRow("min", 60.0)]
    [DataRow("h", 3600.0)]
    public void TimeUnits_ConvertToSeconds(string unit, double factor)
    {
        using var file = new TemporaryTextFile(
            $"Time/{unit},Current/A\n0,1\n1,2\n");

        var trace = ItTraceReader.Read(file.Path);

        Assert.AreEqual(factor, trace.TimeSeconds[1], 1e-15);
        Assert.AreEqual(factor, trace.Metadata.TimeToSecondsFactor, 1e-15);
    }

    [TestMethod]
    [DataRow("mA", 1e-3)]
    [DataRow("uA", 1e-6)]
    [DataRow("µA", 1e-6)]
    [DataRow("μA", 1e-6)]
    [DataRow("nA", 1e-9)]
    [DataRow("pA", 1e-12)]
    public void CurrentUnits_ConvertToAmperes(string unit, double factor)
    {
        using var file = new TemporaryTextFile(
            $"Time/s,Current/{unit}\n0,1\n1,2\n");

        var trace = ItTraceReader.Read(file.Path);

        Assert.AreEqual(factor, trace.CurrentAmperes[0], 1e-18);
        Assert.AreEqual(
            factor,
            trace.Metadata.CurrentToAmperesFactor,
            1e-18);
    }

    [TestMethod]
    public void MissingUnits_RequireExplicitOverrides()
    {
        using var file = new TemporaryTextFile(
            "time,current\n0,1\n1,2\n");

        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            ItTraceReader.Read(file.Path));

        Assert.AreEqual("IPCE:TraceUnitsRequired", error.Code);
    }

    [TestMethod]
    public void InspectAndRead_CoverSecondsAliasesAndMissingUnits()
    {
        using var secondsFile = new TemporaryTextFile(
            "Time/s,Current/A\n0,1\n1,2\n");
        using var secFile = new TemporaryTextFile(
            "Time/sec,Current/uA\n0,1\n1,2\n");
        using var secondFile = new TemporaryTextFile(
            "Time/second,Current/A\n0,1\n1,2\n");
        using var missingFile = new TemporaryTextFile(
            "elapsed,signal\n0,1\n1,2\n");

        TraceImportInspection detected =
            ItTraceReader.Inspect(secFile.Path);
        TraceImportInspection missing =
            ItTraceReader.Inspect(missingFile.Path);
        var secondsTrace = ItTraceReader.Read(secondsFile.Path);
        var secTrace = ItTraceReader.Read(secFile.Path);
        var secondTrace = ItTraceReader.Read(secondFile.Path);

        Assert.AreEqual("Time/sec", detected.TimeHeader);
        Assert.AreEqual("Current/uA", detected.CurrentHeader);
        Assert.AreEqual("sec", detected.DetectedTimeUnit);
        Assert.AreEqual("uA", detected.DetectedCurrentUnit);
        Assert.AreEqual("elapsed", missing.TimeHeader);
        Assert.AreEqual("signal", missing.CurrentHeader);
        Assert.AreEqual("", missing.DetectedTimeUnit);
        Assert.AreEqual("", missing.DetectedCurrentUnit);
        Assert.AreEqual(1d, secondsTrace.TimeSeconds[1]);
        Assert.AreEqual(1d, secondsTrace.Metadata.TimeToSecondsFactor);
        Assert.AreEqual("s", secondsTrace.Metadata.OriginalTimeUnit);
        Assert.AreEqual(1d, secTrace.TimeSeconds[1]);
        Assert.AreEqual(1d, secTrace.Metadata.TimeToSecondsFactor);
        Assert.AreEqual("sec", secTrace.Metadata.OriginalTimeUnit);
        Assert.AreEqual(1d, secondTrace.TimeSeconds[1]);
        Assert.AreEqual(1d, secondTrace.Metadata.TimeToSecondsFactor);
        Assert.AreEqual("second", secondTrace.Metadata.OriginalTimeUnit);
    }

    [TestMethod]
    public void Overrides_MinutesAndMicroamperes_AreApplied()
    {
        using var file = new TemporaryTextFile(
            "time,current\n0,1\n1,2\n");

        var trace = ItTraceReader.Read(
            file.Path,
            new UnitOverrides("min", "uA"));

        CollectionAssert.AreEqual(
            new[] { 0d, 60d },
            trace.TimeSeconds.ToArray());
        CollectionAssert.AreEqual(
            new[] { 1e-6, 2e-6 },
            trace.CurrentAmperes.ToArray());
    }

    [TestMethod]
    public void HeaderMetadataAndRawText_AreRetained()
    {
        using var file = new TemporaryTextFile(
            "Instrument: test\nTime/ms, Current/mA\n0,1\n1000,2\n");

        var trace = ItTraceReader.Read(file.Path);

        Assert.AreEqual("Time/ms", trace.Metadata.TimeHeader);
        Assert.AreEqual("Current/mA", trace.Metadata.CurrentHeader);
        Assert.AreEqual("ms", trace.Metadata.OriginalTimeUnit);
        Assert.AreEqual("mA", trace.Metadata.OriginalCurrentUnit);
        StringAssert.Contains(trace.Metadata.RawHeaderText, "Instrument: test");
    }

    [TestMethod]
    public void UnsortedTimes_AreSortedWithTheirCurrents()
    {
        using var file = new TemporaryTextFile(
            "Time/s,Current/A\n2,20\n0,10\n1,15\n");

        var trace = ItTraceReader.Read(file.Path);

        CollectionAssert.AreEqual(
            new[] { 0d, 1d, 2d },
            trace.TimeSeconds.ToArray());
        CollectionAssert.AreEqual(
            new[] { 10d, 15d, 20d },
            trace.CurrentAmperes.ToArray());
    }
}
