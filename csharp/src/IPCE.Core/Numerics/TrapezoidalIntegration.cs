using IPCE.Core.Errors;

namespace IPCE.Core.Numerics;

public static class TrapezoidalIntegration
{
    public static double Integrate(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y)
    {
        ValidateGrid(x, y);

        double total = 0;
        for (int index = 1; index < x.Length; index++)
        {
            total += 0.5 *
                (x[index] - x[index - 1]) *
                (y[index] + y[index - 1]);
        }

        return total;
    }

    public static double[] Cumulative(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y)
    {
        ValidateGrid(x, y);

        double[] cumulative = new double[x.Length];
        for (int index = 1; index < x.Length; index++)
        {
            cumulative[index] = cumulative[index - 1] + 0.5 *
                (x[index] - x[index - 1]) *
                (y[index] + y[index - 1]);
        }

        return cumulative;
    }

    private static void ValidateGrid(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y)
    {
        bool invalid =
            x.Length < 2 ||
            x.Length != y.Length;

        for (int index = 0; index < x.Length && !invalid; index++)
        {
            invalid =
                !double.IsFinite(x[index]) ||
                !double.IsFinite(y[index]) ||
                (index > 0 && x[index] <= x[index - 1]);
        }

        if (invalid)
        {
            throw new IpceException(
                "IPCE:InvalidIntegrationGrid",
                "积分网格必须至少包含两个有限、x 严格递增且长度一致的数据点。");
        }
    }
}
