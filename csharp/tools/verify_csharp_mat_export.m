function verify_csharp_mat_export
%VERIFY_CSHARP_MAT_EXPORT Verify the MAT file written by the C# exporter.

matPath = fullfile(tempdir, 'ipce-csharp-mat-verification.mat');
assert(isfile(matPath), ...
    'IPCE:MatVerificationFixtureMissing', ...
    ['Run ExportServiceTests before this verification so that the C# ' ...
     'MAT fixture exists.']);

loaded = load(matPath);
assert(isfield(loaded, 'exportData'));
assert(isstruct(loaded.exportData) && isscalar(loaded.exportData));
assert(isfield(loaded.exportData, 'SampleIPCE'));

tableData = loaded.exportData.SampleIPCE;
assert(isstruct(tableData) && isscalar(tableData));
assert(isequal(tableData.VariableNames, ...
    {'Wavelength_nm', 'IPCE_percent', 'Note', 'Included'}));
assert(tableData.RowCount == 2);
assert(isequal(tableData.Wavelength_nm, [400; 500]));
assert(isequal(tableData.IPCE_percent, [20; 50]));
assert(iscell(tableData.Note));
assert(isequal(tableData.Note, {'note, one'; 'quoted "value"'}));
assert(islogical(tableData.Included));
assert(isequal(tableData.Included, logical([1; 0])));

fprintf('C# MAT export verification passed: %s\n', matPath);
end
