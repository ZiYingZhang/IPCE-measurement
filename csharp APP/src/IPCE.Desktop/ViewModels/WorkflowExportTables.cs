using IPCE.Core.Domain;
using IPCE.IO.Export;

namespace IPCE.Desktop.ViewModels;

public static class WorkflowExportTables
{
    public static ExportTable MeasurementSettings(
        IReadOnlyList<SettingEntry> entries) =>
        new(
            "MeasurementSettings",
            [
                StringColumn("Parameter", entries, item => item.Parameter),
                StringColumn("Value", entries, item => item.Value),
                StringColumn("Unit", entries, item => item.Unit),
            ]);

    public static ExportTable Anchors(
        string tableName,
        IReadOnlyList<AnchorPoint> anchors) =>
        new(
            tableName,
            [
                DoubleColumn("Wavelength_nm", anchors,
                    point => point.WavelengthNm),
                DoubleColumn("ConfirmedTime_s", anchors,
                    point => point.ConfirmedTimeSeconds),
            ]);

    public static ExportTable InputMetadata(
        IReadOnlyList<InputMetadataEntry> entries) =>
        new(
            "InputMetadata",
            [
                StringColumn("Dataset", entries, item => item.Dataset),
                StringColumn("FileName", entries, item => item.FileName),
                StringColumn("Column1Header", entries,
                    item => item.Column1Header),
                StringColumn("Column2Header", entries,
                    item => item.Column2Header),
                StringColumn("SourceUnits", entries,
                    item => item.SourceUnits),
                StringColumn("CanonicalUnits", entries,
                    item => item.CanonicalUnits),
                StringColumn("Selection", entries,
                    item => item.Selection),
            ]);

    public static ExportTable PowerDensity(
        IReadOnlyList<PowerDensityPoint> points) =>
        new(
            "SiPowerDensity",
            [
                DoubleColumn("Wavelength_nm", points, p => p.WavelengthNm),
                DoubleColumn("SiResponsivity_A_per_W", points,
                    p => p.SiliconResponsivityAmperesPerWatt),
                DoubleColumn("SiMeanCurrent_A", points,
                    p => p.SiliconMeanCurrentAmperes),
                DoubleColumn("SiPhotoCurrentSigned_A", points,
                    p => p.SiliconPhotoCurrentSignedAmperes),
                DoubleColumn("SiPhotocurrent_A", points,
                    p => p.SiliconPhotocurrentAmperes),
                DoubleColumn("SiPhotoCurrentSE_A", points,
                    p => p.SiliconPhotoCurrentStandardErrorAmperes),
                DoubleColumn("SiliconIlluminatedArea_cm2", points,
                    p => p.SiliconIlluminatedAreaSquareCentimetres),
                DoubleColumn("IncidentPowerDensity_W_cm2", points,
                    p => p.IncidentPowerDensityWattsPerSquareCentimetre),
                DoubleColumn("IncidentPowerDensitySE_W_cm2", points,
                    p => p.IncidentPowerDensityStandardError),
                IntColumn("SiSampleCount", points, p => p.SampleCount),
            ]);

    public static ExportTable CalculatedIpce(
        IReadOnlyList<IpcePoint> points) =>
        new(
            "SampleIPCE",
            [
                DoubleColumn("Wavelength_nm", points, p => p.WavelengthNm),
                DoubleColumn("IncidentPowerDensity_W_cm2", points,
                    p => p.IncidentPowerDensityWattsPerSquareCentimetre),
                DoubleColumn("IncidentPowerDensitySE_W_cm2", points,
                    p => p.IncidentPowerDensityStandardError),
                BoolColumn("PowerDensityInterpolated", points,
                    p => p.PowerDensityInterpolated),
                DoubleColumn("SampleMeanCurrent_A", points,
                    p => p.SampleMeanCurrentAmperes),
                DoubleColumn("SamplePhotoCurrentSigned_A", points,
                    p => p.SamplePhotoCurrentSignedAmperes),
                DoubleColumn("SamplePhotocurrent_A", points,
                    p => p.SamplePhotocurrentAmperes),
                DoubleColumn("SamplePhotoCurrentSE_A", points,
                    p => p.SamplePhotoCurrentStandardErrorAmperes),
                DoubleColumn("SampleIlluminatedArea_cm2", points,
                    p => p.SampleIlluminatedAreaSquareCentimetres),
                DoubleColumn("SamplePhotocurrentDensity_A_cm2", points,
                    p => p.SamplePhotocurrentDensityAmperesPerSquareCentimetre),
                DoubleColumn("SamplePhotoCurrentDensitySE_A_cm2", points,
                    p => p.SamplePhotoCurrentDensityStandardError),
                IntColumn("SampleSampleCount", points, p => p.SampleCount),
                DoubleColumn("IPCE_percent", points, p => p.IpcePercent),
                DoubleColumn("IPCE_EstimatedSE_percent", points,
                    p => p.IpceEstimatedStandardErrorPercent),
            ]);

