function writtenPaths = ipceWriteExport(items, outputPath, format)
%IPCEWRITEEXPORT Write selected result tables and verify output files.
%   items is a struct array with fields Name and Data.

arguments
    items (1, :) struct
    outputPath (1, 1) string
    format (1, 1) string
end

if isempty(items)
    error("IPCE:NoExportSelection", "至少需要一个导出数据表。");
end
if ~all(isfield(items, ["Name", "Data"]))
    error("IPCE:InvalidExportItems", ...
        "导出项目必须包含 Name 和 Data 字段。");
end

switch lower(format)
    case "xlsx"
        for itemIndex = 1:numel(items)
            writetable(items(itemIndex).Data, outputPath, ...
                "Sheet", items(itemIndex).Name, ...
                "WriteMode", "overwritesheet");
        end
        writtenPaths = outputPath;

    case "csv"
        [baseFolder, baseName] = fileparts(outputPath);
        if numel(items) == 1
            writetable(items(1).Data, outputPath);
            writtenPaths = outputPath;
        else
            writtenPaths = strings(numel(items), 1);
            for itemIndex = 1:numel(items)
                itemPath = fullfile(baseFolder, ...
                    baseName + "_" + items(itemIndex).Name + ".csv");
                writetable(items(itemIndex).Data, itemPath);
                writtenPaths(itemIndex) = string(itemPath);
            end
        end

    case "mat"
        exportData = struct();
        for itemIndex = 1:numel(items)
            exportData.(items(itemIndex).Name) = items(itemIndex).Data;
        end
        save(outputPath, "exportData");
        writtenPaths = outputPath;

    otherwise
        error("IPCE:UnsupportedExport", "不支持的导出格式：%s", format);
end

for pathIndex = 1:numel(writtenPaths)
    fileInfo = dir(writtenPaths(pathIndex));
    if isempty(fileInfo) || fileInfo.bytes == 0
        error("IPCE:ExportVerificationFailed", ...
            "写入后未找到有效文件：%s", writtenPaths(pathIndex));
    end
end
end
