function output = ipceLanguagePreference(action, varargin)
%IPCELANGUAGEPREFERENCE Safely load, save, and resolve UI language.

action = lower(string(action));
switch action
    case "defaultpath"
        root = string(getenv("LOCALAPPDATA"));
        if strlength(root) == 0
            root = string(prefdir);
        end
        output = fullfile(root, "IPCEApp", "settings.json");
    case "load"
        output = loadPreference(string(varargin{1}));
    case "save"
        savePreference(string(varargin{1}), string(varargin{2}));
        output = "";
    case "resolve"
        preferencePath = string(varargin{1});
        systemLocale = string(varargin{2});
        saved = loadPreference(preferencePath);
        if any(saved == ["en-US", "zh-CN"])
            output = saved;
        elseif startsWith(lower(systemLocale), "zh")
            output = "zh-CN";
        else
            output = "en-US";
        end
    otherwise
        error("IPCE:UnknownLanguagePreferenceAction", ...
            "Unknown language preference action: %s", action);
end
end

function language = loadPreference(path)
language = "";
try
    if ~isfile(path)
        return
    end
    decoded = jsondecode(fileread(path));
    if isstruct(decoded) && isscalar(decoded) && isfield(decoded, "Language")
        rawCandidate = decoded.Language;
    elseif isstruct(decoded) && isscalar(decoded) && isfield(decoded, "language")
        rawCandidate = decoded.language;
    else
        return
    end
    if ischar(rawCandidate) || (isstring(rawCandidate) && isscalar(rawCandidate))
        candidate = string(rawCandidate);
        if isscalar(candidate) && any(candidate == ["en-US", "zh-CN"])
            language = candidate;
        end
    end
catch
    language = "";
end
end

function savePreference(path, language)
if ~any(language == ["en-US", "zh-CN"])
    return
end
folder = string(fileparts(path));
temporaryPath = "";
try
    if ~isfolder(folder)
        mkdir(folder);
    end
    temporaryPath = string(tempname(folder)) + ".tmp";
    fileIdentifier = fopen(temporaryPath, "w", "n", "UTF-8");
    if fileIdentifier < 0
        return
    end
    cleanupFile = onCleanup(@()fclose(fileIdentifier));
    fwrite(fileIdentifier, jsonencode(struct("Language", char(language))), ...
        "char");
    clear cleanupFile
    movefile(temporaryPath, path, "f");
catch
    if strlength(temporaryPath) > 0 && isfile(temporaryPath)
        delete(temporaryPath);
    end
end
end