    public static ExportTable ExternalIpce(ExternalIpceData data) =>
        new(
            "ExternalIPCE",
            [
                DoubleColumn("Wavelength_nm", data.Points,
                    p => p.WavelengthNm),
                DoubleColumn("IPCE_percent", data.Points,
                    p => p.IpcePercent),
            ]);

    public static ExportTable SpectrumSummary(IntegrationSummary summary) =>
        new(
            "SpectrumSummary",
            [
                Scalar("MinimumWavelength_nm",
                    summary.MinimumWavelengthNm),
                Scalar("MaximumWavelength_nm",
                    summary.MaximumWavelengthNm),
                Scalar("IntegratedCurrentDensity_mA_cm2",
                    summary.IntegratedCurrentDensityMilliamperePerSquareCentimetre),
                Scalar("IntegratedPower_W_m2",
                    summary.IntegratedPowerWattsPerSquareMetre),
                new ExportColumn(
                    "IntegrationGridPoints",
                    typeof(int),
                    [summary.IntegrationGridPoints]),
                new ExportColumn(
                    "Interpolation",
                    typeof(string),
                    [summary.Interpolation]),
            ]);

    public static ExportTable SpectrumCurve(
        IReadOnlyList<IntegrationCurvePoint> points) =>
        new(
            "SpectrumCurve",
            [
                DoubleColumn("Wavelength_nm", points, p => p.WavelengthNm),
                DoubleColumn("Irradiance_W_m2_nm", points,
                    p => p.IrradianceWattsPerSquareMetrePerNanometre),
                DoubleColumn("IPCE_percent", points, p => p.IpcePercent),
                DoubleColumn("EQE_fraction", points, p => p.EqeFraction),
                DoubleColumn("PhotonFlux_m2_s_nm", points,
                    p => p.PhotonFluxPerSquareMetreSecondNanometre),
                DoubleColumn("SpectralCurrent_mA_cm2_nm", points,
                    p => p.SpectralCurrentMilliamperePerSquareCentimetreNanometre),
                DoubleColumn("CumulativeCurrentDensity_mA_cm2", points,
                    p => p.CumulativeCurrentDensityMilliamperePerSquareCentimetre),
            ]);

    private static ExportColumn DoubleColumn<T>(
        string name,
        IReadOnlyList<T> points,
        Func<T, double> selector) =>
        new(
            name,
            typeof(double),
            points.Select(point => (object?)selector(point)).ToArray());

    private static ExportColumn IntColumn<T>(
        string name,
        IReadOnlyList<T> points,
        Func<T, int> selector) =>
        new(
            name,
            typeof(int),
            points.Select(point => (object?)selector(point)).ToArray());

    private static ExportColumn BoolColumn<T>(
        string name,
        IReadOnlyList<T> points,
        Func<T, bool> selector) =>
        new(
            name,
            typeof(bool),
            points.Select(point => (object?)selector(point)).ToArray());

    private static ExportColumn StringColumn<T>(
        string name,
        IReadOnlyList<T> points,
        Func<T, string> selector) =>
        new(
            name,
            typeof(string),
            points.Select(point => (object?)selector(point)).ToArray());

    private static ExportColumn Scalar(string name, double value) =>
        new(name, typeof(double), [value]);
}
