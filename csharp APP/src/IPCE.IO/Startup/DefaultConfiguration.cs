namespace IPCE.IO.Startup;

public sealed record DefaultConfiguration
{
    public static DefaultConfiguration Current { get; } = new();

    public string CalibrationFileName { get; init; } =
        "标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx";

    public string SpectrumFileName { get; init; } =
        "标准太阳能光谱数据.xls";

    public string SiliconTraceFileName { get; init; } =
        "Si-i t [300 1100] nm-grating 2-filter.txt";

    public string SiliconAnchorFileName { get; init; } =
        "Si-i t [300 1100] nm-grating 2-filter-time match.txt";

    public bool SubtractDark { get; init; } = true;

    public double SiliconDarkStartSeconds { get; init; } = 0.1;

    public double SiliconDarkEndSeconds { get; init; } = 10;

    public double SampleDarkStartSeconds { get; init; } = 50;

    public double SampleDarkEndSeconds { get; init; } = 60;

    public double SiliconAreaSquareCentimetres { get; init; } = 0.36;

    public double SampleAreaSquareCentimetres { get; init; } = 1;

    public double WavelengthStartNanometres { get; init; } = 300;

    public double WavelengthEndNanometres { get; init; } = 1100;

    public double WavelengthStepNanometres { get; init; } = 5;

    public double NominalDelaySeconds { get; init; } = 8;

    public double PostConfirmationAverageSeconds { get; init; } = 4;

    public double IntegrationStartNanometres { get; init; } = 300;

    public double IntegrationEndNanometres { get; init; } = 1100;

    public string SpectrumWorksheet { get; init; } = "Spectra";

    public int SpectrumWavelengthColumn { get; init; } = 1;

    public int SpectrumIrradianceColumn { get; init; } = 3;
}
