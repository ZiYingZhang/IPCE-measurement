function items = ipceBuildPostprocessExportItems(externalIPCE, ...
        spectrumSummary, spectrumCurve, includeExternalIPCE, ...
        includeIntegration)
%IPCEBUILDPOSTPROCESSEXPORTITEMS Build standalone post-processing exports.

arguments
    externalIPCE table
    spectrumSummary table
    spectrumCurve table
    includeExternalIPCE (1, 1) logical
    includeIntegration (1, 1) logical
end

items = struct("Name", {}, "Data", {});
if includeExternalIPCE && ~isempty(externalIPCE)
    items(end + 1) = struct( ...
        "Name", "ExternalIPCE", "Data", externalIPCE);
end
if includeIntegration && ~isempty(spectrumSummary)
    items(end + 1) = struct( ...
        "Name", "SpectrumSummary", "Data", spectrumSummary);
    if ~isempty(spectrumCurve)
        items(end + 1) = struct( ...
            "Name", "SpectrumCurve", "Data", spectrumCurve);
    end
end
end
