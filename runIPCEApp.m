function runIPCEApp(varargin)
%RUNIPCEAPP Zero-output launcher for the deployed IPCE application.

if nargin > 1
    error("IPCE:InvalidLaunchArguments", ...
        "IPCEApp accepts at most one launch argument.");
end

mode = "";
if nargin == 1
    mode = string(varargin{1});
end
if mode ~= "" && mode ~= "--smoke-test"
    error("IPCE:InvalidLaunchMode", ...
        "Unsupported launch mode: %s", mode);
end

app = IPCEApp;
drawnow;
assert(isvalid(app), "IPCE:LaunchFailed", ...
    "The IPCE application window was not created.");

if mode == "--smoke-test"
    close(app);
    drawnow;
    return
end

waitfor(app);
end
