function archivePath = build_ipce_portable
%BUILD_IPCE_PORTABLE Build and verify the Runtime-free Windows ZIP.

config = ipcePortablePackageConfig();
if isempty(ver("compiler"))
    error("IPCE:CompilerMissing", ...
        "MATLAB Compiler is not installed.");
end
if ~license("test", "Compiler")
    error("IPCE:CompilerLicenseMissing", ...
        "A MATLAB Compiler license is not available.");
end

missingFiles = config.DefaultFiles(~isfile(config.DefaultFiles));
if ~isempty(missingFiles)
    error("IPCE:PackageDataMissing", ...
        "Default package files are missing: %s", ...
        strjoin(missingFiles, ", "));
end
if ~isfile(config.PortableReadme)
    error("IPCE:PackageReadmeMissing", ...
        "Portable readme is missing: %s", config.PortableReadme);
end

distRoot = fullfile(config.ProjectRoot, "dist");
releaseDir = fullfile(distRoot, config.ReleaseName);
archivePath = fullfile(distRoot, config.ArchiveName);
validateManagedOutput(distRoot, releaseDir, config.ReleaseName);

if ~isfolder(distRoot)
    mkdir(distRoot);
end
if isfolder(releaseDir)
    rmdir(releaseDir, "s");
end
if isfile(archivePath)
    delete(archivePath);
end
mkdir(releaseDir);

originalFolder = string(pwd);
restoreFolder = onCleanup(@()cd(originalFolder));
cd(config.ProjectRoot);
run_ipce_selftest;

mccArguments = { ...
    "-e", char(fullfile(config.ProjectRoot, "runIPCEApp.m")), ...
    "-o", "IPCEApp", ...
    "-d", char(releaseDir)};
for fileIndex = 1:numel(config.DefaultFiles)
    mccArguments(end + 1:end + 2) = { ...
        "-a", char(config.DefaultFiles(fileIndex))}; %#ok<AGROW>
end
mcc(mccArguments{:});

readmeTarget = fullfile(releaseDir, "README_运行说明.txt");
copyfile(config.PortableReadme, readmeTarget);

executablePath = fullfile(releaseDir, config.ExecutableName);
assertNonemptyFile(executablePath, "IPCE:PackageExecutableMissing");
assertNoRuntimePayload(releaseDir);

entries = dir(releaseDir);
entries = entries(~ismember({entries.name}, {'.', '..'}));
entryNames = string({entries.name});
if isempty(entryNames)
    error("IPCE:PackageEmpty", "The release directory is empty.");
end
zip(archivePath, entryNames, releaseDir);
assertNonemptyFile(archivePath, "IPCE:PackageArchiveMissing");

validationRoot = string(tempname(tempdir));
mkdir(validationRoot);
cleanupValidation = onCleanup( ...
    @()removeFolderIfPresent(validationRoot));
unzip(archivePath, validationRoot);
assertNonemptyFile(fullfile(validationRoot, config.ExecutableName), ...
    "IPCE:PackageArchiveInvalid");
assertNoRuntimePayload(validationRoot);

archiveInfo = dir(archivePath);
fprintf("Portable release directory:\n  %s\n", releaseDir);
fprintf("Portable ZIP:\n  %s\n", archivePath);
fprintf("ZIP size: %.3f MB\n", archiveInfo.bytes / 1024^2);
end

function validateManagedOutput(distRoot, releaseDir, releaseName)
distRoot = string(distRoot);
releaseDir = string(releaseDir);
[parentFolder, finalFolder] = fileparts(releaseDir);
if string(parentFolder) ~= distRoot || string(finalFolder) ~= releaseName
    error("IPCE:UnsafeBuildOutput", ...
        "Refusing to manage an unexpected output path: %s", ...
        releaseDir);
end
end

function assertNonemptyFile(filePath, errorIdentifier)
fileInfo = dir(filePath);
if isempty(fileInfo) || fileInfo.bytes <= 0
    error(errorIdentifier, "Missing or empty file: %s", filePath);
end
end

function assertNoRuntimePayload(folderPath)
items = dir(fullfile(folderPath, "**", "*"));
items = items(~[items.isdir]);
names = lower(string({items.name}));
forbidden = ["matlab_runtime", "mcrinstaller", "runtime_installer"];
if any(contains(names, forbidden), "all")
    error("IPCE:RuntimePayloadDetected", ...
        "The portable package unexpectedly contains a Runtime installer.");
end
end

function removeFolderIfPresent(folderPath)
if isfolder(folderPath)
    rmdir(folderPath, "s");
end
end
