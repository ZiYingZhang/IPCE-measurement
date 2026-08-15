using IPCE.Core.Errors;
using IPCE.Core.Numerics;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class IntegrationPrimitiveTests
{
    [TestMethod]
    public void Integrate_YEqualsX_ReturnsTwo()
    {
        double area = TrapezoidalIntegration.Integrate(
            [0, 1, 2], [0, 1, 2]);

        Assert.AreEqual(2, area, 1e-15);
    }

    [TestMethod]
    public void Cumulative_FinalValueEqualsTotal()
    {
        double[] cumulative = TrapezoidalIntegration.Cumulative(
            [0, 0.5, 2], [2, 4, 3]);
        double total = TrapezoidalIntegration.Integrate(
            [0, 0.5, 2], [2, 4, 3]);

        Assert.AreEqual(0, cumulative[0]);
        Assert.AreEqual(total, cumulative[^1], 1e-15);
    }

    [TestMethod]
    public void Integration_RejectsNonIncreasingGridWithStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TrapezoidalIntegration.Integrate(
                [0, 1, 1], [0, 1, 2]));

        Assert.AreEqual("IPCE:InvalidIntegrationGrid", error.Code);
    }

    [TestMethod]
    public void Integration_RejectsMismatchedLengthsWithStableCode()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            TrapezoidalIntegration.Cumulative(
                [0, 1], [0]));

        Assert.AreEqual("IPCE:InvalidIntegrationGrid", error.Code);
    }
}
