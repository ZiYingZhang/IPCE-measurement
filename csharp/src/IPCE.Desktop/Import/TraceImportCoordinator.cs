using IPCE.Core.Domain;
using IPCE.Desktop.Services;
using IPCE.IO.Import;

namespace IPCE.Desktop.Import;

public sealed class TraceImportCoordinator
{
    private readonly IImportSelectionService _selections;

    public TraceImportCoordinator(IImportSelectionService selections)
    {
        _selections = selections ??
            throw new ArgumentNullException(nameof(selections));
    }

    public async Task<TraceData?> ReadAsync(string path)
    {
        TraceImportInspection inspection = await Task.Run(
            () => ItTraceReader.Inspect(path));
        if (inspection.DetectedTimeUnit.Length > 0 &&
            inspection.DetectedCurrentUnit.Length > 0)
        {
            return await Task.Run(() => ItTraceReader.Read(path));
        }

        UnitOverrides? overrides =
            _selections.SelectTraceUnits(inspection);
        return overrides is null
            ? null
            : await Task.Run(
                () => ItTraceReader.Read(path, overrides));
    }
}
