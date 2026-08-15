function [ipce, sourceLabel] = ipceResolveIPCESource( ...
        calculatedIPCE, externalIPCE, source)
%IPCERESOLVEIPCESOURCE Select a canonical IPCE table for integration.

arguments
    calculatedIPCE table
    externalIPCE table
    source (1, 1) string
end

switch source
    case "calculated"
        if isempty(calculatedIPCE)
            error("IPCE:MissingCalculatedIPCE", ...
                "当前选择了“本软件计算结果”，请先计算样品 IPCE。");
        end
        ipce = calculatedIPCE;
        sourceLabel = "本软件计算结果";
    case "external"
        if isempty(externalIPCE)
            error("IPCE:MissingExternalIPCE", ...
                "当前选择了“外部导入 IPCE”，请先导入外部 IPCE 文件。");
        end
        ipce = externalIPCE;
        sourceLabel = "外部导入 IPCE";
    otherwise
        error("IPCE:UnknownIPCESource", ...
            "无法识别 IPCE 数据源：%s。", source);
end
end
