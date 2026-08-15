using IPCE.Core.Calculation;
using IPCE.Core.Domain;
using IPCE.Core.Errors;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class IpceSourceResolverTests
{
    [TestMethod]
    public void CalculatedSource_ReturnsCalculatedValues()
    {
        IpcePoint[] calculated =
        [
            Point(400, 20),
            Point(500, 50),
        ];

        IReadOnlyList<IpceValue> result = IpceSourceResolver.Resolve(
            calculated, null, IpceSource.Calculated);

        CollectionAssert.AreEqual(
            new[] { new IpceValue(400, 20), new IpceValue(500, 50) },
            result.ToArray());
    }

    [TestMethod]
    public void ExternalSource_PreservesOneHundredTwentyPercent()
    {
        var external = new ExternalIpceData(
            [new IpceValue(400, 50), new IpceValue(500, 120)],
            "Wavelength/nm",
            "IPCE/%");

        IReadOnlyList<IpceValue> result = IpceSourceResolver.Resolve(
            null, external, IpceSource.External);

        Assert.AreEqual(120, result[1].IpcePercent);
    }

    [TestMethod]
    public void MissingCalculatedSource_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceSourceResolver.Resolve(null, null, IpceSource.Calculated));

        Assert.AreEqual("IPCE:MissingCalculatedIPCE", error.Code);
    }

    [TestMethod]
    public void MissingExternalSource_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceSourceResolver.Resolve(null, null, IpceSource.External));

        Assert.AreEqual("IPCE:MissingExternalIPCE", error.Code);
    }

    [TestMethod]
    public void UnknownSource_ThrowsStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            IpceSourceResolver.Resolve(null, null, (IpceSource)99));

        Assert.AreEqual("IPCE:UnknownIPCESource", error.Code);
    }

    private static IpcePoint Point(double wavelength, double ipce)
    {
        return new IpcePoint(
            wavelength, 1, 0, false, 1, 1, 1, 0, 1, 1, 0, 1, ipce, 0);
    }
}
