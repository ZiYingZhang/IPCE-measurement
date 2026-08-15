using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Numerics;

namespace IPCE.Core.Calculation;

public static class SpectrumIntegrator
{
    private const double Planck = 6.62607015e-34;
    private const double SpeedOfLight = 299792458.0;
    private const double ElementaryCharge = 1.602176634e-19;

    public static IntegrationResult Integrate(
        IReadOnlyList<IpceValue> ipce,
        IReadOnlyList<SpectrumPoint> spectrum,
        double minimumWavelengthNm,
        double maximumWavelengthNm)
    {
        ArgumentNullException.ThrowIfNull(ipce);
        ArgumentNullException.ThrowIfNull(spectrum);
        ValidateIpce(ipce);
        ValidateSpectrum(spectrum);

        if (!double.IsFinite(minimumWavelengthNm) ||
            !double.IsFinite(maximumWavelengthNm) ||
            minimumWavelengthNm <= 0 ||
            maximumWavelengthNm <= 0 ||
            minimumWavelengthNm >= maximumWavelengthNm)
        {
            throw new IpceException(
                "IPCE:InvalidIntegrationRange",
                "积分终止波长必须大于起始波长。");
        }

        double lowerCoverage = Math.Max(
            ipce[0].WavelengthNm,
            spectrum[0].WavelengthNm);
        double upperCoverage = Math.Min(
            ipce[^1].WavelengthNm,
            spectrum[^1].WavelengthNm);
        if (minimumWavelengthNm < lowerCoverage ||
            maximumWavelengthNm > upperCoverage)
        {
            throw new IpceException(
                "IPCE:IntegrationCoverage",
                "积分范围超出 IPCE 与光谱共同覆盖范围；程序不会外推。");
        }

        double[] wavelength = spectrum
            .Where(point =>
                point.WavelengthNm > minimumWavelengthNm &&
                point.WavelengthNm < maximumWavelengthNm)
            .Select(point => point.WavelengthNm)
            .Prepend(minimumWavelengthNm)
            .Append(maximumWavelengthNm)
            .Distinct()
            .Order()
            .ToArray();
        double[] spectrumWavelength = spectrum
            .Select(point => point.WavelengthNm)
            .ToArray();
        double[] irradianceSource = spectrum
            .Select(point =>
                point.IrradianceWattsPerSquareMetrePerNanometre)
            .ToArray();
        double[] ipceWavelength = ipce
            .Select(point => point.WavelengthNm)
            .ToArray();
        double[] ipceSource = ipce
            .Select(point => point.IpcePercent)
            .ToArray();
        double[] irradiance = Interpolation.Linear(
            spectrumWavelength,
            irradianceSource,
            wavelength,
            allowExtrapolation: false);
        double[] ipcePercent = Interpolation.Pchip(
            ipceWavelength,
            ipceSource,
            wavelength);

        if (irradiance.Any(value => !double.IsFinite(value)) ||
            ipcePercent.Any(value => !double.IsFinite(value)))
        {
            throw new IpceException(
                "IPCE:IntegrationInterpolation",
                "光谱或 IPCE 插值产生了无效值。");
        }

        double[] eqeFraction = new double[wavelength.Length];
        double[] photonFlux = new double[wavelength.Length];
        double[] spectralCurrentAmperesPerSquareMetreNanometre =
            new double[wavelength.Length];
        for (int index = 0; index < wavelength.Length; index++)
        {
            double wavelengthMetres = wavelength[index] * 1e-9;
            eqeFraction[index] = ipcePercent[index] / 100;
            photonFlux[index] =
                irradiance[index] * wavelengthMetres /
                (Planck * SpeedOfLight);
            spectralCurrentAmperesPerSquareMetreNanometre[index] =
                ElementaryCharge * photonFlux[index] * eqeFraction[index];
        }

        double currentDensityAmperesPerSquareMetre =
            TrapezoidalIntegration.Integrate(
                wavelength,
                spectralCurrentAmperesPerSquareMetreNanometre);
        double currentDensityMilliamperePerSquareCentimetre =
            0.1 * currentDensityAmperesPerSquareMetre;
        double[] cumulativeAmperesPerSquareMetre =
            TrapezoidalIntegration.Cumulative(
                wavelength,
                spectralCurrentAmperesPerSquareMetreNanometre);
        double integratedPower = TrapezoidalIntegration.Integrate(
            wavelength, irradiance);

        IntegrationCurvePoint[] curve =
            new IntegrationCurvePoint[wavelength.Length];
        for (int index = 0; index < curve.Length; index++)
        {
            curve[index] = new IntegrationCurvePoint(
                wavelength[index],
                irradiance[index],
                ipcePercent[index],
                eqeFraction[index],
                photonFlux[index],
                0.1 *
                    spectralCurrentAmperesPerSquareMetreNanometre[index],
                0.1 * cumulativeAmperesPerSquareMetre[index]);
        }

        var summary = new IntegrationSummary(
            minimumWavelengthNm,
            maximumWavelengthNm,
            currentDensityMilliamperePerSquareCentimetre,
            integratedPower,
            wavelength.Length,
            "pchip(IPCE) + linear(spectrum)");
        return new IntegrationResult(summary, curve);
    }

    private static void ValidateIpce(IReadOnlyList<IpceValue> ipce)
    {
        if (ipce.Count < 2)
        {
            throw new IpceException(
                "IPCE:InvalidIPCEResult",
                "IPCE 结果至少需要两个数据点。");
        }

        for (int index = 0; index < ipce.Count; index++)
        {
            if (!double.IsFinite(ipce[index].WavelengthNm) ||
                !double.IsFinite(ipce[index].IpcePercent) ||
                ipce[index].WavelengthNm <= 0 ||
                (index > 0 &&
                    ipce[index].WavelengthNm <= ipce[index - 1].WavelengthNm))
            {
                throw new IpceException(
                    "IPCE:InvalidIPCEResult",
                    "IPCE 数据必须有限并按波长严格递增。");
            }
        }
    }

    private static void ValidateSpectrum(IReadOnlyList<SpectrumPoint> spectrum)
    {
        if (spectrum.Count < 2)
        {
            throw new IpceException(
                "IPCE:InvalidSpectrum",
                "光谱至少需要两个数据点。");
        }

        for (int index = 0; index < spectrum.Count; index++)
        {
            SpectrumPoint point = spectrum[index];
            if (!double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(
                    point.IrradianceWattsPerSquareMetrePerNanometre) ||
                point.WavelengthNm <= 0 ||
                point.IrradianceWattsPerSquareMetrePerNanometre < 0 ||
                (index > 0 &&
                    point.WavelengthNm <= spectrum[index - 1].WavelengthNm))
            {
                throw new IpceException(
                    "IPCE:InvalidSpectrum",
                    "光谱数据必须有限、非负并按波长严格递增。");
            }
        }
    }
}
