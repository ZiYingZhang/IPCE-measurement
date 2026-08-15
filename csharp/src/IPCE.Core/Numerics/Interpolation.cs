using IPCE.Core.Errors;

namespace IPCE.Core.Numerics;

public static class Interpolation
{
    public static double[] Linear(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        ReadOnlySpan<double> query,
        bool allowExtrapolation)
    {
        ValidateSource(x, y, query);
        ValidateCoverage(x, query, allowExtrapolation);

        double[] result = new double[query.Length];
        for (int index = 0; index < query.Length; index++)
        {
            int interval = FindInterval(x, query[index]);
            double fraction =
                (query[index] - x[interval]) /
                (x[interval + 1] - x[interval]);
            result[index] =
                (1 - fraction) * y[interval] +
                fraction * y[interval + 1];
        }

        return result;
    }

    public static double[] Pchip(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        ReadOnlySpan<double> query)
    {
        ValidateSource(x, y, query);
        ValidateCoverage(x, query, allowExtrapolation: false);

        double[] slopes = CalculatePchipSlopes(x, y);
        double[] result = new double[query.Length];

        for (int index = 0; index < query.Length; index++)
        {
            int interval = FindInterval(x, query[index]);
            double width = x[interval + 1] - x[interval];
            double secant = (y[interval + 1] - y[interval]) / width;
            double cubicCoefficient =
                (slopes[interval] - 2 * secant + slopes[interval + 1]) /
                (width * width);
            double quadraticCoefficient =
                (3 * secant - 2 * slopes[interval] - slopes[interval + 1]) /
                width;
            double offset = query[index] - x[interval];

            result[index] = y[interval] + offset *
                (slopes[interval] + offset *
                    (quadraticCoefficient + offset * cubicCoefficient));
        }

        return result;
    }

    private static double[] CalculatePchipSlopes(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y)
    {
        int pointCount = x.Length;
        double[] widths = new double[pointCount - 1];
        double[] secants = new double[pointCount - 1];

        for (int index = 0; index < pointCount - 1; index++)
        {
            widths[index] = x[index + 1] - x[index];
            secants[index] = (y[index + 1] - y[index]) / widths[index];
        }

        double[] slopes = new double[pointCount];
        if (pointCount == 2)
        {
            slopes[0] = secants[0];
            slopes[1] = secants[0];
            return slopes;
        }

        slopes[0] = CalculateEndpointSlope(
            widths[0], widths[1], secants[0], secants[1]);
        slopes[^1] = CalculateEndpointSlope(
            widths[^1], widths[^2], secants[^1], secants[^2]);

        for (int index = 1; index < pointCount - 1; index++)
        {
            double leftSecant = secants[index - 1];
            double rightSecant = secants[index];
            if (leftSecant == 0 ||
                rightSecant == 0 ||
                Math.Sign(leftSecant) != Math.Sign(rightSecant))
            {
                slopes[index] = 0;
                continue;
            }

            double leftWeight = 2 * widths[index] + widths[index - 1];
            double rightWeight = widths[index] + 2 * widths[index - 1];
            slopes[index] =
                (leftWeight + rightWeight) /
                (leftWeight / leftSecant + rightWeight / rightSecant);
        }

        return slopes;
    }

    private static double CalculateEndpointSlope(
        double adjacentWidth,
        double nextWidth,
        double adjacentSecant,
        double nextSecant)
    {
        double slope =
            ((2 * adjacentWidth + nextWidth) * adjacentSecant -
                adjacentWidth * nextSecant) /
            (adjacentWidth + nextWidth);

        if (Math.Sign(slope) != Math.Sign(adjacentSecant))
        {
            return 0;
        }

        if (Math.Sign(adjacentSecant) != Math.Sign(nextSecant) &&
            Math.Abs(slope) > Math.Abs(3 * adjacentSecant))
        {
            return 3 * adjacentSecant;
        }

        return slope;
    }

    private static void ValidateSource(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        ReadOnlySpan<double> query)
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

        for (int index = 0; index < query.Length && !invalid; index++)
        {
            invalid = !double.IsFinite(query[index]);
        }

        if (invalid)
        {
            throw new IpceException(
                "IPCE:InvalidInterpolationInput",
                "插值源数据必须有限、长度一致，并按 x 严格递增排列。");
        }
    }

    private static void ValidateCoverage(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> query,
        bool allowExtrapolation)
    {
        if (allowExtrapolation)
        {
            return;
        }

        for (int index = 0; index < query.Length; index++)
        {
            if (query[index] < x[0] || query[index] > x[^1])
            {
                throw new IpceException(
                    "IPCE:InterpolationCoverage",
                    "查询点超出插值数据覆盖范围；程序不会外推。");
            }
        }
    }

    private static int FindInterval(
        ReadOnlySpan<double> x,
        double query)
    {
        if (query <= x[0])
        {
            return 0;
        }

        if (query >= x[^1])
        {
            return x.Length - 2;
        }

        int lower = 0;
        int upper = x.Length - 1;
        while (upper - lower > 1)
        {
            int middle = lower + (upper - lower) / 2;
            if (query < x[middle])
            {
                upper = middle;
            }
            else
            {
                lower = middle;
            }
        }

        return lower;
    }
}
