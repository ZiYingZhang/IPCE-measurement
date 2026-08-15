function locale = ipceSystemLocale(reader)
%IPCESYSTEMLOCALE Return the Windows UI culture used for first launch.
%   An injectable reader keeps locale selection deterministic in tests.

if nargin < 1
    reader = @readPlatformUICulture;
end

locale = "en-US";
try
    candidate = string(reader());
    if isscalar(candidate) && strlength(strtrim(candidate)) > 0
        candidate = replace(strtrim(candidate), "_", "-");
        candidate = extractBefore(candidate + ".", ".");
        if strlength(candidate) > 0
            locale = candidate;
        end
    end
catch
    locale = "en-US";
end
end

function locale = readPlatformUICulture()
% .NET CurrentUICulture follows the Windows display/UI language and remains
% available to MATLAB Compiler applications on supported Windows releases.
try
    locale = string( ...
        System.Globalization.CultureInfo.CurrentUICulture.Name);
    if strlength(locale) > 0
        return
    end
catch
end

% Java is a documented MATLAB runtime dependency and provides a portable
% fallback if .NET culture access is unavailable.
try
    locale = string(char(java.util.Locale.getDefault().toLanguageTag()));
    if strlength(locale) > 0
        return
    end
catch
end

locale = string(getenv("LANG"));
end
