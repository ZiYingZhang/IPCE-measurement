using IPCE.Core.Errors;
using IPCE.IO.Import;

namespace IPCE.IO.Tests;

[TestClass]
public sealed class AnchorReaderTests
{
    [TestMethod]
    public void OptionalHeaderAndWhitespaceData_ReturnTwoColumns()
    {
        using var file = new TemporaryTextFile(
            "Wavelength_nm ConfirmedTime_s\n370  127\n400  168\n");

        var anchors = AnchorReader.Read(file.Path);

        Assert.AreEqual(2, anchors.Count);
        Assert.AreEqual(370, anchors[0].WavelengthNm);
        Assert.AreEqual(127, anchors[0].ConfirmedTimeSeconds);
        Assert.AreEqual(400, anchors[1].WavelengthNm);
    }

    [TestMethod]
    public void DuplicateWavelengths_ThrowStableCode()
    {
        using var file = new TemporaryTextFile(
            "400,10\n400,20\n");

        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            AnchorReader.Read(file.Path));

        Assert.AreEqual("IPCE:InvalidAnchorFile", error.Code);
    }
}
