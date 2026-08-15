namespace IPCE.IO.Import;

public sealed record TraceImportInspection(
    string TimeHeader,
    string CurrentHeader,
    string DetectedTimeUnit,
    string DetectedCurrentUnit);
