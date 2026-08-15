using System.IO;
using IPCE.Core.Errors;

namespace IPCE.Desktop.Localization;

public sealed class UserMessageLocalizer(
    ILocalizationService localization)
{
    private static readonly IReadOnlyDictionary<string, string> ErrorKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IPCE:AnchorFileNotFound"] = "Error.FileNotFound",
            ["IPCE:CalibrationRange"] = "Error.Coverage",
            ["IPCE:DarkRangeOutsideTrace"] = "Error.Coverage",
            ["IPCE:DuplicateAnchors"] = "Error.DuplicateData",
            ["IPCE:DuplicateExportNames"] = "Error.DuplicateData",
            ["IPCE:EmptyWavelengths"] = "Error.InvalidData",
            ["IPCE:EmptyWindow"] = "Error.Coverage",
            ["IPCE:ExportFailed"] = "Error.ExportOperation",
            ["IPCE:ExportVerificationFailed"] = "Error.ExportOperation",
            ["IPCE:FileNotFound"] = "Error.FileNotFound",
            ["IPCE:InsufficientCoverage"] = "Error.Coverage",
            ["IPCE:InsufficientDarkData"] = "Error.Coverage",
            ["IPCE:IntegrationCoverage"] = "Error.Coverage",
            ["IPCE:IntegrationInterpolation"] = "Error.Calculation",
            ["IPCE:InterpolationCoverage"] = "Error.Coverage",
            ["IPCE:InvalidAnchorFile"] = "Error.InvalidData",
            ["IPCE:InvalidArea"] = "Error.InvalidSettings",
            ["IPCE:InvalidAxisLimits"] = "Error.InvalidAxis",
            ["IPCE:InvalidDarkRange"] = "Error.InvalidSettings",
            ["IPCE:InvalidExportTable"] = "Error.ExportOperation",
            ["IPCE:InvalidExternalIPCE"] = "Error.InvalidData",
            ["IPCE:InvalidHitTestRadius"] = "Error.InvalidPlot",
            ["IPCE:InvalidIntegrationGrid"] = "Error.InvalidData",
            ["IPCE:InvalidIntegrationRange"] = "Error.InvalidSettings",
            ["IPCE:InvalidInterpolatedPowerDensity"] = "Error.Calculation",
            ["IPCE:InvalidInterpolationInput"] = "Error.InvalidData",
            ["IPCE:InvalidIPCEResult"] = "Error.InvalidData",
            ["IPCE:InvalidLogAxis"] = "Error.InvalidAxis",
            ["IPCE:InvalidPlotCoordinate"] = "Error.InvalidPlot",
            ["IPCE:InvalidPlotData"] = "Error.InvalidPlot",
            ["IPCE:InvalidPlotSeries"] = "Error.InvalidPlot",
            ["IPCE:InvalidPowerDensity"] = "Error.InvalidData",
            ["IPCE:InvalidPreview"] = "Error.InvalidPreview",
            ["IPCE:InvalidReference"] = "Error.InvalidData",
            ["IPCE:InvalidResponsivity"] = "Error.InvalidData",
            ["IPCE:InvalidSchedule"] = "Error.IPCE.InvalidSchedule",
            ["IPCE:InvalidSiliconResult"] = "Error.Calculation",
            ["IPCE:InvalidSpectrum"] = "Error.InvalidData",
            ["IPCE:InvalidSpectrumSelection"] = "Error.InvalidSelection",
            ["IPCE:InvalidTrace"] = "Error.IPCE.InvalidTrace",
            ["IPCE:InvalidTraceOverlay"] = "Error.InvalidPlot",
            ["IPCE:InvalidViewportPolicy"] = "Error.InvalidPlot",
            ["IPCE:InvalidWavelengthGrid"] = "Error.InvalidSettings",
            ["IPCE:MissingAnchors"] = "Error.MissingInput",
            ["IPCE:MissingCalculatedIPCE"] = "Error.MissingInput",
            ["IPCE:MissingCalibration"] = "Error.MissingInput",
            ["IPCE:MissingExternalIPCE"] = "Error.MissingInput",
            ["IPCE:MissingPowerDensity"] = "Error.MissingInput",
            ["IPCE:MissingSampleTrace"] = "Error.MissingInput",
            ["IPCE:MissingSiliconTrace"] = "Error.MissingInput",
            ["IPCE:MissingSpectrum"] = "Error.MissingInput",
            ["IPCE:NoCurrentExportSelection"] = "Error.NoExportSelection",
            ["IPCE:NoExportSelection"] = "Error.NoExportSelection",
            ["IPCE:NonMonotonicSchedule"] = "Error.InvalidSchedule",
            ["IPCE:NoNumericSpectrumColumns"] = "Error.InvalidData",
            ["IPCE:PowerInterpolationRange"] = "Error.Coverage",
            ["IPCE:ReferenceImportFailed"] = "Error.ImportOperation",
            ["IPCE:SpectrumColumnMissing"] = "Error.InvalidSelection",
            ["IPCE:SpectrumImportFailed"] = "Error.ImportOperation",
            ["IPCE:SpectrumSheetNotFound"] = "Error.InvalidSelection",
            ["IPCE:StaleResult"] = "Error.StaleResult",
            ["IPCE:StartupDataNotFound"] = "Error.FileNotFound",
            ["IPCE:TableImportFailed"] = "Error.ImportOperation",
            ["IPCE:TraceUnitsRequired"] = "Error.UnitsRequired",
            ["IPCE:UnknownAlignmentMode"] = "Error.InvalidSelection",
            ["IPCE:UnknownIPCESource"] = "Error.InvalidSelection",
            ["IPCE:UnsupportedCurrentUnit"] = "Error.UnsupportedInput",
            ["IPCE:UnsupportedExternalIPCE"] = "Error.UnsupportedInput",
            ["IPCE:UnsupportedTimeUnit"] = "Error.UnsupportedInput",
            ["IPCE:WorkbookImportFailed"] = "Error.ImportOperation",
            ["IPCE:WorkbookSheetNotFound"] = "Error.InvalidSelection",
        };
    private readonly ILocalizationService _localization = localization ??
        throw new ArgumentNullException(nameof(localization));

    public string Localize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is IpceException ipceException)
        {
            string key = ErrorKeys.TryGetValue(
                ipceException.Code,
                out string? mapped)
                    ? mapped
                    : $"Error.{ipceException.Code.Replace(':', '.')}";
            string message = _localization[key];
            return message == $"[{key}]"
                ? _localization.Format(
                    "Error.GenericDomain",
                    ipceException.Code)
                : message;
        }

        return exception switch
        {
            UnauthorizedAccessException => _localization["Error.AccessDenied"],
            IOException => _localization["Error.FileOperation"],
            _ => _localization["Error.GenericUnexpected"],
        };
    }
}
