function filePath = ipceResolveStartupFile( ...
        exactFileName, searchPattern, primaryRoot, fallbackRoot)
%IPCERESOLVESTARTUPFILE Locate one startup file without guessing.

arguments
    exactFileName (1, 1) string
    searchPattern (1, 1) string = exactFileName
    primaryRoot (1, 1) string = string(pwd)
    fallbackRoot (1, 1) string = defaultFallbackRoot()
end

filePath = "";
exactPrimary = fullfile(primaryRoot, exactFileName);
if isfile(exactPrimary)
    filePath = string(exactPrimary);
    return
end

matches = dir(fullfile(primaryRoot, searchPattern));
matches = matches(~[matches.isdir]);
if numel(matches) == 1
    filePath = string(fullfile(matches(1).folder, matches(1).name));
    return
elseif numel(matches) > 1
    return
end

exactFallback = fullfile(fallbackRoot, exactFileName);
if isfile(exactFallback)
    filePath = string(exactFallback);
end
end

function root = defaultFallbackRoot()
if isdeployed
    root = string(ctfroot);
else
    root = string(fileparts(mfilename("fullpath")));
end
end
