using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Core.Numerics;

namespace IPCE.Core.Calculation;

public static class IpceCalculator
{
    private const double HcOverQElectronVoltNanometres =
        1239.8419843320026;

    public static IReadOnlyList<PowerDensityPoint> CalculatePowerDensity(
        CalibrationData calibration,
        IReadOnlyList<ExtractedPoint> siliconExtracted,
        double siliconAreaSquareCentimetres)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(siliconExtracted);
        ValidateArea(siliconAreaSquareCentimetres);

        if (siliconExtracted.Count == 0)
        {
            throw new IpceException(
                "IPCE:InvalidSiliconResult",
                "标探提取结果不能为空。");
        }

        double[] wavelengths = siliconExtracted
            .Select(point => point.WavelengthNm)
            .ToArray();
        double minimumCalibration =
            calibration.Points[0].WavelengthNm;
        double maximumCalibration =
            calibration.Points[^1].WavelengthNm;
        if (wavelengths.Any(wavelength =>
                !double.IsFinite(wavelength) ||
                wavelength < minimumCalibration ||
                wavelength > maximumCalibration))
        {
            throw new IpceException(
                "IPCE:CalibrationRange",
                "所选波长超出标探校准范围；程序不会外推响应度。");
        }

        double[] calibrationWavelengths = calibration.Points
            .Select(point => point.WavelengthNm)
            .ToArray();
        double[] calibrationResponsivities = calibration.Points
            .Select(point => point.ResponsivityAmperesPerWatt)
            .ToArray();
        double[] responsivities = Interpolation.Pchip(
            calibrationWavelengths,
            calibrationResponsivities,
            wavelengths);

        if (responsivities.Any(value =>
                !double.IsFinite(value) || value <= 0))
        {
            throw new IpceException(
                "IPCE:InvalidResponsivity",
                "插值得到的标探响应度包含无效值。");
        }

        PowerDensityPoint[] result =
            new PowerDensityPoint[siliconExtracted.Count];
        for (int index = 0; index < result.Length; index++)
        {
            ExtractedPoint extracted = siliconExtracted[index];
            double responsivity = responsivities[index];
            double siliconCollectedPower =
                extracted.AbsolutePhotoCurrentAmperes / responsivity;
            double siliconCollectedPowerStandardError =
                extracted.PhotoCurrentStandardErrorAmperes / responsivity;
            double incidentPowerDensity =
                siliconCollectedPower / siliconAreaSquareCentimetres;
            double incidentPowerDensityStandardError =
                siliconCollectedPowerStandardError /
                siliconAreaSquareCentimetres;

            if (!double.IsFinite(incidentPowerDensity) ||
                incidentPowerDensity <= 0)
            {
                throw new IpceException(
                    "IPCE:InvalidPowerDensity",
                    "反算得到的单色光功率密度包含非正值或无效值。");
            }

            result[index] = new PowerDensityPoint(
                extracted.WavelengthNm,
                responsivity,
                extracted.MeanCurrentAmperes,
                extracted.PhotoCurrentSignedAmperes,
                extracted.AbsolutePhotoCurrentAmperes,
                extracted.PhotoCurrentStandardErrorAmperes,
                siliconAreaSquareCentimetres,
                incidentPowerDensity,
                incidentPowerDensityStandardError,
                extracted.SampleCount);
        }

