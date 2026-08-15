using IPCE.Core.Errors;
using IPCE.Core.Numerics;

namespace IPCE.Core.Tests;

[TestClass]
public sealed class InterpolationTests
{
    [TestMethod]
    public void Linear_RecoversSourcePointsAndMidpoint()
    {
        double[] result = Interpolation.Linear(
            [0, 10], [0, 20], [0, 5, 10], allowExtrapolation: false);

        AssertClose(0, result[0]);
        AssertClose(10, result[1]);
        AssertClose(20, result[2]);
    }

    [TestMethod]
    public void Linear_ExtrapolatesOnlyWhenExplicitlyEnabled()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            Interpolation.Linear(
                [0, 10], [0, 20], [-5], allowExtrapolation: false));
        Assert.AreEqual("IPCE:InterpolationCoverage", error.Code);

        double[] result = Interpolation.Linear(
            [0, 10], [0, 20], [-5, 15], allowExtrapolation: true);
        AssertClose(-10, result[0]);
        AssertClose(30, result[1]);
    }

    [TestMethod]
    public void Linear_MatchesMatlabBarycentricRounding()
    {
        double result = Interpolation.Linear(
            [830, 905],
            [909, 1035],
            [855],
            allowExtrapolation: false)[0];

        Assert.AreEqual(951.0000000000001, result);
    }

    [TestMethod]
    public void Pchip_MatchesMatlabReferenceOnNonuniformGrid()
    {
        double[] result = Interpolation.Pchip(
            [0, 1, 2.5, 4],
            [0, 2, 1, 3],
            [0, 0.25, 0.5, 1, 1.5, 2.5, 3, 3.75, 4]);
        double[] expected =
        [
            0,
            0.74375,
            1.3833333333333333,
            2,
            1.7407407407407407,
            1,
            1.2592592592592593,
            2.446759259259259,
            3,
        ];

        AssertSameValues(expected, result);
    }

    [TestMethod]
    public void Pchip_MonotoneInput_DoesNotOvershoot()
    {
        double[] result = Interpolation.Pchip(
            [0, 1, 3], [0, 2, 3], [0.25, 0.5, 2, 2.5]);

        Assert.IsTrue(result.All(value => value is >= 0 and <= 3));
        Assert.IsTrue(result.Zip(result.Skip(1), (left, right) =>
            right >= left).All(isMonotone => isMonotone));
    }

    [TestMethod]
    public void Pchip_RejectsQueriesOutsideCoverage()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            Interpolation.Pchip([0, 1, 3], [0, 2, 3], [-0.01]));

        Assert.AreEqual("IPCE:InterpolationCoverage", error.Code);
    }

    [TestMethod]
    public void Interpolation_RejectsInvalidSourceGrid()
    {
        IpceException error = Assert.ThrowsExactly<IpceException>(() =>
            Interpolation.Linear(
                [0, 1, 1],
                [0, 1, 2],
                [0.5],
                allowExtrapolation: false));

        Assert.AreEqual("IPCE:InvalidInterpolationInput", error.Code);
    }

    private static void AssertSameValues(
        IReadOnlyList<double> expected,
        IReadOnlyList<double> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            AssertClose(expected[index], actual[index]);
        }
    }

    private static void AssertClose(
        double expected,
        double actual,
        double relative = 1e-12,
        double absolute = 1e-14)
    {
        double tolerance = Math.Max(absolute, relative * Math.Abs(expected));
        Assert.IsTrue(
            Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
    }
}
