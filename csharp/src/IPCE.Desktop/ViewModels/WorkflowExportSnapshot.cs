using System.Globalization;
using IPCE.Core.Domain;

namespace IPCE.Desktop.ViewModels;

public sealed record SettingEntry(
    string Parameter,
    string Value,
    string Unit);

public sealed record InputMetadataEntry(
    string Dataset,
    string FileName,
    string Column1Header,
    string Column2Header,
    string SourceUnits,
    string CanonicalUnits,
    string Selection);

public sealed record WorkflowExportSnapshot(
    IReadOnlyList<SettingEntry> Settings,
    IReadOnlyList<AnchorPoint> SiliconAnchors,
    IReadOnlyList<AnchorPoint> SampleAnchors,
    IReadOnlyList<InputMetadataEntry> Inputs)
{
    public static WorkflowExportSnapshot Build(
        SiliconWorkflowViewModel? silicon,
        SampleWorkflowViewModel? sample,
        SpectrumWorkflowViewModel spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        List<SettingEntry> settings = [];
        if (silicon is not null)
        {
            AddMeasurementSettings(
                settings,
                "Silicon",
                silicon.WavelengthStartNanometres,
                silicon.WavelengthEndNanometres,
                silicon.WavelengthStepNanometres,
                silicon.AlignmentMode,
                silicon.FixedStartTimeSeconds,
                silicon.NominalDelaySeconds,
                silicon.AveragingDurationSeconds,
                silicon.SubtractDark,
                silicon.DarkStartSeconds,
                silicon.DarkEndSeconds,
                silicon.AreaSquareCentimetres);
        }
        if (sample is not null)
        {
            AddMeasurementSettings(
                settings,
                "Sample",
                sample.WavelengthStartNanometres,
                sample.WavelengthEndNanometres,
                sample.WavelengthStepNanometres,
                sample.AlignmentMode,
                sample.FixedStartTimeSeconds,
                sample.NominalDelaySeconds,
                sample.AveragingDurationSeconds,
                sample.SubtractDark,
                sample.DarkStartSeconds,
                sample.DarkEndSeconds,
                sample.AreaSquareCentimetres);
        }

        settings.Add(new SettingEntry(
            "IntegrationRange",
            $"{Number(spectrum.IntegrationMinimumNanometres)}–" +
            Number(spectrum.IntegrationMaximumNanometres),
            "nm"));
        settings.Add(new SettingEntry(
            "SelectedIpceSource",
            spectrum.SelectedIpceSource.ToString(),
            ""));
        AddStatus(
            settings,
            "PowerDensity",
            spectrum.Session.PowerDensityStatus);
        AddStatus(
            settings,
            "CalculatedIpce",
            spectrum.Session.CalculatedIpceStatus);
        AddStatus(
            settings,
            "Integration",
            spectrum.Session.IntegrationStatus);

        List<InputMetadataEntry> inputs = [];
        AddTraceInput(
            inputs,
            "SiliconTrace",
            silicon?.TraceFileName ?? "",
            silicon?.Trace);
        AddTraceInput(
            inputs,
            "SampleTrace",
            sample?.TraceFileName ?? "",
            sample?.Trace);

        if (spectrum.ExternalIpce is { } external)
        {
            inputs.Add(new InputMetadataEntry(
                "ExternalIpce",
                spectrum.ExternalIpceFileName,
                external.WavelengthHeader,
                external.IpceHeader,
                "nm/%",
                "nm/%",
                spectrum.SelectedIpceSource == IpceSource.External
                    ? "Selected"
                    : ""));
        }

        if (spectrum.Spectrum is { Count: > 0 })
        {
            var metadata = spectrum.SpectrumImportMetadata;
            inputs.Add(new InputMetadataEntry(
                "Spectrum",
                spectrum.SpectrumFileName,
                metadata?.WavelengthHeader ?? "",
                metadata?.IrradianceHeader ?? "",
                "nm/W m^-2 nm^-1",
                "nm/W m^-2 nm^-1",
                metadata is null
                    ? ""
                    : $"{metadata.Selection.SheetName}; " +
                      $"{ColumnName(metadata.Selection.WavelengthColumn)}/" +
                      ColumnName(metadata.Selection.IrradianceColumn)));
        }

        return new WorkflowExportSnapshot(
            Array.AsReadOnly(settings.ToArray()),
            Array.AsReadOnly((silicon?.Anchors ?? []).ToArray()),
            Array.AsReadOnly((sample?.Anchors ?? []).ToArray()),
            Array.AsReadOnly(inputs.ToArray()));
    }

    private static void AddMeasurementSettings(
        List<SettingEntry> settings,
        string prefix,
        double wavelengthStart,
        double wavelengthEnd,
        double wavelengthStep,
        AlignmentMode alignmentMode,
        double fixedStartTime,
        double nominalDelay,
        double averagingDuration,
        bool subtractDark,
        double darkStart,
        double darkEnd,
        double area)
    {
        settings.Add(new SettingEntry(
            $"{prefix}WavelengthRange",
            $"{Number(wavelengthStart)}–{Number(wavelengthEnd)}",
            "nm"));
        settings.Add(new SettingEntry(
            $"{prefix}WavelengthStep",
            Number(wavelengthStep),
            "nm"));
        settings.Add(new SettingEntry(
            $"{prefix}AlignmentMode",
            alignmentMode.ToString(),
            ""));
        settings.Add(new SettingEntry(
            $"{prefix}FixedStartTime",
            Number(fixedStartTime),
            "s"));
        settings.Add(new SettingEntry(
            $"{prefix}NominalDelay",
            Number(nominalDelay),
            "s"));
        settings.Add(new SettingEntry(
            $"{prefix}AveragingDuration",
            Number(averagingDuration),
            "s"));
        settings.Add(new SettingEntry(
            $"{prefix}SubtractDark",
            subtractDark ? "True" : "False",
            ""));
        settings.Add(new SettingEntry(
            $"{prefix}DarkRange",
            $"{Number(darkStart)}–{Number(darkEnd)}",
            "s"));
        settings.Add(new SettingEntry(
            $"{prefix}Area",
            Number(area),
            "cm2"));
    }

    private static void AddStatus(
        List<SettingEntry> settings,
        string prefix,
        State.ResultStatus status)
    {
        settings.Add(new SettingEntry(
            $"{prefix}Status",
            status.Freshness.ToString(),
            ""));
        settings.Add(new SettingEntry(
            $"{prefix}StatusReason",
            status.Reason,
            ""));
    }

    private static void AddTraceInput(
        List<InputMetadataEntry> inputs,
        string dataset,
        string fileName,
        TraceData? trace)
    {
        if (trace is null)
        {
            return;
        }

        TraceMetadata metadata = trace.Metadata;
        inputs.Add(new InputMetadataEntry(
            dataset,
            fileName,
            metadata.TimeHeader,
            metadata.CurrentHeader,
            JoinUnits(
                metadata.OriginalTimeUnit,
                metadata.OriginalCurrentUnit),
            "s/A",
            ""));
    }

    private static string JoinUnits(string left, string right) =>
        string.IsNullOrWhiteSpace(left) &&
        string.IsNullOrWhiteSpace(right)
            ? ""
            : $"{left}/{right}";

    private static string Number(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

    private static string ColumnName(int oneBasedColumn)
    {
        int value = oneBasedColumn;
        string result = "";
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }
        return result;
    }
}