        return Array.AsReadOnly(result);
    }

    public static IReadOnlyList<IpcePoint> CalculateIpce(
        IReadOnlyList<PowerDensityPoint> powerDensity,
        IReadOnlyList<ExtractedPoint> sampleExtracted,
        double sampleAreaSquareCentimetres)
    {
        ArgumentNullException.ThrowIfNull(powerDensity);
        ArgumentNullException.ThrowIfNull(sampleExtracted);
        ValidateArea(sampleAreaSquareCentimetres);
        ValidatePowerDensity(powerDensity);

        double[] powerWavelengths = powerDensity
            .Select(point => point.WavelengthNm)
            .ToArray();
        double[] powerValues = powerDensity
            .Select(point =>
                point.IncidentPowerDensityWattsPerSquareCentimetre)
            .ToArray();
        double[] powerStandardErrors = powerDensity
            .Select(point => point.IncidentPowerDensityStandardError)
            .ToArray();
        double[] sampleWavelengths = sampleExtracted
            .Select(point => point.WavelengthNm)
            .ToArray();

        if (sampleWavelengths.Any(wavelength =>
                !double.IsFinite(wavelength) ||
                wavelength < powerWavelengths[0] ||
                wavelength > powerWavelengths[^1]))
        {
            throw new IpceException(
                "IPCE:PowerInterpolationRange",
                "样品波长超出标探功率密度范围；程序不会外推。");
        }

        double[] interpolatedPower = Interpolation.Pchip(
            powerWavelengths, powerValues, sampleWavelengths);
        double[] interpolatedPowerStandardError = Interpolation.Linear(
            powerWavelengths,
            powerStandardErrors,
            sampleWavelengths,
            allowExtrapolation: false);
        if (interpolatedPower.Any(value =>
                !double.IsFinite(value) || value <= 0))
        {
            throw new IpceException(
                "IPCE:InvalidInterpolatedPowerDensity",
                "插值到样品波长的入射功率密度包含非正值或无效值。");
        }

        double membershipTolerance = 10 * (
            double.BitIncrement(powerWavelengths.Max(Math.Abs)) -
            powerWavelengths.Max(Math.Abs));
        IpcePoint[] result = new IpcePoint[sampleExtracted.Count];
        for (int index = 0; index < result.Length; index++)
        {
            ExtractedPoint extracted = sampleExtracted[index];
            double samplePhotocurrent =
                extracted.AbsolutePhotoCurrentAmperes;
            double samplePhotocurrentDensity =
                samplePhotocurrent / sampleAreaSquareCentimetres;
            double samplePhotoCurrentDensityStandardError =
                extracted.PhotoCurrentStandardErrorAmperes /
                sampleAreaSquareCentimetres;
            double ipcePercent =
                100 * HcOverQElectronVoltNanometres *
                samplePhotocurrentDensity /
                (interpolatedPower[index] * extracted.WavelengthNm);
            double relativeSilicon =
                interpolatedPowerStandardError[index] /
                interpolatedPower[index];
            double relativeSample =
                samplePhotoCurrentDensityStandardError /
                samplePhotocurrentDensity;
            double ipceEstimatedStandardError =
                ipcePercent * Hypot(relativeSilicon, relativeSample);
            if (!double.IsFinite(ipceEstimatedStandardError))
            {
                ipceEstimatedStandardError = double.NaN;
            }

            bool isInterpolated = !powerWavelengths.Any(wavelength =>
                Math.Abs(extracted.WavelengthNm - wavelength) <=
                membershipTolerance);
            result[index] = new IpcePoint(
                extracted.WavelengthNm,
                interpolatedPower[index],
                interpolatedPowerStandardError[index],
                isInterpolated,
                extracted.MeanCurrentAmperes,
                extracted.PhotoCurrentSignedAmperes,
                samplePhotocurrent,
                extracted.PhotoCurrentStandardErrorAmperes,
                sampleAreaSquareCentimetres,
                samplePhotocurrentDensity,
                samplePhotoCurrentDensityStandardError,
                extracted.SampleCount,
                ipcePercent,
                ipceEstimatedStandardError);
        }

        return Array.AsReadOnly(result);
    }

    private static void ValidatePowerDensity(
        IReadOnlyList<PowerDensityPoint> points)
    {
        if (points.Count < 2)
        {
            throw new IpceException(
                "IPCE:InvalidPowerDensity",
                "功率密度至少需要两个波长点。");
        }

        for (int index = 0; index < points.Count; index++)
        {
            PowerDensityPoint point = points[index];
            if (!double.IsFinite(point.WavelengthNm) ||
                !double.IsFinite(
                    point.IncidentPowerDensityWattsPerSquareCentimetre) ||
                !double.IsFinite(point.IncidentPowerDensityStandardError) ||
                point.WavelengthNm <= 0 ||
                point.IncidentPowerDensityWattsPerSquareCentimetre <= 0 ||
                point.IncidentPowerDensityStandardError < 0 ||
                (index > 0 &&
                    point.WavelengthNm <= points[index - 1].WavelengthNm))
            {
                throw new IpceException(
                    "IPCE:InvalidPowerDensity",
                    "功率密度必须有限、为正且按波长严格递增。");
            }
        }
    }

    private static void ValidateArea(double area)
    {
        if (!double.IsFinite(area) || area <= 0)
        {
            throw new IpceException(
                "IPCE:InvalidArea",
                "照射面积必须为有限正数。");
        }
    }

    private static double Hypot(double left, double right)
    {
        double maximum = Math.Max(Math.Abs(left), Math.Abs(right));
        if (maximum == 0)
        {
            return 0;
        }

        double scaledLeft = left / maximum;
        double scaledRight = right / maximum;
        return maximum * Math.Sqrt(
            scaledLeft * scaledLeft + scaledRight * scaledRight);
    }
}
