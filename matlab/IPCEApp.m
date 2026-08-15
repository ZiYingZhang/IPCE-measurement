function appFigure = IPCEApp
%IPCEAPP Interactive workflow for power-density calibration and IPCE.
%   Run IPCEApp from the MATLAB command window. This is a programmatic
%   MATLAB UI and does not require an App Designer .mlapp file.

defaults = ipceDefaultConfig();
languagePreferencePath = ipceLanguagePreference("defaultpath");
currentLanguage = ipceLanguagePreference( ...
    "resolve", languagePreferencePath, ipceSystemLocale());
localizationRegistry = cell(0, 3);
state = struct( ...
    "calibration", table(), ...
    "calibrationFile", "", ...
    "siliconTrace", table(), ...
    "siliconFile", "", ...
    "sampleTrace", table(), ...
    "sampleFile", "", ...
    "siliconSchedule", table(), ...
    "sampleSchedule", table(), ...
    "siliconScan", struct("Start_nm", 300, "End_nm", 1100, ...
        "Step_nm", 5, "Delay_s", 8, "Average_s", 4), ...
    "sampleScan", struct("Start_nm", 300, "End_nm", 1100, ...
        "Step_nm", 5, "Delay_s", 8, "Average_s", 4), ...
    "siliconExtracted", table(), ...
    "sampleExtracted", table(), ...
    "lightResult", table(), ...
    "ipceResult", table(), ...
    "externalIPCE", table(), ...
    "externalIPCEFile", "", ...
    "spectrumIPCESource", "calculated", ...
    "spectrum", table(), ...
    "spectrumFile", "", ...
    "spectrumColumns", table(), ...
    "spectrumSummary", table(), ...
    "spectrumCurve", table(), ...
    "pickTarget", "", ...
    "siliconAnchorRow", 1, ...
    "sampleAnchorRow", 1);

appFigure = uifigure( ...
    "Name", "IPCE 测量与分析", ...
    "Position", [80, 60, 1320, 820], ...
    "Color", [0.97, 0.97, 0.97]);

mainGrid = uigridlayout(appFigure, [1, 2]);
mainGrid.ColumnWidth = {430, "1x"};
mainGrid.Padding = [8, 8, 8, 8];
mainGrid.ColumnSpacing = 8;

controlPanel = uipanel(mainGrid, "Title", "测量流程");
controlGrid = uigridlayout(controlPanel, [23, 4]);
controlGrid.ColumnWidth = {125, "1x", 100, 85};
controlGrid.RowHeight = {32, 38, 30, 22, 30, 22, 30, 22, ...
    28, 22, 30, 30, 30, 30, 30, 30, 30, 28, 36, 36, 64, "1x", 1};
controlGrid.RowSpacing = 4;
controlGrid.ColumnSpacing = 5;
controlGrid.Padding = [8, 8, 8, 8];

titleLabel = uilabel(controlGrid, ...
    "Text", "硅基标探 → 单色光功率密度 → 样品 IPCE", ...
    "FontSize", 16, "FontWeight", "bold");
setLayout(titleLabel, 1, [1, 3]);
languageDropDown = uidropdown(controlGrid, ...
    "Items", ["English", "中文"], ...
    "ItemsData", ["en-US", "zh-CN"], ...
    "Value", currentLanguage, ...
    "ValueChangedFcn", @onLanguageChanged, ...
    "Tooltip", "语言 / Language");
setLayout(languageDropDown, 1, 4);

% 1. 在 IPCE 之前加入 <br> 强制换行
% 2. 使用 style="white-space: nowrap" 确保 IPCE(%) 作为一个整体绝不中途断行
htmlText = ['<b><i>P</i><sub>inc</sub></b> = |Δ<i>I</i><sub>Si</sub>| / (<i>R</i><sub>Si</sub> × <i>A</i><sub>Si</sub>)<br>' ...
            '<span style="white-space: nowrap;"><b>IPCE(%)</b></span> = 100 × 1239.84 × (|Δ<i>I</i><sub>sample</sub>| / <i>A</i><sub>sample</sub>) / (<i>P</i><sub>inc</sub> × λ<sub>nm</sub>)'];

% 2. 创建 label 并指定 Interpreter 为 'html'
formulaLabel = uilabel(controlGrid, ...
    "Text", htmlText, ...
    "Interpreter", "html", ...
    "WordWrap", "on", ...
    "FontColor", [0.22, 0.22, 0.22]);
setLayout(formulaLabel, 2, [1, 4]);

calibrationCaption = uilabel(controlGrid, "Text", "标探响应度 (.xlsx)");
setLayout(calibrationCaption, 3, [1, 3]);
calibrationButton = uibutton(controlGrid, "Text", "导入", ...
    "ButtonPushedFcn", @onLoadCalibration);
setLayout(calibrationButton, 3, 4);
calibrationPathLabel = fileLabel(controlGrid, "尚未导入");
setLayout(calibrationPathLabel, 4, [1, 4]);

siliconCaption = uilabel(controlGrid, "Text", "硅标探 i-t（0 V）");
setLayout(siliconCaption, 5, [1, 3]);
siliconButton = uibutton(controlGrid, "Text", "导入", ...
    "ButtonPushedFcn", @onLoadSilicon);
setLayout(siliconButton, 5, 4);
siliconPathLabel = fileLabel(controlGrid, "尚未导入");
setLayout(siliconPathLabel, 6, [1, 4]);

sampleCaption = uilabel(controlGrid, "Text", "样品 i-t");
setLayout(sampleCaption, 7, [1, 3]);
sampleButton = uibutton(controlGrid, "Text", "导入", ...
    "ButtonPushedFcn", @onLoadSample);
setLayout(sampleButton, 7, 4);
samplePathLabel = fileLabel(controlGrid, "尚未导入");
setLayout(samplePathLabel, 8, [1, 4]);

parameterTitle = uilabel(controlGrid, "Text", "扫描与取点参数", ...
    "FontWeight", "bold", "FontSize", 14);
setLayout(parameterTitle, 9, [1, 2]);
scanTargetDropDown = uidropdown(controlGrid, ...
    "Items", ["编辑标探参数", "编辑样品参数"], ...
    "ItemsData", ["silicon", "sample"], ...
    "Value", "silicon", ...
    "ValueChangedFcn", @onScanTargetChanged);
setLayout(scanTargetDropDown, 9, [3, 4]);

waveStartLabel = uilabel(controlGrid, "Text", "起始波长 (nm)", ...
    "HorizontalAlignment", "center");
waveEndLabel = uilabel(controlGrid, "Text", "终止波长 (nm)", ...
    "HorizontalAlignment", "center");
waveStepLabel = uilabel(controlGrid, "Text", "步长 (nm)", ...
    "HorizontalAlignment", "center");
setLayout(waveStartLabel, 10, 1);
setLayout(waveEndLabel, 10, 2);
setLayout(waveStepLabel, 10, [3, 4]);

waveStartField = uieditfield(controlGrid, "numeric", ...
    "Value", 300, "Limits", [0, Inf], ...
    "ValueChangedFcn", @onScanFieldChanged);
waveEndField = uieditfield(controlGrid, "numeric", ...
    "Value", 1100, "Limits", [0, Inf], ...
    "ValueChangedFcn", @onScanFieldChanged);
waveStepField = uieditfield(controlGrid, "numeric", ...
    "Value", 5, "Limits", [eps, Inf], ...
    "ValueChangedFcn", @onScanFieldChanged);
setLayout(waveStartField, 11, 1);
setLayout(waveEndField, 11, 2);
setLayout(waveStepField, 11, [3, 4]);

alignmentModeLabel = uilabel(controlGrid, "Text", "时间对齐方式");
setLayout(alignmentModeLabel, 12, 1);
alignmentModeDropDown = uidropdown(controlGrid, ...
    "Items", ["锚点插值（推荐）", "固定 Delay"], ...
    "ItemsData", ["anchors", "fixed"], ...
    "Value", "anchors", ...
    "ValueChangedFcn", @onAlignmentModeChanged);
setLayout(alignmentModeDropDown, 12, [2, 4]);

siliconStartLabel = uilabel(controlGrid, "Text", "固定模式：标探首时刻");
setLayout(siliconStartLabel, 13, 1);
siliconStartField = uieditfield(controlGrid, "numeric", "Value", 50);
setLayout(siliconStartField, 13, [2, 3]);
siliconPickButton = uibutton(controlGrid, "Text", "图上选", ...
    "ButtonPushedFcn", @(~, ~)beginPick("silicon"));
setLayout(siliconPickButton, 13, 4);

sampleStartLabel = uilabel(controlGrid, "Text", "固定模式：样品首时刻");
setLayout(sampleStartLabel, 14, 1);
sampleStartField = uieditfield(controlGrid, "numeric", "Value", 50);
setLayout(sampleStartField, 14, [2, 3]);
samplePickButton = uibutton(controlGrid, "Text", "图上选", ...
    "ButtonPushedFcn", @(~, ~)beginPick("sample"));
setLayout(samplePickButton, 14, 4);

dwellLabel = uilabel(controlGrid, "Text", "驻留 / Delay (s)");
setLayout(dwellLabel, 15, 1);
dwellField = uieditfield(controlGrid, "numeric", ...
    "Value", 8, "Limits", [eps, Inf], ...
    "ValueChangedFcn", @onScanFieldChanged, ...
    "Tooltip", "固定模式的驻留时间；锚点模式只有一个锚点时作为时间步长。");
setLayout(dwellField, 15, 2);
tailLabel = uilabel(controlGrid, "Text", "确认点后平均 (s)", ...
    "HorizontalAlignment", "right");
setLayout(tailLabel, 15, 3);
tailField = uieditfield(controlGrid, "numeric", ...
    "Value", 4, "Limits", [0, Inf], ...
    "ValueChangedFcn", @onScanFieldChanged);
setLayout(tailField, 15, 4);

darkCheckBox = uicheckbox(controlGrid, ...
    "Text", "扣除指定区间暗电流", "Value", defaults.SubtractDark, ...
    "ValueChangedFcn", @(~, ~)refreshTracePlots(), ...
    "Tooltip", "标探和样品分别使用用户输入或图上选择的暗电流时间区间。");
setLayout(darkCheckBox, 16, [1, 2]);
darkSettingsButton = uibutton(controlGrid, "Text", "设置暗区间", ...
    "ButtonPushedFcn", @(~, ~)selectDarkTab());
setLayout(darkSettingsButton, 16, [3, 4]);

siliconAreaLabel = uilabel(controlGrid, "Text", "标探面积 (cm²)");
setLayout(siliconAreaLabel, 17, 1);
siliconAreaField = uieditfield(controlGrid, "numeric", ...
    "Value", 0.36, "Limits", [eps, Inf], ...
    "Tooltip", "硅标探实际受光面积。");
setLayout(siliconAreaField, 17, 2);
sampleAreaLabel = uilabel(controlGrid, "Text", "样品面积 (cm²)", ...
    "HorizontalAlignment", "right");
setLayout(sampleAreaLabel, 17, 3);
sampleAreaField = uieditfield(controlGrid, "numeric", ...
    "Value", 1, "Limits", [eps, Inf], ...
    "Tooltip", "样品有效受光面积；假设光斑辐照度均匀。");
setLayout(sampleAreaField, 17, 4);

windowHint = uilabel(controlGrid, ...
    "Text", "锚点只表示此时已经输出目标波长；程序从确认点向后取稳态均值。", ...
    "WordWrap", "on", "FontColor", [0.35, 0.35, 0.35]);
setLayout(windowHint, 18, [1, 4]);

lightButton = uibutton(controlGrid, "push", ...
    "Text", "① 反算单色光功率密度", ...
    "ButtonPushedFcn", @onComputeLight, ...
    "BackgroundColor", [0.88, 0.94, 1.00]);
setLayout(lightButton, 19, [1, 2]);
ipceButton = uibutton(controlGrid, "push", ...
    "Text", "② 计算样品 IPCE", ...
    "ButtonPushedFcn", @onComputeIPCE, ...
    "BackgroundColor", [0.88, 0.98, 0.90]);
setLayout(ipceButton, 19, [3, 4]);

exportButton = uibutton(controlGrid, "push", ...
    "Text", "导出结果（XLSX / CSV / MAT）", ...
    "ButtonPushedFcn", @onExport);
setLayout(exportButton, 20, [1, 4]);

statusLabel = uilabel(controlGrid, ...
    "Text", "请先导入标探响应度和硅标探 i-t。", ...
    "WordWrap", "on", "FontColor", [0.10, 0.25, 0.50]);
setLayout(statusLabel, 21, [1, 4]);
statusSource = "请先导入标探响应度和硅标探 i-t。";
statusArguments = {};

usageLabel = uilabel(controlGrid, ...
    "Text", ["提示：在“时间对齐”页输入波长–时间锚点，或选择锚点行后到图上确认。" ...
    "若光电流方向为负，程序保留有符号列，但用 |ΔI| 计算功率密度和 IPCE。" ...
    "界面不对标探响应度做范围外外推。"], ...
    "WordWrap", "on", "VerticalAlignment", "top", ...
    "FontColor", [0.32, 0.32, 0.32]);
setLayout(usageLabel, 22, [1, 4]);

rightPanel = uipanel(mainGrid, "BorderType", "none");
rightGrid = uigridlayout(rightPanel, [1, 1]);
rightGrid.Padding = [0, 0, 0, 0];
tabGroup = uitabgroup(rightGrid);

siliconTab = uitab(tabGroup, "Title", "标探 i-t");
siliconTabGrid = uigridlayout(siliconTab, [1, 1]);
siliconAxes = uiaxes(siliconTabGrid);
configureTraceAxes(siliconAxes, "硅标探 i-t", ...
    @(source, ~)onAxesClick(source, "silicon"));
try
    axtoolbar(siliconAxes, {"zoomin", "zoomout", "pan", "restoreview"});
catch
end

sampleTab = uitab(tabGroup, "Title", "样品 i-t");
sampleTabGrid = uigridlayout(sampleTab, [1, 1]);
sampleAxes = uiaxes(sampleTabGrid);
configureTraceAxes(sampleAxes, "样品 i-t", ...
    @(source, ~)onAxesClick(source, "sample"));
try
    axtoolbar(sampleAxes, {"zoomin", "zoomout", "pan", "restoreview"});
catch
end

alignmentTab = uitab(tabGroup, "Title", "时间对齐");
alignmentGrid = uigridlayout(alignmentTab, [2, 2]);
alignmentGrid.RowHeight = {48, "1x"};
alignmentGrid.ColumnWidth = {"0.46x", "0.54x"};
alignmentInstruction = uilabel(alignmentGrid, ...
    "Text", ["锚点时间只保证单色仪此时已经输出该波长，不假定它是切换起点。" ...
    "可直接编辑；也可先放大 i-t 图，再新增图上选点。" ...
    "两点以上分段线性插值，两端按最近两点斜率外推。"], ...
    "WordWrap", "on");
setLayout(alignmentInstruction, 1, [1, 2]);

anchorTabGroup = uitabgroup(alignmentGrid);
setLayout(anchorTabGroup, 2, 1);
siliconAnchorTab = uitab(anchorTabGroup, "Title", "标探锚点");
siliconAnchorGrid = uigridlayout(siliconAnchorTab, [2, 1]);
siliconAnchorGrid.RowHeight = {"1x", 70};
siliconAnchorTable = uitable(siliconAnchorGrid, ...
    "Data", [370, 127; 400, 168; 500, 333; 885, 965], ...
    "ColumnName", {"波长 (nm)", "时间 (s)"}, ...
    "ColumnEditable", [true, true], ...
    "CellSelectionCallback", ...
    @(~, event)rememberAnchorRow(event, "silicon"), ...
    "CellEditCallback", @(~, ~)plotAlignmentPreview());
siliconAnchorButtons = uigridlayout(siliconAnchorGrid, [2, 3]);
siliconAnchorButtons.ColumnWidth = {"1x", "1x", "1x"};
siliconAnchorButtons.RowHeight = {30, 30};
siliconAnchorButtons.Padding = [0, 0, 0, 0];
button = uibutton(siliconAnchorButtons, "Text", "添加行", ...
    "ButtonPushedFcn", @(~, ~)addAnchorRow("silicon"));
setLayout(button, 1, 1);
button = uibutton(siliconAnchorButtons, "Text", "删除行", ...
    "ButtonPushedFcn", @(~, ~)deleteAnchorRow("silicon"));
setLayout(button, 1, 2);
button = uibutton(siliconAnchorButtons, "Text", "导入 TXT", ...
    "ButtonPushedFcn", @(~, ~)onLoadAnchors("silicon"));
setLayout(button, 1, 3);
button = uibutton(siliconAnchorButtons, "Text", "确认已有行", ...
    "ButtonPushedFcn", @(~, ~)beginAnchorPick("silicon"));
setLayout(button, 2, 1);
button = uibutton(siliconAnchorButtons, "Text", "新增图上选点", ...
    "ButtonPushedFcn", @(~, ~)beginNewAnchorPick("silicon"));
setLayout(button, 2, [2, 3]);

sampleAnchorTab = uitab(anchorTabGroup, "Title", "样品锚点");
sampleAnchorGrid = uigridlayout(sampleAnchorTab, [2, 1]);
sampleAnchorGrid.RowHeight = {"1x", 70};
sampleAnchorTable = uitable(sampleAnchorGrid, ...
    "Data", nan(2, 2), ...
    "ColumnName", {"波长 (nm)", "时间 (s)"}, ...
    "ColumnEditable", [true, true], ...
    "CellSelectionCallback", ...
    @(~, event)rememberAnchorRow(event, "sample"), ...
    "CellEditCallback", @(~, ~)plotAlignmentPreview());
sampleAnchorButtons = uigridlayout(sampleAnchorGrid, [2, 3]);
sampleAnchorButtons.ColumnWidth = {"1x", "1x", "1x"};
sampleAnchorButtons.RowHeight = {30, 30};
sampleAnchorButtons.Padding = [0, 0, 0, 0];
button = uibutton(sampleAnchorButtons, "Text", "添加行", ...
    "ButtonPushedFcn", @(~, ~)addAnchorRow("sample"));
setLayout(button, 1, 1);
button = uibutton(sampleAnchorButtons, "Text", "删除行", ...
    "ButtonPushedFcn", @(~, ~)deleteAnchorRow("sample"));
setLayout(button, 1, 2);
button = uibutton(sampleAnchorButtons, "Text", "导入 TXT", ...
    "ButtonPushedFcn", @(~, ~)onLoadAnchors("sample"));
setLayout(button, 1, 3);
button = uibutton(sampleAnchorButtons, "Text", "确认已有行", ...
    "ButtonPushedFcn", @(~, ~)beginAnchorPick("sample"));
setLayout(button, 2, 1);
button = uibutton(sampleAnchorButtons, "Text", "新增图上选点", ...
    "ButtonPushedFcn", @(~, ~)beginNewAnchorPick("sample"));
setLayout(button, 2, [2, 3]);

alignmentAxes = uiaxes(alignmentGrid);
setLayout(alignmentAxes, 2, 2);
xlabel(alignmentAxes, "波长 (nm)");
ylabel(alignmentAxes, "确认时间 (s)");
title(alignmentAxes, "由锚点生成的波长–时间调度");
grid(alignmentAxes, "on");

darkTab = uitab(tabGroup, "Title", "暗电流");
darkGrid = uigridlayout(darkTab, [4, 4]);
darkGrid.RowHeight = {70, 32, 38, 38};
darkGrid.ColumnWidth = {95, "1x", "1x", 150};
darkInstruction = uilabel(darkGrid, ...
    "Text", ["暗电流不会再默认取扫描前一段。请为标探和样品分别输入明确时间区间，" ...
    "或先在对应 i-t 图上放大，再依次点击暗区间起点和终点。计算时取该区间的平均电流。"], ...
    "WordWrap", "on");
setLayout(darkInstruction, 1, [1, 4]);
targetHeader = uilabel(darkGrid, "Text", "数据", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(targetHeader, 2, 1);
startHeader = uilabel(darkGrid, "Text", "暗区间起点 (s)", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(startHeader, 2, 2);
endHeader = uilabel(darkGrid, "Text", "暗区间终点 (s)", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(endHeader, 2, 3);
methodHeader = uilabel(darkGrid, "Text", "图上操作", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(methodHeader, 2, 4);

siliconDarkLabel = uilabel(darkGrid, "Text", "硅标探", ...
    "HorizontalAlignment", "center");
setLayout(siliconDarkLabel, 3, 1);
siliconDarkStartField = uieditfield(darkGrid, "numeric", ...
    "Value", defaults.SiliconDarkRange_s(1), ...
    "ValueChangedFcn", @(~, ~)onDarkRangeChanged("silicon"));
setLayout(siliconDarkStartField, 3, 2);
siliconDarkEndField = uieditfield(darkGrid, "numeric", ...
    "Value", defaults.SiliconDarkRange_s(2), ...
    "ValueChangedFcn", @(~, ~)onDarkRangeChanged("silicon"));
setLayout(siliconDarkEndField, 3, 3);
siliconDarkPickButton = uibutton(darkGrid, "Text", "依次选择起点、终点", ...
    "ButtonPushedFcn", @(~, ~)beginDarkRangePick("silicon"));
setLayout(siliconDarkPickButton, 3, 4);

sampleDarkLabel = uilabel(darkGrid, "Text", "样品", ...
    "HorizontalAlignment", "center");
setLayout(sampleDarkLabel, 4, 1);
sampleDarkStartField = uieditfield(darkGrid, "numeric", ...
    "Value", defaults.SampleDarkRange_s(1), ...
    "ValueChangedFcn", @(~, ~)onDarkRangeChanged("sample"));
setLayout(sampleDarkStartField, 4, 2);
sampleDarkEndField = uieditfield(darkGrid, "numeric", ...
    "Value", defaults.SampleDarkRange_s(2), ...
    "ValueChangedFcn", @(~, ~)onDarkRangeChanged("sample"));
setLayout(sampleDarkEndField, 4, 3);
sampleDarkPickButton = uibutton(darkGrid, "Text", "依次选择起点、终点", ...
    "ButtonPushedFcn", @(~, ~)beginDarkRangePick("sample"));
setLayout(sampleDarkPickButton, 4, 4);

axisSettingsTab = uitab(tabGroup, "Title", "图形设置");
axisSettingsGrid = uigridlayout(axisSettingsTab, [6, 4]);
axisSettingsGrid.RowHeight = {70, 38, 30, 38, 38, 42};
axisSettingsGrid.ColumnWidth = {90, "1x", "1x", 125};
axisInstruction = uilabel(axisSettingsGrid, ...
    "Text", ["每张图都可直接使用右上角工具栏放大、缩小、平移和恢复。" ...
    "这里还可以精确输入 X/Y 显示范围，并分别选择线性或对数刻度。"], ...
    "WordWrap", "on");
setLayout(axisInstruction, 1, [1, 4]);
axisTargetLabel = uilabel(axisSettingsGrid, "Text", "目标图形");
setLayout(axisTargetLabel, 2, 1);
axisTargetDropDown = uidropdown(axisSettingsGrid, ...
    "Items", ["标探 i-t", "样品 i-t", "时间对齐", "单色光功率密度", ...
    "样品 IPCE", "光谱积分（左轴）", "光谱积分（右轴）", ...
    "累计积分电流密度"], ...
    "ItemsData", ["silicon", "sample", "alignment", "power", ...
    "ipce", "spectrum-left", "spectrum-right", "cumulative"], ...
    "Value", "silicon", "ValueChangedFcn", @onAxisTargetChanged);
setLayout(axisTargetDropDown, 2, [2, 4]);
minimumHeader = uilabel(axisSettingsGrid, "Text", "最小值", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(minimumHeader, 3, 2);
maximumHeader = uilabel(axisSettingsGrid, "Text", "最大值", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(maximumHeader, 3, 3);
scaleHeader = uilabel(axisSettingsGrid, "Text", "刻度类型", ...
    "HorizontalAlignment", "center", "FontWeight", "bold");
setLayout(scaleHeader, 3, 4);
xAxisLabel = uilabel(axisSettingsGrid, "Text", "X 轴", ...
    "HorizontalAlignment", "center");
setLayout(xAxisLabel, 4, 1);
axisXMinField = uieditfield(axisSettingsGrid, "numeric", "Value", 0);
setLayout(axisXMinField, 4, 2);
axisXMaxField = uieditfield(axisSettingsGrid, "numeric", "Value", 1);
setLayout(axisXMaxField, 4, 3);
axisXScaleDropDown = uidropdown(axisSettingsGrid, ...
    "Items", ["线性", "对数"], "ItemsData", ["linear", "log"], ...
    "Value", "linear");
setLayout(axisXScaleDropDown, 4, 4);
yAxisLabel = uilabel(axisSettingsGrid, "Text", "Y 轴", ...
    "HorizontalAlignment", "center");
setLayout(yAxisLabel, 5, 1);
axisYMinField = uieditfield(axisSettingsGrid, "numeric", "Value", 0);
setLayout(axisYMinField, 5, 2);
axisYMaxField = uieditfield(axisSettingsGrid, "numeric", "Value", 1);
setLayout(axisYMaxField, 5, 3);
axisYScaleDropDown = uidropdown(axisSettingsGrid, ...
    "Items", ["线性", "对数"], "ItemsData", ["linear", "log"], ...
    "Value", "linear");
setLayout(axisYScaleDropDown, 5, 4);
axisReadButton = uibutton(axisSettingsGrid, "Text", "读取当前范围", ...
    "ButtonPushedFcn", @(~, ~)loadAxisSettings());
setLayout(axisReadButton, 6, 1);
axisApplyButton = uibutton(axisSettingsGrid, "Text", "应用范围与刻度", ...
    "ButtonPushedFcn", @onApplyAxisSettings);
setLayout(axisApplyButton, 6, [2, 3]);
axisAutoButton = uibutton(axisSettingsGrid, "Text", "自动范围", ...
    "ButtonPushedFcn", @onAutoAxisSettings);
setLayout(axisAutoButton, 6, 4);

resultTab = uitab(tabGroup, "Title", "功率密度与 IPCE");
resultGrid = uigridlayout(resultTab, [2, 1]);
resultGrid.RowHeight = {"1x", "1x"};
powerAxes = uiaxes(resultGrid);
xlabel(powerAxes, "波长 (nm)");
ylabel(powerAxes, "入射功率密度 (\muW cm^{-2})");
title(powerAxes, "由硅标探反算的单色光功率密度");
grid(powerAxes, "on");
ipceAxes = uiaxes(resultGrid);
xlabel(ipceAxes, "波长 (nm)");
ylabel(ipceAxes, "IPCE (%)");
title(ipceAxes, "样品 IPCE");
grid(ipceAxes, "on");

spectrumTab = uitab(tabGroup, "Title", "光谱积分");
spectrumGrid = uigridlayout(spectrumTab, [4, 1]);
spectrumGrid.RowHeight = {175, "1.15x", "0.85x", 78};
spectrumControlPanel = uipanel(spectrumGrid, "Title", "光谱数据与积分范围");
spectrumControlGrid = uigridlayout(spectrumControlPanel, [4, 8]);
spectrumControlGrid.ColumnWidth = {78, "1x", 62, "1x", 68, "1x", 68, "1x"};
spectrumControlGrid.RowHeight = {28, 28, 28, 32};
spectrumControlGrid.Padding = [6, 3, 6, 3];

externalIPCEImportButton = uibutton(spectrumControlGrid, ...
    "Text", "导入 IPCE", "ButtonPushedFcn", @onLoadExternalIPCE);
setLayout(externalIPCEImportButton, 1, 1);
externalIPCEPathLabel = fileLabel(spectrumControlGrid, "尚未导入外部 IPCE");
setLayout(externalIPCEPathLabel, 1, [2, 4]);
ipceSourceLabel = uilabel(spectrumControlGrid, "Text", "IPCE 数据源");
setLayout(ipceSourceLabel, 1, [5, 6]);
ipceSourceDropDown = uidropdown(spectrumControlGrid, ...
    "Items", ["本软件计算结果", "外部导入 IPCE"], ...
    "ItemsData", ["calculated", "external"], ...
    "Value", "calculated", ...
    "ValueChangedFcn", @onIPCESourceChanged);
setLayout(ipceSourceDropDown, 1, [7, 8]);

spectrumImportButton = uibutton(spectrumControlGrid, "Text", "导入光谱", ...
    "ButtonPushedFcn", @onLoadSpectrum);
setLayout(spectrumImportButton, 2, 1);
spectrumPathLabel = fileLabel(spectrumControlGrid, "尚未导入");
setLayout(spectrumPathLabel, 2, [2, 4]);
spectrumSheetLabel = uilabel(spectrumControlGrid, "Text", "工作表");
setLayout(spectrumSheetLabel, 2, 5);
spectrumSheetField = uieditfield(spectrumControlGrid, "text", ...
    "Value", "Spectra", "ValueChangedFcn", @onSpectrumSelectionChanged);
setLayout(spectrumSheetField, 2, [6, 8]);
spectrumWavelengthColumnLabel = uilabel(spectrumControlGrid, "Text", "波长列");
setLayout(spectrumWavelengthColumnLabel, 3, 1);
spectrumWavelengthColumnDropDown = uidropdown(spectrumControlGrid, ...
    "Items", "尚未读取表头", "ItemsData", 1, "Value", 1, ...
    "ValueChangedFcn", @onSpectrumSelectionChanged);
setLayout(spectrumWavelengthColumnDropDown, 3, [2, 4]);

spectrumIrradianceColumnLabel = uilabel(spectrumControlGrid, "Text", "积分列");
setLayout(spectrumIrradianceColumnLabel, 3, 5);
spectrumIrradianceColumnDropDown = uidropdown(spectrumControlGrid, ...
    "Items", "尚未读取表头", "ItemsData", 3, "Value", 3, ...
    "ValueChangedFcn", @onSpectrumSelectionChanged);
setLayout(spectrumIrradianceColumnDropDown, 3, [6, 8]);
integrationStartLabel = uilabel(spectrumControlGrid, "Text", "积分起点");
setLayout(integrationStartLabel, 4, 1);
integrationStartField = uieditfield(spectrumControlGrid, "numeric", ...
    "Value", 300, "Limits", [0, Inf]);
setLayout(integrationStartField, 4, 2);
integrationEndLabel = uilabel(spectrumControlGrid, "Text", "积分终点");
setLayout(integrationEndLabel, 4, 3);
integrationEndField = uieditfield(spectrumControlGrid, "numeric", ...
    "Value", 1100, "Limits", [0, Inf]);
setLayout(integrationEndField, 4, 4);
spectrumComputeButton = uibutton(spectrumControlGrid, ...
    "Text", "计算积分 J", "ButtonPushedFcn", @onComputeSpectrum);
setLayout(spectrumComputeButton, 4, [5, 8]);

spectrumAxes = uiaxes(spectrumGrid);
xlabel(spectrumAxes, "波长 (nm)");
ylabel(spectrumAxes, "光谱辐照度 (W m^{-2} nm^{-1})");
title(spectrumAxes, "光谱与样品 IPCE");
grid(spectrumAxes, "on");
cumulativeAxes = uiaxes(spectrumGrid);
xlabel(cumulativeAxes, "波长 (nm)");
ylabel(cumulativeAxes, "累计积分电流密度 (mA cm^{-2})");
title(cumulativeAxes, "累计积分电流密度随波长变化");
grid(cumulativeAxes, "on");
spectrumResultTable = uitable(spectrumGrid, "Data", table());

tableTab = uitab(tabGroup, "Title", "结果表");
tableGrid = uigridlayout(tableTab, [1, 1]);
resultTable = uitable(tableGrid, "Data", table());

addAnalysisToolbar(alignmentAxes);
addAnalysisToolbar(powerAxes);
addAnalysisToolbar(ipceAxes);
addAnalysisToolbar(spectrumAxes);
addAnalysisToolbar(cumulativeAxes);

dynamicComponents = { ...
    statusLabel, parameterTitle, ...
    calibrationPathLabel, siliconPathLabel, samplePathLabel, ...
    externalIPCEPathLabel, spectrumPathLabel, ...
    spectrumWavelengthColumnDropDown, ...
    spectrumIrradianceColumnDropDown, ...
    resultTable, spectrumResultTable};
localizationRegistry = captureLocalizationRegistry( ...
    appFigure, dynamicComponents);
applyLanguage(currentLanguage, false);
appFigure.UserData = struct( ...
    "SetLanguage", @setLanguageForTest, ...
    "GetLanguage", @()currentLanguage, ...
    "StateSignature", @()state, ...
    "VisibleTexts", @()visibleTextsForTest(), ...
    "SetStatusForTest", @setLocalizedStatus, ...
    "SetDynamicSurfaceForTest", @setDynamicSurfaceForTest, ...
    "DynamicSurfaceSnapshot", @dynamicSurfaceSnapshot, ...
    "OpenAnchorDialogForTest", @openNewAnchorDialog, ...
    "OpenExportDialogForTest", @openExportDialog, ...
    "PopulateExternalWorkflowForTest", @populateExternalWorkflowForTest, ...
    "PlotTexts", @plotTextsForTest);

onAlignmentModeChanged();
plotAlignmentPreview();
autoLoadWorkspaceFiles();
loadAxisSettings();
applyLanguage(currentLanguage, false);
if isdeployed && nargout == 0
    waitfor(appFigure);
end

    function onLanguageChanged(~, ~)
        applyLanguage(string(languageDropDown.Value), true);
    end

    function setLanguageForTest(language)
        applyLanguage(language, false);
    end

    function applyLanguage(language, persistPreference)
        if startsWith(lower(string(language)), "zh")
            currentLanguage = "zh-CN";
        else
            currentLanguage = "en-US";
        end
        applyLocalizationRegistry(localizationRegistry, currentLanguage);
        refreshDynamicControls();
        renderLocalizedStatus();
        refreshLocalizedPlots();
        languageDropDown.Items = ["English", "中文"];
        languageDropDown.ItemsData = ["en-US", "zh-CN"];
        languageDropDown.Value = currentLanguage;
        if persistPreference
            ipceLanguagePreference( ...
                "save", languagePreferencePath, currentLanguage);
        end
        drawnow limitrate
    end

    function output = localized(source, varargin)
        output = ipceLocalizeLiteral(currentLanguage, source);
        if ~isempty(varargin)
            arguments = varargin;
            localizableArguments = [ ...
                "标探", "硅标探", "样品", ...
                "本软件计算结果", "外部导入 IPCE"];
            for argumentIndex = 1:numel(arguments)
                argument = arguments{argumentIndex};
                if (ischar(argument) || (isstring(argument) && isscalar(argument))) && ...
                        any(string(argument) == localizableArguments)
                    arguments{argumentIndex} = ...
                        ipceLocalizeLiteral(currentLanguage, argument);
                end
            end
            output = string(sprintf(char(output), arguments{:}));
        end
    end

    function setLocalizedStatus(source, varargin)
        statusSource = string(source);
        statusArguments = varargin;
        renderLocalizedStatus();
    end

    function renderLocalizedStatus()
        if statusSource == "__IPCE_ERROR__"
            errorText = ipceLocalizeException(currentLanguage, ...
                statusArguments{1}, statusArguments{2});
            statusLabel.Text = localized("错误：%s", errorText);
        else
            statusLabel.Text = localized(statusSource, statusArguments{:});
        end
    end

    function setLocalizedErrorStatus(exception)
        statusSource = "__IPCE_ERROR__";
        statusArguments = {string(exception.identifier), string(exception.message)};
        renderLocalizedStatus();
    end

    function output = localizedException(exception)
        output = ipceLocalizeException(currentLanguage, ...
            string(exception.identifier), string(exception.message));
    end

    function refreshDynamicControls()
        refreshFilePlaceholder(calibrationPathLabel, "尚未导入");
        refreshFilePlaceholder(siliconPathLabel, "尚未导入");
        refreshFilePlaceholder(samplePathLabel, "尚未导入");
        refreshFilePlaceholder(externalIPCEPathLabel, "尚未导入外部 IPCE");
        refreshFilePlaceholder(spectrumPathLabel, "尚未导入");
        refreshSpectrumHeaderPlaceholder(spectrumWavelengthColumnDropDown);
        refreshSpectrumHeaderPlaceholder(spectrumIrradianceColumnDropDown);
        loadScanProfile(string(scanTargetDropDown.Value));
    end

    function refreshFilePlaceholder(label, chineseSource)
        currentText = string(label.Text);
        englishText = string(ipceLocalizeLiteral("en-US", chineseSource));
        if currentText == chineseSource || currentText == englishText
            label.Text = localized(chineseSource);
        end
    end

    function refreshSpectrumHeaderPlaceholder(dropDown)
        items = string(dropDown.Items);
        chineseSource = "尚未读取表头";
        englishText = string(ipceLocalizeLiteral("en-US", chineseSource));
        if numel(items) == 1 && ...
                (items(1) == chineseSource || items(1) == englishText)
            dropDown.Items = localized(chineseSource);
        end
    end

    function setDynamicSurfaceForTest(filePath, labels, indices)
        updateFileLabel(calibrationPathLabel, string(filePath));
        spectrumWavelengthColumnDropDown.Items = string(labels);
        spectrumWavelengthColumnDropDown.ItemsData = indices;
        spectrumWavelengthColumnDropDown.Value = indices(1);
        spectrumIrradianceColumnDropDown.Items = string(labels);
        spectrumIrradianceColumnDropDown.ItemsData = indices;
        spectrumIrradianceColumnDropDown.Value = indices(end);
    end

    function snapshot = dynamicSurfaceSnapshot()
        snapshot = struct( ...
            "CalibrationText", string(calibrationPathLabel.Text), ...
            "CalibrationTooltip", string(calibrationPathLabel.Tooltip), ...
            "WavelengthItems", string(spectrumWavelengthColumnDropDown.Items), ...
            "WavelengthItemsData", spectrumWavelengthColumnDropDown.ItemsData, ...
            "WavelengthValue", spectrumWavelengthColumnDropDown.Value, ...
            "IrradianceItems", string(spectrumIrradianceColumnDropDown.Items), ...
            "IrradianceItemsData", spectrumIrradianceColumnDropDown.ItemsData, ...
            "IrradianceValue", spectrumIrradianceColumnDropDown.Value);
    end

    function populateExternalWorkflowForTest()
        state.externalIPCE = table( ...
            [400; 500; 600], [40; 55; 65], ...
            'VariableNames', {'Wavelength_nm', 'IPCE_percent'});
        state.externalIPCEFile = "external_test.csv";
        state.spectrum = table( ...
            [400; 500; 600], [1.0; 1.1; 1.0], ...
            'VariableNames', ...
            {'Wavelength_nm', 'Irradiance_W_m2_nm'});
        state.spectrumFile = "spectrum_test.csv";
        state.spectrumIPCESource = "external";
        ipceSourceDropDown.Value = "external";
        [state.spectrumSummary, state.spectrumCurve] = ...
            ipceIntegrateSpectrum(state.externalIPCE, state.spectrum, 400, 600);
        spectrumResultTable.Data = state.spectrumSummary;
        updateFileLabel(externalIPCEPathLabel, state.externalIPCEFile);
        updateFileLabel(spectrumPathLabel, state.spectrumFile);
        plotSpectrumPreview();
    end

    function texts = plotTextsForTest()
        texts = strings(0, 1);
        axesHandles = [siliconAxes, sampleAxes, alignmentAxes, ...
            powerAxes, ipceAxes, spectrumAxes, cumulativeAxes];
        for axesIndex = 1:numel(axesHandles)
            axesHandle = axesHandles(axesIndex);
            texts = [texts; string(axesHandle.Title.String); ...
                string(axesHandle.XLabel.String); ...
                string(axesHandle.YLabel.String)]; %#ok<AGROW>
            children = findall(axesHandle);
            for childIndex = 1:numel(children)
                child = children(childIndex);
                if isprop(child, "DisplayName")
                    displayName = string(child.DisplayName);
                    if isscalar(displayName) && strlength(displayName) > 0
                        texts(end + 1, 1) = displayName; %#ok<AGROW>
                    end
                end
            end
        end
    end

    function refreshLocalizedPlots()
        refreshTracePlots();
        if isempty(state.siliconTrace)
            xlabel(siliconAxes, localized("时间 (s)"));
            ylabel(siliconAxes, localized("电流 (A)"));
            title(siliconAxes, localized("硅标探 i-t"));
        end
        if isempty(state.sampleTrace)
            xlabel(sampleAxes, localized("时间 (s)"));
            ylabel(sampleAxes, localized("电流 (A)"));
            title(sampleAxes, localized("样品 i-t"));
        end
        plotAlignmentPreview();
        plotSpectrumPreview();
        if isempty(state.lightResult)
            xlabel(powerAxes, localized("波长 (nm)"));
            ylabel(powerAxes, localized("入射功率密度 (\muW cm^{-2})"));
            title(powerAxes, localized("由硅标探反算的单色光功率密度"));
        else
            plotPower();
        end
        if isempty(state.ipceResult)
            xlabel(ipceAxes, localized("波长 (nm)"));
            ylabel(ipceAxes, localized("IPCE (%)"));
            if isempty(state.lightResult)
                title(ipceAxes, localized("样品 IPCE"));
            else
                title(ipceAxes, localized("请导入样品 i-t 后计算 IPCE"));
            end
        else
            plot(ipceAxes, state.ipceResult.Wavelength_nm, ...
                state.ipceResult.IPCE_percent, "-o", ...
                "LineWidth", 1.3, "MarkerSize", 3.5);
            xlabel(ipceAxes, localized("波长 (nm)"));
            ylabel(ipceAxes, localized("IPCE (%)"));
            title(ipceAxes, localized("样品 IPCE"));
            grid(ipceAxes, "on");
        end
    end

    function texts = visibleTextsForTest()
        texts = collectLocalizedTexts(localizationRegistry);
        texts = [texts; ...
            string(statusLabel.Text); string(parameterTitle.Text); ...
            placeholderTextForAudit(calibrationPathLabel, "尚未导入"); ...
            placeholderTextForAudit(siliconPathLabel, "尚未导入"); ...
            placeholderTextForAudit(samplePathLabel, "尚未导入"); ...
            placeholderTextForAudit(externalIPCEPathLabel, "尚未导入外部 IPCE"); ...
            placeholderTextForAudit(spectrumPathLabel, "尚未导入"); ...
            spectrumPlaceholderForAudit(spectrumWavelengthColumnDropDown); ...
            spectrumPlaceholderForAudit(spectrumIrradianceColumnDropDown); ...
            string(resultTable.ColumnName(:)); ...
            string(spectrumResultTable.ColumnName(:))];
    end

    function output = placeholderTextForAudit(label, chineseSource)
        currentText = string(label.Text);
        englishText = string(ipceLocalizeLiteral("en-US", chineseSource));
        if currentText == chineseSource || currentText == englishText
            output = currentText;
        else
            output = "";
        end
    end

    function output = spectrumPlaceholderForAudit(dropDown)
        items = string(dropDown.Items);
        chineseSource = "尚未读取表头";
        englishText = string(ipceLocalizeLiteral("en-US", chineseSource));
        if numel(items) == 1 && ...
                (items(1) == chineseSource || items(1) == englishText)
            output = items(1);
        else
            output = "";
        end
    end

    function onLoadCalibration(~, ~)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.xlsx;*.xls;*.csv', '标探校准表 (*.xlsx, *.xls, *.csv)'; ...
            '*.*', '所有文件'}), localized('选择硅标探响应度校准表'));
        if isequal(fileName, 0)
            return
        end
        loadCalibration(fullfile(folder, fileName), true);
    end

    function onLoadSilicon(~, ~)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.txt;*.csv;*.xlsx;*.xls', 'i-t 数据'; '*.*', '所有文件'}), ...
            localized('选择硅标探 i-t 文件'));
        if isequal(fileName, 0)
            return
        end
        loadSilicon(fullfile(folder, fileName), true);
    end

    function onLoadSample(~, ~)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.txt;*.csv;*.xlsx;*.xls', 'i-t 数据'; '*.*', '所有文件'}), ...
            localized('选择样品 i-t 文件'));
        if isequal(fileName, 0)
            return
        end
        loadSample(fullfile(folder, fileName), true);
    end

    function onLoadExternalIPCE(~, ~)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.txt;*.csv;*.xlsx;*.xls', ...
            '两列 IPCE 数据 (*.txt, *.csv, *.xlsx, *.xls)'; ...
            '*.*', '所有文件'}), ...
            localized('选择外部 IPCE 文件（第 1 列 nm，第 2 列 %）'));
        if isequal(fileName, 0)
            return
        end
        loadExternalIPCEFile(fullfile(folder, fileName), true);
    end

    function onLoadSpectrum(~, ~)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.xls;*.xlsx;*.csv', '光谱数据 (*.xls, *.xlsx, *.csv)'; ...
            '*.*', '所有文件'}), localized('选择标准光谱文件'));
        if isequal(fileName, 0)
            return
        end
        loadSpectrumFile(fullfile(folder, fileName), true);
    end

    function onLoadAnchors(target)
        [fileName, folder] = uigetfile( ...
            ipceLocalizeLiteral(currentLanguage, ...
            {'*.txt;*.csv;*.xlsx;*.xls', ...
            '两列锚点数据 (*.txt, *.csv, *.xlsx, *.xls)'; ...
            '*.*', '所有文件'}), ...
            localized('选择%s波长–时间锚点文件', ...
            localized(targetDisplayName(target))));
        if isequal(fileName, 0)
            return
        end
        try
            anchors = ipceReadAnchors(string(fullfile(folder, fileName)));
            if target == "silicon"
                siliconAnchorTable.Data = anchors;
                state.siliconAnchorRow = 1;
            else
                sampleAnchorTable.Data = anchors;
                state.sampleAnchorRow = 1;
            end
            plotAlignmentPreview();
            setLocalizedStatus( ...
                '已导入%s锚点：%d 行（第 1 列波长 nm，第 2 列确认时间 s）。', ...
                targetDisplayName(target), size(anchors, 1));
        catch exception
            showError(exception);
        end
    end

    function loadCalibration(filePath, showDialog)
        try
            state.calibration = ipceReadReference(string(filePath));
            state.calibrationFile = string(filePath);
            updateFileLabel(calibrationPathLabel, state.calibrationFile);
            setLocalizedStatus( ...
                "已读取标探响应度：%d 点，%.6g–%.6g nm。", ...
                height(state.calibration), ...
                min(state.calibration.Wavelength_nm), ...
                max(state.calibration.Wavelength_nm));
        catch exception
            if showDialog
                showError(exception);
            end
        end
    end

    function loadSilicon(filePath, showDialog)
        try
            [importedTrace, cancelled] = readTraceForImport( ...
                string(filePath), showDialog);
            if cancelled
                return
            end
            state.siliconTrace = importedTrace;
            state.siliconFile = string(filePath);
            state.siliconSchedule = table();
            state.siliconExtracted = table();
            state.lightResult = table();
            state.ipceResult = table();
            invalidateCalculatedIntegration();
            updateFileLabel(siliconPathLabel, state.siliconFile);
            plotTrace(siliconAxes, state.siliconTrace, table(), ...
                traceStartForDisplay("silicon"), "硅标探 i-t", ...
                darkRangeForTarget("silicon"), darkCheckBox.Value, ...
                currentLanguage);
            plotAlignmentPreview();
            setLocalizedStatus(traceSummary("硅标探", state.siliconTrace));
            tabGroup.SelectedTab = siliconTab;
        catch exception
            if showDialog
                showError(exception);
            end
        end
    end

    function loadSample(filePath, showDialog)
        try
            [importedTrace, cancelled] = readTraceForImport( ...
                string(filePath), showDialog);
            if cancelled
                return
            end
            state.sampleTrace = importedTrace;
            state.sampleFile = string(filePath);
            state.sampleSchedule = table();
            state.sampleExtracted = table();
            state.ipceResult = table();
            invalidateCalculatedIntegration();
            updateFileLabel(samplePathLabel, state.sampleFile);
            plotTrace(sampleAxes, state.sampleTrace, table(), ...
                traceStartForDisplay("sample"), "样品 i-t", ...
                darkRangeForTarget("sample"), darkCheckBox.Value, ...
                currentLanguage);
            plotAlignmentPreview();
            setLocalizedStatus(traceSummary("样品", state.sampleTrace));
            tabGroup.SelectedTab = sampleTab;
        catch exception
            if showDialog
                showError(exception);
            end
        end
    end

    function success = loadExternalIPCEFile(filePath, showDialog)
        success = false;
        try
            importedIPCE = ipceReadExternalIPCE(string(filePath));
            state.externalIPCE = importedIPCE;
            state.externalIPCEFile = string(filePath);
            state.spectrumIPCESource = "external";
            state.spectrumSummary = table();
            state.spectrumCurve = table();
            spectrumResultTable.Data = table();
            ipceSourceDropDown.Value = "external";
            updateFileLabel(externalIPCEPathLabel, ...
                state.externalIPCEFile);
            plotSpectrumPreview();
            setLocalizedStatus( ...
                "已读取外部 IPCE：%d 点，%.6g–%.6g nm；数据源已切换为外部导入。", ...
                height(importedIPCE), ...
                min(importedIPCE.Wavelength_nm), ...
                max(importedIPCE.Wavelength_nm));
            tabGroup.SelectedTab = spectrumTab;
            success = true;
        catch exception
            if showDialog
                showError(exception);
            end
        end
    end

    function onIPCESourceChanged(varargin)
        state.spectrumIPCESource = string(ipceSourceDropDown.Value);
        state.spectrumSummary = table();
        state.spectrumCurve = table();
        spectrumResultTable.Data = table();
        plotSpectrumPreview();
        if state.spectrumIPCESource == "external"
            setLocalizedStatus("光谱积分将使用外部导入 IPCE。");
        else
            setLocalizedStatus("光谱积分将使用本软件计算的样品 IPCE。");
        end
    end

    function invalidateCalculatedIntegration()
        if string(ipceSourceDropDown.Value) ~= "calculated"
            return
        end
        state.spectrumSummary = table();
        state.spectrumCurve = table();
        spectrumResultTable.Data = table();
    end

    function [trace, cancelled] = readTraceForImport(filePath, allowPrompt)
        trace = table();
        cancelled = false;
        try
            trace = ipceReadIT(filePath);
            return
        catch exception
            if string(exception.identifier) ~= "IPCE:TraceUnitsRequired" || ...
                    ~allowPrompt
                rethrow(exception);
            end
        end

        cancelText = localized("取消");
        timeUnit = string(uiconfirm(appFigure, ...
            localizedException(exception) + newline + ...
            localized("请选择原始时间列单位。"), ...
            localized("选择时间单位"), ...
            "Options", ["s", "ms", "min", "h", cancelText], ...
            "DefaultOption", "s", "CancelOption", cancelText));
        if timeUnit == cancelText
            cancelled = true;
            return
        end
        currentUnit = string(uiconfirm(appFigure, ...
            localized("请选择原始电流列单位。导入后将统一换算为 A。"), ...
            localized("选择电流单位"), ...
            "Options", ["A", "mA", "uA", "nA", "pA", cancelText], ...
            "DefaultOption", "A", "CancelOption", cancelText));
        if currentUnit == cancelText
            cancelled = true;
            return
        end
        trace = ipceReadIT(filePath, ...
            TimeUnit=timeUnit, CurrentUnit=currentUnit);
    end

    function success = loadSpectrumFile(filePath, showDialog)
        success = false;
        try
            filePath = string(filePath);
            preserveSelection = state.spectrumFile ~= "" && ...
                state.spectrumFile == filePath;
            updateSpectrumColumnSelectors(filePath, ...
                string(spectrumSheetField.Value), preserveSelection);
            wavelengthColumn = spectrumWavelengthColumnDropDown.Value;
            irradianceColumn = spectrumIrradianceColumnDropDown.Value;
            if wavelengthColumn == irradianceColumn
                error("IPCE:DuplicateSpectrumColumns", ...
                    "波长列和积分列不能选择同一列。");
            end
            state.spectrum = ipceReadSpectrum(string(filePath), ...
                string(spectrumSheetField.Value), ...
                wavelengthColumn, irradianceColumn);
            state.spectrumFile = filePath;
            state.spectrumSummary = table();
            state.spectrumCurve = table();
            updateFileLabel(spectrumPathLabel, state.spectrumFile);
            wavelengthHeader = selectedSpectrumHeader(wavelengthColumn);
            irradianceHeader = selectedSpectrumHeader(irradianceColumn);
            setLocalizedStatus( ...
                "已读取光谱：%d 点，%.6g–%.6g nm；波长列“%s”，积分列“%s”。", ...
                height(state.spectrum), min(state.spectrum.Wavelength_nm), ...
                max(state.spectrum.Wavelength_nm), ...
                wavelengthHeader, irradianceHeader);
            plotSpectrumPreview();
            success = true;
        catch exception
            if showDialog
                showError(exception);
            end
        end
    end

    function onSpectrumSelectionChanged(varargin)
        if state.spectrumFile == ""
            return
        end
        loadSpectrumFile(state.spectrumFile, true);
    end

    function updateSpectrumColumnSelectors(filePath, sheetName, preserve)
        previousWavelength = spectrumWavelengthColumnDropDown.Value;
        previousIrradiance = spectrumIrradianceColumnDropDown.Value;
        columns = ipceReadSpectrumHeaders(filePath, sheetName);
        state.spectrumColumns = columns;
        indices = columns.ColumnIndex(:)';
        labels = columns.DisplayName(:)';
        spectrumWavelengthColumnDropDown.Items = labels;
        spectrumWavelengthColumnDropDown.ItemsData = indices;
        spectrumIrradianceColumnDropDown.Items = labels;
        spectrumIrradianceColumnDropDown.ItemsData = indices;

        if preserve && ismember(previousWavelength, indices)
            wavelengthColumn = previousWavelength;
        else
            headerLower = lower(columns.Header);
            match = find(contains(headerLower, "wavelength") | ...
                contains(columns.Header, "波长") | ...
                contains(headerLower, "lambda"), 1);
            if isempty(match)
                match = 1;
            end
            wavelengthColumn = columns.ColumnIndex(match);
        end

        if preserve && ismember(previousIrradiance, indices)
            irradianceColumn = previousIrradiance;
        else
            headerLower = lower(columns.Header);
            match = find(contains(headerLower, "global tilt"), 1);
            if isempty(match)
                match = find(contains(headerLower, "irradiance") | ...
                    contains(columns.Header, "辐照"), 1);
            end
            if isempty(match)
                match = find(columns.ColumnIndex == 3, 1);
            end
            if isempty(match)
                match = find(columns.ColumnIndex ~= wavelengthColumn, 1);
            end
            if isempty(match)
                match = 1;
            end
            irradianceColumn = columns.ColumnIndex(match);
        end

        spectrumWavelengthColumnDropDown.Value = wavelengthColumn;
        spectrumIrradianceColumnDropDown.Value = irradianceColumn;
    end

    function header = selectedSpectrumHeader(columnIndex)
        if isempty(state.spectrumColumns) || ...
                ~ismember("ColumnIndex", ...
                string(state.spectrumColumns.Properties.VariableNames))
            match = [];
        else
            match = find(state.spectrumColumns.ColumnIndex == columnIndex, 1);
        end
        if isempty(match)
            header = sprintf("第 %d 列", columnIndex);
        else
            header = char(state.spectrumColumns.Header(match));
        end
    end

    function onComputeSpectrum(~, ~)
        try
            state.spectrumIPCESource = string(ipceSourceDropDown.Value);
            [selectedIPCE, sourceLabel] = ipceResolveIPCESource( ...
                state.ipceResult, state.externalIPCE, ...
                state.spectrumIPCESource);
            if state.spectrumFile ~= ""
                if ~loadSpectrumFile(state.spectrumFile, false)
                    error("IPCE:SpectrumReloadFailed", ...
                        "无法按当前表格/列设置重新读取光谱文件。");
                end
            end
            if isempty(state.spectrum)
                error("IPCE:MissingSpectrum", "请先导入标准光谱数据。");
            end
            [state.spectrumSummary, state.spectrumCurve] = ...
                ipceIntegrateSpectrum(selectedIPCE, state.spectrum, ...
                integrationStartField.Value, integrationEndField.Value);
            state.spectrumSummary.Properties.UserData.IPCESource = ...
                char(sourceLabel);
            state.spectrumCurve.Properties.UserData.IPCESource = ...
                char(sourceLabel);
            if state.spectrumIPCESource == "external"
                sourceFile = state.externalIPCEFile;
            else
                sourceFile = state.sampleFile;
            end
            state.spectrumSummary.Properties.UserData.IPCESourceFile = ...
                char(sourceFile);
            state.spectrumCurve.Properties.UserData.IPCESourceFile = ...
                char(sourceFile);
            spectrumResultTable.Data = state.spectrumSummary;
            plotSpectrumPreview();
            currentDensity = ...
                state.spectrumSummary.IntegratedCurrentDensity_mA_cm2(1);
            setLocalizedStatus( ...
                "光谱积分完成（%s）：%.6g–%.6g nm，J = %.6g mA cm^{-2}。", ...
                sourceLabel, ...
                integrationStartField.Value, integrationEndField.Value, ...
                currentDensity);
            tabGroup.SelectedTab = spectrumTab;
        catch exception
            showError(exception);
        end
    end

    function onComputeLight(~, ~)
        try
            [wavelengths, ~, settings] = currentSettings();
            requireInputs(false);
            state.siliconSchedule = ipceBuildSchedule( ...
                wavelengths, settings.AlignmentMode, ...
                settings.SiliconAnchors, settings.SiliconStartTime_s, ...
                settings.SiliconDelay_s);
            state.siliconExtracted = ipceExtractSchedule( ...
                state.siliconTrace, state.siliconSchedule, ...
                settings.SiliconAverageTime_s, ...
                settings.SubtractDark, settings.SiliconDarkRange_s);
            [state.lightResult, ~] = ipceCalculate( ...
                state.calibration, state.siliconExtracted, table(), ...
                settings.SiliconIlluminatedArea_cm2, ...
                settings.SampleIlluminatedArea_cm2);
            state.ipceResult = table();
            invalidateCalculatedIntegration();
            plotTrace(siliconAxes, state.siliconTrace, ...
                state.siliconExtracted, state.siliconSchedule.ReferenceTime_s(1), ...
                "硅标探 i-t", settings.SiliconDarkRange_s, ...
                settings.SubtractDark, currentLanguage);
            plotPower();
            cla(ipceAxes);
            title(ipceAxes, localized("请导入样品 i-t 后计算 IPCE"));
            grid(ipceAxes, "on");
            resultTable.Data = state.lightResult;
            setLocalizedStatus( ...
                "功率密度计算完成：%d 个波长，调度 %.3f–%.3f s，功率密度 %.4g–%.4g μW cm^{-2}。", ...
                height(state.lightResult), ...
                state.siliconSchedule.WindowStart_s(1), ...
                state.siliconSchedule.WindowEnd_s(end), ...
                min(state.lightResult.IncidentPowerDensity_W_cm2) * 1e6, ...
                max(state.lightResult.IncidentPowerDensity_W_cm2) * 1e6);
            plotAlignmentPreview();
            tabGroup.SelectedTab = resultTab;
        catch exception
            showError(exception);
        end
    end

    function onComputeIPCE(~, ~)
        try
            [siliconWavelengths, sampleWavelengths, settings] = currentSettings();
            requireInputs(true);
            state.siliconSchedule = ipceBuildSchedule( ...
                siliconWavelengths, settings.AlignmentMode, ...
                settings.SiliconAnchors, settings.SiliconStartTime_s, ...
                settings.SiliconDelay_s);
            state.sampleSchedule = ipceBuildSchedule( ...
                sampleWavelengths, settings.AlignmentMode, ...
                settings.SampleAnchors, settings.SampleStartTime_s, ...
                settings.SampleDelay_s);
            state.siliconExtracted = ipceExtractSchedule( ...
                state.siliconTrace, state.siliconSchedule, ...
                settings.SiliconAverageTime_s, ...
                settings.SubtractDark, settings.SiliconDarkRange_s);
            state.sampleExtracted = ipceExtractSchedule( ...
                state.sampleTrace, state.sampleSchedule, ...
                settings.SampleAverageTime_s, ...
                settings.SubtractDark, settings.SampleDarkRange_s);
            [state.lightResult, state.ipceResult] = ipceCalculate( ...
                state.calibration, state.siliconExtracted, ...
                state.sampleExtracted, ...
                settings.SiliconIlluminatedArea_cm2, ...
                settings.SampleIlluminatedArea_cm2);
            invalidateCalculatedIntegration();
            plotTrace(siliconAxes, state.siliconTrace, ...
                state.siliconExtracted, state.siliconSchedule.ReferenceTime_s(1), ...
                "硅标探 i-t", settings.SiliconDarkRange_s, ...
                settings.SubtractDark, currentLanguage);
            plotTrace(sampleAxes, state.sampleTrace, state.sampleExtracted, ...
                state.sampleSchedule.ReferenceTime_s(1), "样品 i-t", ...
                settings.SampleDarkRange_s, settings.SubtractDark, ...
                currentLanguage);
            plotPower();
            plotSpectrumPreview();
            plot(ipceAxes, state.ipceResult.Wavelength_nm, ...
                state.ipceResult.IPCE_percent, "-o", ...
                "LineWidth", 1.3, "MarkerSize", 3.5);
            xlabel(ipceAxes, localized("波长 (nm)"));
            ylabel(ipceAxes, localized("IPCE (%)"));
            title(ipceAxes, localized("样品 IPCE"));
            grid(ipceAxes, "on");
            resultTable.Data = state.ipceResult;
            setLocalizedStatus( ...
                "IPCE 计算完成：%d 个波长。中位数 %.4g%%，最大值 %.4g%%。", ...
                height(state.ipceResult), ...
                median(state.ipceResult.IPCE_percent, "omitnan"), ...
                max(state.ipceResult.IPCE_percent, [], "omitnan"));
            plotAlignmentPreview();
            tabGroup.SelectedTab = resultTab;
        catch exception
            showError(exception);
        end
    end

    function onExport(~, ~)
        if isempty(state.lightResult) && isempty(state.ipceResult) && ...
                isempty(state.externalIPCE) && ...
                isempty(state.spectrumSummary)
            uialert(appFigure, ...
                localized("请先计算功率密度/IPCE，或导入外部 IPCE。"), ...
                localized("没有可导出的结果"));
            return
        end

        openExportDialog();
    end

    function dialog = openExportDialog()
        dialog = uifigure("Name", localized("选择导出内容"), ...
            "Position", [430, 160, 440, 480], ...
            "WindowStyle", "modal", "Resize", "off");
        gridLayout = uigridlayout(dialog, [11, 2]);
        gridLayout.RowHeight = ...
            {34, 32, 32, 32, 32, 32, 32, 32, 32, 36, 40};
        gridLayout.ColumnWidth = {"1x", "1x"};
        heading = uilabel(gridLayout, "Text", localized("选择文件格式和输出参数"), ...
            "FontSize", 15, "FontWeight", "bold");
        setLayout(heading, 1, [1, 2]);
        formatLabel = uilabel(gridLayout, "Text", localized("文件格式"));
        setLayout(formatLabel, 2, 1);
        formatDropDown = uidropdown(gridLayout, ...
            "Items", [localized("Excel 工作簿"), "CSV", "MATLAB MAT"], ...
            "ItemsData", ["xlsx", "csv", "mat"], "Value", "xlsx");
        setLayout(formatDropDown, 2, 2);

        lightCheck = uicheckbox(gridLayout, "Text", localized("标探功率密度结果"), ...
            "Value", ~isempty(state.lightResult), ...
            "Enable", onOff(~isempty(state.lightResult)));
        setLayout(lightCheck, 3, [1, 2]);
        ipceCheck = uicheckbox(gridLayout, "Text", localized("样品 IPCE 结果"), ...
            "Value", ~isempty(state.ipceResult), ...
            "Enable", onOff(~isempty(state.ipceResult)));
        setLayout(ipceCheck, 4, [1, 2]);
        externalIPCECheck = uicheckbox(gridLayout, ...
            "Text", localized("外部导入 IPCE"), ...
            "Value", ~isempty(state.externalIPCE), ...
            "Enable", onOff(~isempty(state.externalIPCE)));
        setLayout(externalIPCECheck, 5, [1, 2]);
        integrationCheck = uicheckbox(gridLayout, ...
            "Text", localized("光谱积分汇总与积分曲线"), ...
            "Value", ~isempty(state.spectrumSummary), ...
            "Enable", onOff(~isempty(state.spectrumSummary)));
        setLayout(integrationCheck, 6, [1, 2]);
        parameterCheck = uicheckbox(gridLayout, ...
            "Text", localized("参数、源文件和可用锚点"), "Value", true);
        setLayout(parameterCheck, 7, [1, 2]);
        extractedCheck = uicheckbox(gridLayout, ...
            "Text", localized("标探/样品窗口提取统计"), ...
            "Value", false, ...
            "Enable", onOff(~isempty(state.siliconExtracted) || ...
            ~isempty(state.sampleExtracted)));
        setLayout(extractedCheck, 8, [1, 2]);
        detailedCheck = uicheckbox(gridLayout, ...
            "Text", localized("保留详细误差、符号电流和采样数列"), "Value", true);
        setLayout(detailedCheck, 9, [1, 2]);
        note = uilabel(gridLayout, ...
            "Text", localized("CSV 选择多项时会输出多个带后缀的文件。导出成功后会显示绝对路径。"), ...
            "WordWrap", "on", "FontColor", [0.35, 0.35, 0.35]);
        setLayout(note, 10, [1, 2]);
        cancelButton = uibutton(gridLayout, "Text", localized("取消"), ...
            "ButtonPushedFcn", @(~, ~)delete(dialog));
        setLayout(cancelButton, 11, 1);
        confirmButton = uibutton(gridLayout, "Text", localized("选择路径并导出"), ...
            "ButtonPushedFcn", @(~, ~)performExport(dialog, ...
            string(formatDropDown.Value), lightCheck.Value, ...
            ipceCheck.Value, externalIPCECheck.Value, ...
            integrationCheck.Value, ...
            parameterCheck.Value, extractedCheck.Value, ...
            detailedCheck.Value));
        setLayout(confirmButton, 11, 2);
    end

    function performExport(dialog, format, includeLight, includeIPCE, ...
            includeExternalIPCE, includeIntegration, includeParameters, ...
            includeExtracted, detailed)
        try
            items = struct("Name", {}, "Data", {});
            if includeLight && ~isempty(state.lightResult)
                data = state.lightResult;
                if ~detailed
                    data = data(:, {'Wavelength_nm', ...
                        'SiResponsivity_A_per_W', 'SiPhotocurrent_A', ...
                        'IncidentPowerDensity_W_cm2'});
                end
                items(end + 1) = struct( ...
                    "Name", "SiPowerDensity", "Data", data);
            end
            if includeIPCE && ~isempty(state.ipceResult)
                data = state.ipceResult;
                if ~detailed
                    data = data(:, {'Wavelength_nm', ...
                        'IncidentPowerDensity_W_cm2', ...
                        'SamplePhotocurrentDensity_A_cm2', ...
                        'IPCE_percent'});
                end
                items(end + 1) = struct("Name", "SampleIPCE", "Data", data);
            end
            postprocessItems = ipceBuildPostprocessExportItems( ...
                state.externalIPCE, state.spectrumSummary, ...
                state.spectrumCurve, includeExternalIPCE, ...
                includeIntegration);
            items = [items, postprocessItems];
            if includeParameters
                needsMeasurementSettings = includeLight || includeIPCE || ...
                    includeExtracted;
                if needsMeasurementSettings
                    [~, ~, settings] = currentSettings();
                    metadata = settingsTable(settings);
                else
                    settings = struct( ...
                        "SiliconAnchors", zeros(0, 2), ...
                        "SampleAnchors", zeros(0, 2));
                    metadata = postprocessSettingsTable();
                end
                items(end + 1) = struct( ...
                    "Name", "Parameters", "Data", metadata);
                if ~isempty(settings.SiliconAnchors)
                    items(end + 1) = struct("Name", "SiliconAnchors", ...
                        "Data", anchorOutputTable(settings.SiliconAnchors));
                end
                if ~isempty(settings.SampleAnchors)
                    items(end + 1) = struct("Name", "SampleAnchors", ...
                        "Data", anchorOutputTable(settings.SampleAnchors));
                end
            end
            if includeExtracted && ~isempty(state.siliconExtracted)
                items(end + 1) = struct("Name", "SiExtraction", ...
                    "Data", state.siliconExtracted);
            end
            if includeExtracted && ~isempty(state.sampleExtracted)
                items(end + 1) = struct("Name", "SampleExtraction", ...
                    "Data", state.sampleExtracted);
            end
            if isempty(items)
                error("IPCE:NoExportSelection", "至少选择一项输出内容。");
            end

            switch format
                case "xlsx"
                    filter = {'*.xlsx', 'Excel 工作簿 (*.xlsx)'};
                    defaultName = "IPCE_export.xlsx";
                case "csv"
                    filter = {'*.csv', 'CSV 文本 (*.csv)'};
                    defaultName = "IPCE_export.csv";
                case "mat"
                    filter = {'*.mat', 'MATLAB 数据 (*.mat)'};
                    defaultName = "IPCE_export.mat";
                otherwise
                    error("IPCE:UnsupportedExport", "不支持的导出格式：%s", format);
            end
            filter = ipceLocalizeLiteral(currentLanguage, filter);
            [fileName, folder] = uiputfile(filter, ...
                localized('选择导出路径'), char(defaultName));
            if isequal(fileName, 0)
                return
            end
            outputPath = string(fullfile(folder, fileName));
            writtenPaths = ipceWriteExport(items, outputPath, format);
            if isvalid(dialog)
                delete(dialog);
            end
            pathText = strjoin(writtenPaths, newline);
            setLocalizedStatus("导出成功：%s", writtenPaths(1));
            uialert(appFigure, localized("文件已写入：") + newline + pathText, ...
                localized("导出成功"), "Icon", "success");
        catch exception
            if isvalid(dialog)
                uialert(dialog, localized(exception.message), localized("导出失败"));
            else
                showError(exception);
            end
        end
    end

    function value = onOff(condition)
        if condition
            value = "on";
        else
            value = "off";
        end
    end

    function onScanTargetChanged(~, event)
        previousTarget = string(event.PreviousValue);
        commitScanProfile(previousTarget);
        loadScanProfile(string(scanTargetDropDown.Value));
        plotAlignmentPreview();
    end

    function onScanFieldChanged(varargin)
        commitScanProfile(string(scanTargetDropDown.Value));
        plotAlignmentPreview();
    end

    function commitScanProfile(target)
        profile = struct( ...
            "Start_nm", waveStartField.Value, ...
            "End_nm", waveEndField.Value, ...
            "Step_nm", waveStepField.Value, ...
            "Delay_s", dwellField.Value, ...
            "Average_s", tailField.Value);
        if target == "silicon"
            state.siliconScan = profile;
        else
            state.sampleScan = profile;
        end
    end

    function loadScanProfile(target)
        if target == "silicon"
            profile = state.siliconScan;
            parameterTitle.Text = localized("扫描与取点参数（标探）");
        else
            profile = state.sampleScan;
            parameterTitle.Text = localized("扫描与取点参数（样品）");
        end
        waveStartField.Value = profile.Start_nm;
        waveEndField.Value = profile.End_nm;
        waveStepField.Value = profile.Step_nm;
        dwellField.Value = profile.Delay_s;
        tailField.Value = profile.Average_s;
    end

    function onAlignmentModeChanged(varargin)
        fixedMode = string(alignmentModeDropDown.Value) == "fixed";
        if fixedMode
            siliconStartField.Enable = "on";
            sampleStartField.Enable = "on";
            siliconPickButton.Enable = "on";
            samplePickButton.Enable = "on";
        else
            siliconStartField.Enable = "off";
            sampleStartField.Enable = "off";
            siliconPickButton.Enable = "off";
            samplePickButton.Enable = "off";
        end
        if isvalid(alignmentAxes)
            plotAlignmentPreview();
        end
    end

    function rememberAnchorRow(event, target)
        if isempty(event.Indices)
            return
        end
        if target == "silicon"
            state.siliconAnchorRow = event.Indices(1, 1);
        else
            state.sampleAnchorRow = event.Indices(1, 1);
        end
    end

    function addAnchorRow(target)
        if target == "silicon"
            data = siliconAnchorTable.Data;
            data(end + 1, :) = [NaN, NaN];
            siliconAnchorTable.Data = data;
            state.siliconAnchorRow = size(data, 1);
        else
            data = sampleAnchorTable.Data;
            data(end + 1, :) = [NaN, NaN];
            sampleAnchorTable.Data = data;
            state.sampleAnchorRow = size(data, 1);
        end
        setLocalizedStatus("已添加锚点行；请填写波长和时间，或填写波长后在图上确认时间。");
    end

    function deleteAnchorRow(target)
        if target == "silicon"
            data = siliconAnchorTable.Data;
            row = state.siliconAnchorRow;
            if isempty(data) || row < 1 || row > size(data, 1)
                return
            end
            data(row, :) = [];
            siliconAnchorTable.Data = data;
            state.siliconAnchorRow = max(1, min(row, size(data, 1)));
        else
            data = sampleAnchorTable.Data;
            row = state.sampleAnchorRow;
            if isempty(data) || row < 1 || row > size(data, 1)
                return
            end
            data(row, :) = [];
            sampleAnchorTable.Data = data;
            state.sampleAnchorRow = max(1, min(row, size(data, 1)));
        end
        plotAlignmentPreview();
    end

    function beginAnchorPick(target)
        if target == "silicon"
            if isempty(state.siliconTrace)
                uialert(appFigure, localized("请先导入硅标探 i-t。"), ...
                    localized("无法确认锚点"));
                return
            end
            data = siliconAnchorTable.Data;
            row = state.siliconAnchorRow;
        else
            if isempty(state.sampleTrace)
                uialert(appFigure, localized("请先导入样品 i-t。"), ...
                    localized("无法确认锚点"));
                return
            end
            data = sampleAnchorTable.Data;
            row = state.sampleAnchorRow;
        end
        if isempty(data) || row < 1 || row > size(data, 1) || ...
                ~isfinite(data(row, 1))
            uialert(appFigure, ...
                localized("请先选择一个锚点行，并在第一列填写波长。"), ...
                localized("锚点波长缺失"));
            return
        end

        state.pickTarget = target + "Anchor";
        appFigure.Pointer = "crosshair";
        if target == "silicon"
            tabGroup.SelectedTab = siliconTab;
        else
            tabGroup.SelectedTab = sampleTab;
        end
        setLocalizedStatus( ...
            "请先放大曲线，再单击一个能确认 %.6g nm 已稳定输出的时间点（%s）。", ...
            data(row, 1), targetDisplayName(target));
    end

    function beginNewAnchorPick(target)
        if target == "silicon" && isempty(state.siliconTrace)
            uialert(appFigure, localized("请先导入硅标探 i-t。"), ...
                localized("无法新增锚点"));
            return
        elseif target == "sample" && isempty(state.sampleTrace)
            uialert(appFigure, localized("请先导入样品 i-t。"), ...
                localized("无法新增锚点"));
            return
        end
        state.pickTarget = target + "NewAnchor";
        appFigure.Pointer = "crosshair";
        if target == "silicon"
            tabGroup.SelectedTab = siliconTab;
        else
            tabGroup.SelectedTab = sampleTab;
        end
        setLocalizedStatus( ...
            "请先用图上工具栏或滚轮放大，再单击目标点；随后可确认/修改时间并输入波长。");
    end

    function beginPick(target)
        if target == "silicon" && isempty(state.siliconTrace)
            uialert(appFigure, localized("请先导入硅标探 i-t。"), ...
                localized("无法选点"));
            return
        elseif target == "sample" && isempty(state.sampleTrace)
            uialert(appFigure, localized("请先导入样品 i-t。"), ...
                localized("无法选点"));
            return
        end
        state.pickTarget = target;
        appFigure.Pointer = "crosshair";
        if target == "silicon"
            tabGroup.SelectedTab = siliconTab;
            setLocalizedStatus("请在“标探 i-t”图上单击第一个波长驻留窗口的起始位置。");
        else
            tabGroup.SelectedTab = sampleTab;
            setLocalizedStatus("请在“样品 i-t”图上单击第一个波长驻留窗口的起始位置。");
        end
    end

    function selectDarkTab()
        tabGroup.SelectedTab = darkTab;
    end

    function beginDarkRangePick(target)
        if target == "silicon" && isempty(state.siliconTrace)
            uialert(appFigure, localized("请先导入硅标探 i-t。"), ...
                localized("无法选择暗区间"));
            return
        elseif target == "sample" && isempty(state.sampleTrace)
            uialert(appFigure, localized("请先导入样品 i-t。"), ...
                localized("无法选择暗区间"));
            return
        end
        state.pickTarget = target + "DarkStart";
        appFigure.Pointer = "crosshair";
        if target == "silicon"
            tabGroup.SelectedTab = siliconTab;
        else
            tabGroup.SelectedTab = sampleTab;
        end
        setLocalizedStatus( ...
            "请在%s i-t 图上单击暗电流区间的起点；随后再选择终点。", ...
            targetDisplayName(target));
    end

    function onDarkRangeChanged(target)
        refreshTracePlot(target);
        range = darkRangeForTarget(target);
        if range(2) > range(1)
            setLocalizedStatus("%s暗电流区间：%.4f–%.4f s。", ...
                targetDisplayName(target), range(1), range(2));
        else
            setLocalizedStatus( ...
                "%s暗电流区间尚未定义：终点必须晚于起点。", ...
                targetDisplayName(target));
        end
    end

    function range = darkRangeForTarget(target)
        if target == "silicon"
            range = [siliconDarkStartField.Value, siliconDarkEndField.Value];
        else
            range = [sampleDarkStartField.Value, sampleDarkEndField.Value];
        end
    end

    function refreshTracePlots()
        refreshTracePlot("silicon");
        refreshTracePlot("sample");
    end

    function refreshTracePlot(target)
        if target == "silicon"
            if isempty(state.siliconTrace)
                return
            end
            plotTrace(siliconAxes, state.siliconTrace, ...
                state.siliconExtracted, traceStartForDisplay("silicon"), ...
                "硅标探 i-t", darkRangeForTarget("silicon"), ...
                darkCheckBox.Value, currentLanguage);
        else
            if isempty(state.sampleTrace)
                return
            end
            plotTrace(sampleAxes, state.sampleTrace, ...
                state.sampleExtracted, traceStartForDisplay("sample"), ...
                "样品 i-t", darkRangeForTarget("sample"), ...
                darkCheckBox.Value, currentLanguage);
        end
    end

    function onAxisTargetChanged(varargin)
        loadAxisSettings();
    end

    function loadAxisSettings()
        [axesHandle, side] = resolveAxisTarget( ...
            string(axisTargetDropDown.Value));
        activateAxisSide(axesHandle, side);
        axisXMinField.Value = axesHandle.XLim(1);
        axisXMaxField.Value = axesHandle.XLim(2);
        axisYMinField.Value = axesHandle.YLim(1);
        axisYMaxField.Value = axesHandle.YLim(2);
        axisXScaleDropDown.Value = string(axesHandle.XScale);
        axisYScaleDropDown.Value = string(axesHandle.YScale);
    end

    function onApplyAxisSettings(~, ~)
        try
            xLimits = [axisXMinField.Value, axisXMaxField.Value];
            yLimits = [axisYMinField.Value, axisYMaxField.Value];
            xScale = string(axisXScaleDropDown.Value);
            yScale = string(axisYScaleDropDown.Value);
            if xLimits(2) <= xLimits(1) || yLimits(2) <= yLimits(1)
                error("IPCE:InvalidAxisLimits", ...
                    "坐标轴最大值必须大于最小值。");
            end
            if xScale == "log" && any(xLimits <= 0)
                error("IPCE:InvalidLogAxis", ...
                    "X 轴使用对数刻度时，显示范围必须全部大于 0。");
            end
            if yScale == "log" && any(yLimits <= 0)
                error("IPCE:InvalidLogAxis", ...
                    "Y 轴使用对数刻度时，显示范围必须全部大于 0。");
            end
            [axesHandle, side] = resolveAxisTarget( ...
                string(axisTargetDropDown.Value));
            activateAxisSide(axesHandle, side);
            axesHandle.XScale = xScale;
            axesHandle.YScale = yScale;
            axesHandle.XLim = xLimits;
            axesHandle.YLim = yLimits;
            setLocalizedStatus("已应用图形显示范围和刻度类型。");
        catch exception
            showError(exception);
        end
    end

    function onAutoAxisSettings(~, ~)
        try
            [axesHandle, side] = resolveAxisTarget( ...
                string(axisTargetDropDown.Value));
            activateAxisSide(axesHandle, side);
            axesHandle.XScale = string(axisXScaleDropDown.Value);
            axesHandle.YScale = string(axisYScaleDropDown.Value);
            xlim(axesHandle, "auto");
            ylim(axesHandle, "auto");
            drawnow;
            loadAxisSettings();
            setLocalizedStatus("已按当前数据自动选择显示范围。");
        catch exception
            showError(exception);
        end
    end

    function [axesHandle, side] = resolveAxisTarget(target)
        side = "";
        switch target
            case "silicon"
                axesHandle = siliconAxes;
            case "sample"
                axesHandle = sampleAxes;
            case "alignment"
                axesHandle = alignmentAxes;
            case "power"
                axesHandle = powerAxes;
            case "ipce"
                axesHandle = ipceAxes;
            case "spectrum-left"
                axesHandle = spectrumAxes;
                side = "left";
            case "spectrum-right"
                axesHandle = spectrumAxes;
                side = "right";
            case "cumulative"
                axesHandle = cumulativeAxes;
            otherwise
                error("IPCE:UnknownAxisTarget", "未知图形目标：%s", target);
        end
    end

    function activateAxisSide(axesHandle, side)
        if side ~= ""
            yyaxis(axesHandle, side);
        end
    end

    function onAxesClick(axesHandle, target)
        legacyPick = state.pickTarget == target;
        anchorPick = state.pickTarget == target + "Anchor";
        newAnchorPick = state.pickTarget == target + "NewAnchor";
        darkStartPick = state.pickTarget == target + "DarkStart";
        darkEndPick = state.pickTarget == target + "DarkEnd";
        if ~legacyPick && ~anchorPick && ~newAnchorPick && ...
                ~darkStartPick && ~darkEndPick
            return
        end
        point = axesHandle.CurrentPoint;
        selectedTime = point(1, 1);
        limits = xlim(axesHandle);
        selectedTime = min(max(selectedTime, limits(1)), limits(2));
        if target == "silicon"
            selectedTrace = state.siliconTrace;
        else
            selectedTrace = state.sampleTrace;
        end
        [~, nearestIndex] = min(abs(selectedTrace.Time_s - selectedTime));
        selectedTime = selectedTrace.Time_s(nearestIndex);
        selectedCurrent = selectedTrace.Current_A(nearestIndex);

        if darkStartPick
            if target == "silicon"
                siliconDarkStartField.Value = selectedTime;
            else
                sampleDarkStartField.Value = selectedTime;
            end
            state.pickTarget = target + "DarkEnd";
            setLocalizedStatus( ...
                "已选择%s暗区间起点 %.4f s；请再单击终点。", ...
                targetDisplayName(target), selectedTime);
            return
        elseif darkEndPick
            range = darkRangeForTarget(target);
            range(2) = selectedTime;
            range = sort(range);
            if range(2) <= range(1)
                setLocalizedStatus("暗区间起点和终点不能相同，请重新选择。");
                state.pickTarget = target + "DarkEnd";
                return
            end
            if target == "silicon"
                siliconDarkStartField.Value = range(1);
                siliconDarkEndField.Value = range(2);
            else
                sampleDarkStartField.Value = range(1);
                sampleDarkEndField.Value = range(2);
            end
            state.pickTarget = "";
            appFigure.Pointer = "arrow";
            refreshTracePlot(target);
            setLocalizedStatus( ...
                "已从图上选择%s暗电流区间：%.4f–%.4f s。", ...
                targetDisplayName(target), range(1), range(2));
            return
        elseif newAnchorPick
            state.pickTarget = "";
            appFigure.Pointer = "arrow";
            openNewAnchorDialog(target, selectedTime, selectedCurrent);
            return
        elseif anchorPick
            if target == "silicon"
                data = siliconAnchorTable.Data;
                row = state.siliconAnchorRow;
                data(row, 2) = selectedTime;
                siliconAnchorTable.Data = data;
                wavelengthValue = data(row, 1);
            else
                data = sampleAnchorTable.Data;
                row = state.sampleAnchorRow;
                data(row, 2) = selectedTime;
                sampleAnchorTable.Data = data;
                wavelengthValue = data(row, 1);
            end
            setLocalizedStatus( ...
                "已确认%s锚点：%.6g nm → %.4f s。", ...
                targetDisplayName(target), wavelengthValue, selectedTime);
            plotAlignmentPreview();
        elseif target == "silicon"
            siliconStartField.Value = selectedTime;
            plotTrace(siliconAxes, state.siliconTrace, ...
                state.siliconExtracted, selectedTime, "硅标探 i-t", ...
                darkRangeForTarget("silicon"), darkCheckBox.Value, ...
                currentLanguage);
        else
            sampleStartField.Value = selectedTime;
            plotTrace(sampleAxes, state.sampleTrace, ...
                state.sampleExtracted, selectedTime, "样品 i-t", ...
                darkRangeForTarget("sample"), darkCheckBox.Value, ...
                currentLanguage);
        end
        state.pickTarget = "";
        appFigure.Pointer = "arrow";
        if legacyPick
            setLocalizedStatus("已从图上选择固定模式起始时间：%.4f s。", selectedTime);
        end
    end

    function dialog = openNewAnchorDialog(target, selectedTime, selectedCurrent)
        dialog = uifigure( ...
            "Name", localized("确认新锚点"), ...
            "Position", [420, 300, 390, 230], ...
            "WindowStyle", "modal", ...
            "Resize", "off");
        gridLayout = uigridlayout(dialog, [5, 2]);
        gridLayout.RowHeight = {30, 34, 34, 34, 38};
        gridLayout.ColumnWidth = {135, "1x"};
        titleText = uilabel(gridLayout, ...
            "Text", localized("已吸附到最近的原始采样点"), ...
            "FontWeight", "bold");
        setLayout(titleText, 1, [1, 2]);
        currentLabel = uilabel(gridLayout, "Text", localized("该点电流"));
        setLayout(currentLabel, 2, 1);
        currentValue = uilabel(gridLayout, ...
            "Text", sprintf("%.8g A", selectedCurrent));
        setLayout(currentValue, 2, 2);
        wavelengthLabel = uilabel(gridLayout, ...
            "Text", localized("确认波长 (nm)"));
        setLayout(wavelengthLabel, 3, 1);
        wavelengthField = uieditfield(gridLayout, "numeric", ...
            "Value", 500, "Limits", [eps, Inf]);
        setLayout(wavelengthField, 3, 2);
        timeLabel = uilabel(gridLayout, "Text", localized("确认时间 (s)"));
        setLayout(timeLabel, 4, 1);
        timeField = uieditfield(gridLayout, "numeric", ...
            "Value", selectedTime);
        setLayout(timeField, 4, 2);
        cancelButton = uibutton(gridLayout, "Text", localized("取消"), ...
            "ButtonPushedFcn", @(~, ~)delete(dialog));
        setLayout(cancelButton, 5, 1);
        confirmButton = uibutton(gridLayout, "Text", localized("确认并加入"), ...
            "ButtonPushedFcn", @(~, ~)confirmNewAnchor( ...
            dialog, target, wavelengthField.Value, timeField.Value));
        setLayout(confirmButton, 5, 2);
    end

    function confirmNewAnchor(dialog, target, wavelengthValue, timeValue)
        if target == "silicon"
            data = siliconAnchorTable.Data;
        else
            data = sampleAnchorTable.Data;
        end
        if isempty(data)
            data = zeros(0, 2);
        end
        existingRow = find(isfinite(data(:, 1)) & ...
            abs(data(:, 1) - wavelengthValue) <= ...
            10 * eps(max(abs(wavelengthValue), 1)), 1);
        if isempty(existingRow)
            data(end + 1, :) = [wavelengthValue, timeValue];
            selectedRow = size(data, 1);
        else
            data(existingRow, :) = [wavelengthValue, timeValue];
            selectedRow = existingRow;
        end
        if target == "silicon"
            siliconAnchorTable.Data = data;
            state.siliconAnchorRow = selectedRow;
        else
            sampleAnchorTable.Data = data;
            state.sampleAnchorRow = selectedRow;
        end
        if isvalid(dialog)
            delete(dialog);
        end
        plotAlignmentPreview();
        setLocalizedStatus( ...
            "已加入%s锚点：%.6g nm → %.4f s。", ...
            targetDisplayName(target), wavelengthValue, timeValue);
        tabGroup.SelectedTab = alignmentTab;
    end

    function [siliconWavelengths, sampleWavelengths, settings] = currentSettings()
        commitScanProfile(string(scanTargetDropDown.Value));
        siliconProfile = state.siliconScan;
        sampleProfile = state.sampleScan;
        siliconWavelengths = makeWavelengths(siliconProfile);
        sampleWavelengths = makeWavelengths(sampleProfile);

        if string(alignmentModeDropDown.Value) == "fixed" && ...
                (siliconProfile.Average_s > siliconProfile.Delay_s || ...
                sampleProfile.Average_s > sampleProfile.Delay_s)
            error("IPCE:InvalidAverageWindow", ...
                "固定 Delay 模式下，取样时长不能大于对应的 Delay。");
        end

        siliconAnchors = readAnchorData(siliconAnchorTable, "标探");
        sampleAnchors = readAnchorData(sampleAnchorTable, "样品");

        settings = struct( ...
            "SiliconWavelengthStart_nm", siliconProfile.Start_nm, ...
            "SiliconWavelengthEnd_nm", siliconProfile.End_nm, ...
            "SiliconWavelengthStep_nm", siliconProfile.Step_nm, ...
            "SiliconWavelengthPointCount", numel(siliconWavelengths), ...
            "SampleWavelengthStart_nm", sampleProfile.Start_nm, ...
            "SampleWavelengthEnd_nm", sampleProfile.End_nm, ...
            "SampleWavelengthStep_nm", sampleProfile.Step_nm, ...
            "SampleWavelengthPointCount", numel(sampleWavelengths), ...
            "AlignmentMode", string(alignmentModeDropDown.Value), ...
            "SiliconAnchors", siliconAnchors, ...
            "SampleAnchors", sampleAnchors, ...
            "SiliconStartTime_s", siliconStartField.Value, ...
            "SampleStartTime_s", sampleStartField.Value, ...
            "SiliconDelay_s", siliconProfile.Delay_s, ...
            "SampleDelay_s", sampleProfile.Delay_s, ...
            "SiliconAverageTime_s", siliconProfile.Average_s, ...
            "SampleAverageTime_s", sampleProfile.Average_s, ...
            "SubtractDark", darkCheckBox.Value, ...
            "SiliconDarkRange_s", darkRangeForTarget("silicon"), ...
            "SampleDarkRange_s", darkRangeForTarget("sample"), ...
            "SiliconIlluminatedArea_cm2", siliconAreaField.Value, ...
            "SampleIlluminatedArea_cm2", sampleAreaField.Value);
    end

    function wavelengths = makeWavelengths(profile)
        startWavelength = profile.Start_nm;
        endWavelength = profile.End_nm;
        step = profile.Step_nm;
        direction = sign(endWavelength - startWavelength);
        if direction == 0
            wavelengths = startWavelength;
        else
            pointCount = floor(abs(endWavelength - startWavelength) / step + 1e-12) + 1;
            wavelengths = startWavelength + direction * step * (0:pointCount - 1);
            if abs(wavelengths(end) - endWavelength) > ...
                    100 * eps(max(abs([startWavelength, endWavelength])))
                wavelengths(end + 1) = endWavelength;
            end
        end
        wavelengths = wavelengths(:);
    end

    function requireInputs(requireSample)
        if isempty(state.calibration)
            error("IPCE:MissingCalibration", "请先导入硅标探响应度校准表。");
        end
        if isempty(state.siliconTrace)
            error("IPCE:MissingSilicon", "请先导入硅标探 i-t 文件。");
        end
        if requireSample && isempty(state.sampleTrace)
            error("IPCE:MissingSample", "请先导入样品 i-t 文件。");
        end
    end

    function plotPower()
        plot(powerAxes, state.lightResult.Wavelength_nm, ...
            state.lightResult.IncidentPowerDensity_W_cm2 * 1e6, "-o", ...
            "LineWidth", 1.3, "MarkerSize", 3.5);
        xlabel(powerAxes, localized("波长 (nm)"));
        ylabel(powerAxes, localized("入射功率密度 (\muW cm^{-2})"));
        title(powerAxes, localized("由硅标探反算的单色光功率密度"));
        grid(powerAxes, "on");
    end

    function plotSpectrumPreview()
        cla(spectrumAxes);
        cla(cumulativeAxes);
        state.spectrumIPCESource = string(ipceSourceDropDown.Value);
        try
            [previewIPCE, sourceLabel] = ipceResolveIPCESource( ...
                state.ipceResult, state.externalIPCE, ...
                state.spectrumIPCESource);
        catch
            previewIPCE = table();
            if state.spectrumIPCESource == "external"
                sourceLabel = "外部导入 IPCE";
            else
                sourceLabel = "本软件计算结果";
            end
        end
        sourceLabel = localized(sourceLabel);
        if isempty(state.spectrum)
            title(spectrumAxes, localized("请导入标准光谱"));
            title(cumulativeAxes, localized("计算积分后显示累计电流密度"));
            xlabel(spectrumAxes, localized("波长 (nm)"));
            ylabel(spectrumAxes, localized("辐照度 (W m^{-2} nm^{-1})"));
            xlabel(cumulativeAxes, localized("波长 (nm)"));
            ylabel(cumulativeAxes, ...
                localized("累计积分电流密度 (mA cm^{-2})"));
            return
        end
        spectrumColor = [0.90, 0.45, 0.05];
        ipceColor = [0.05, 0.40, 0.75];
        yyaxis(spectrumAxes, "left");
        plot(spectrumAxes, state.spectrum.Wavelength_nm, ...
            state.spectrum.Irradiance_W_m2_nm, ...
            "Color", spectrumColor, "LineWidth", 1.1, ...
            "DisplayName", localized("光谱辐照度"));
        ylabel(spectrumAxes, localized("辐照度 (W m^{-2} nm^{-1})"));
        spectrumAxes.YAxis(1).Color = spectrumColor;
        hold(spectrumAxes, "on");
        if ~isempty(state.spectrumCurve)
            selected = state.spectrum.Wavelength_nm >= ...
                state.spectrumSummary.MinimumWavelength_nm(1) & ...
                state.spectrum.Wavelength_nm <= ...
                state.spectrumSummary.MaximumWavelength_nm(1);
            area(spectrumAxes, state.spectrum.Wavelength_nm(selected), ...
                state.spectrum.Irradiance_W_m2_nm(selected), ...
                "FaceColor", [1.00, 0.82, 0.35], "FaceAlpha", 0.25, ...
                "EdgeColor", "none", "DisplayName", localized("积分范围"));
        end
        hold(spectrumAxes, "off");

        if ~isempty(previewIPCE)
            yyaxis(spectrumAxes, "right");
            if numel(spectrumAxes.YAxis) >= 2
                spectrumAxes.YAxis(2).Visible = "on";
            end
            plot(spectrumAxes, previewIPCE.Wavelength_nm, ...
                previewIPCE.IPCE_percent, "-o", ...
                "Color", ipceColor, "LineWidth", 1.2, ...
                "MarkerSize", 3, "DisplayName", sourceLabel);
            ylabel(spectrumAxes, localized("IPCE (%)"));
            spectrumAxes.YAxis(2).Color = ipceColor;
        elseif numel(spectrumAxes.YAxis) >= 2
            spectrumAxes.YAxis(2).Visible = "off";
        end
        xlabel(spectrumAxes, localized("波长 (nm)"));
        title(spectrumAxes, localized("光谱与 %s（积分前进行波长插值）", ...
            sourceLabel));
        grid(spectrumAxes, "on");

        if ~isempty(state.spectrumCurve)
            cumulativeColor = [0.38, 0.52, 0.16];
            plot(cumulativeAxes, state.spectrumCurve.Wavelength_nm, ...
                state.spectrumCurve.CumulativeCurrentDensity_mA_cm2, ...
                "-", "Color", cumulativeColor, "LineWidth", 1.6, ...
                "DisplayName", localized("累计积分 J"));
            hold(cumulativeAxes, "on");
            plot(cumulativeAxes, state.spectrumCurve.Wavelength_nm(end), ...
                state.spectrumCurve.CumulativeCurrentDensity_mA_cm2(end), ...
                "o", "Color", cumulativeColor, ...
                "MarkerFaceColor", cumulativeColor, ...
                "DisplayName", localized("最终积分 J"));
            hold(cumulativeAxes, "off");
            ylabel(cumulativeAxes, localized("累计积分电流密度 (mA cm^{-2})"));
            title(cumulativeAxes, localized("累计积分电流密度随波长变化"));
            legend(cumulativeAxes, "Location", "best");
        else
            title(cumulativeAxes, localized("计算积分后显示累计电流密度"));
        end
        xlabel(cumulativeAxes, localized("波长 (nm)"));
        ylabel(cumulativeAxes, ...
            localized("累计积分电流密度 (mA cm^{-2})"));
        grid(cumulativeAxes, "on");
    end

    function anchors = readAnchorData(tableHandle, dataName)
        data = tableHandle.Data;
        if isempty(data)
            anchors = zeros(0, 2);
            return
        end
        if iscell(data)
            converted = nan(size(data));
            for rowIndex = 1:size(data, 1)
                for columnIndex = 1:size(data, 2)
                    value = data{rowIndex, columnIndex};
                    if isnumeric(value)
                        converted(rowIndex, columnIndex) = double(value);
                    else
                        converted(rowIndex, columnIndex) = str2double(string(value));
                    end
                end
            end
            data = converted;
        else
            data = double(data);
        end

        incomplete = xor(isfinite(data(:, 1)), isfinite(data(:, 2)));
        if any(incomplete)
            badRow = find(incomplete, 1);
            error("IPCE:IncompleteAnchor", ...
                "%s锚点表第 %d 行不完整；波长和时间需要同时填写。", ...
                dataName, badRow);
        end
        anchors = data(all(isfinite(data), 2), :);
        if any(anchors(:, 1) <= 0)
            error("IPCE:InvalidAnchor", "%s锚点波长必须大于 0。", dataName);
        end
    end

    function plotAlignmentPreview()
        cla(alignmentAxes);
        hold(alignmentAxes, "on");
        plotted = false;
        try
            [siliconWavelengths, sampleWavelengths, settings] = currentSettings();
            if settings.AlignmentMode == "fixed"
                siliconPreview = ipceBuildSchedule(siliconWavelengths, "fixed", ...
                    zeros(0, 2), settings.SiliconStartTime_s, ...
                    settings.SiliconDelay_s);
                samplePreview = ipceBuildSchedule(sampleWavelengths, "fixed", ...
                    zeros(0, 2), settings.SampleStartTime_s, ...
                    settings.SampleDelay_s);
            else
                siliconPreview = table();
                samplePreview = table();
                if ~isempty(settings.SiliconAnchors)
                    siliconPreview = ipceBuildSchedule(siliconWavelengths, "anchors", ...
                        settings.SiliconAnchors, settings.SiliconStartTime_s, ...
                        settings.SiliconDelay_s);
                end
                if ~isempty(settings.SampleAnchors)
                    samplePreview = ipceBuildSchedule(sampleWavelengths, "anchors", ...
                        settings.SampleAnchors, settings.SampleStartTime_s, ...
                        settings.SampleDelay_s);
                end
            end

            if ~isempty(siliconPreview)
                plot(alignmentAxes, siliconPreview.Wavelength_nm, ...
                    siliconPreview.ReferenceTime_s, "-", ...
                    "LineWidth", 1.5, "DisplayName", localized("标探调度"));
                if ~isempty(settings.SiliconAnchors)
                    scatter(alignmentAxes, settings.SiliconAnchors(:, 1), ...
                        settings.SiliconAnchors(:, 2), 45, "filled", ...
                        "DisplayName", localized("标探锚点"));
                end
                plotted = true;
            end
            if ~isempty(samplePreview)
                plot(alignmentAxes, samplePreview.Wavelength_nm, ...
                    samplePreview.ReferenceTime_s, "-", ...
                    "LineWidth", 1.5, "DisplayName", localized("样品调度"));
                if ~isempty(settings.SampleAnchors)
                    scatter(alignmentAxes, settings.SampleAnchors(:, 1), ...
                        settings.SampleAnchors(:, 2), 45, "filled", ...
                        "DisplayName", localized("样品锚点"));
                end
                plotted = true;
            end
            title(alignmentAxes, localized("由锚点生成的波长–时间调度"));
        catch exception
            title(alignmentAxes, localized("调度预览：请补全或检查锚点"));
            text(alignmentAxes, 0.5, 0.5, localizedException(exception), ...
                "Units", "normalized", "HorizontalAlignment", "center", ...
                "VerticalAlignment", "middle", "Color", [0.75, 0.15, 0.10]);
        end
        hold(alignmentAxes, "off");
        xlabel(alignmentAxes, localized("波长 (nm)"));
        ylabel(alignmentAxes, localized("确认时间 (s)"));
        grid(alignmentAxes, "on");
        if plotted
            legend(alignmentAxes, "Location", "best");
        end
    end

    function name = targetDisplayName(target)
        if target == "silicon"
            name = "标探";
        else
            name = "样品";
        end
    end

    function startTime = traceStartForDisplay(target)
        if target == "silicon"
            startTime = siliconStartField.Value;
        else
            startTime = sampleStartField.Value;
        end
        try
            [siliconWavelengths, sampleWavelengths, settings] = currentSettings();
            if settings.AlignmentMode == "anchors"
                if target == "silicon"
                    anchors = settings.SiliconAnchors;
                    wavelengths = siliconWavelengths;
                    delay = settings.SiliconDelay_s;
                else
                    anchors = settings.SampleAnchors;
                    wavelengths = sampleWavelengths;
                    delay = settings.SampleDelay_s;
                end
                if ~isempty(anchors)
                    preview = ipceBuildSchedule(wavelengths, "anchors", ...
                        anchors, startTime, delay);
                    startTime = preview.ReferenceTime_s(1);
                end
            end
        catch
        end
    end

    function autoLoadWorkspaceFiles()
        calibrationPath = ipceResolveStartupFile( ...
            defaults.CalibrationFile, "*校准*.xlsx");
        spectrumPath = ipceResolveStartupFile( ...
            defaults.SpectrumFile, "*太阳能光谱*.xls");
        siliconPath = ipceResolveStartupFile( ...
            defaults.SiliconTraceFile, defaults.SiliconTraceFile);
        anchorPath = ipceResolveStartupFile( ...
            defaults.SiliconAnchorFile, defaults.SiliconAnchorFile);
        messages = strings(0, 1);
        if calibrationPath ~= ""
            loadCalibration(calibrationPath, false);
            if ~isempty(state.calibration)
                messages(end + 1) = "标探响应度";
            end
        end

        if siliconPath ~= ""
            loadSilicon(siliconPath, false);
            if ~isempty(state.siliconTrace)
                messages(end + 1) = "指定标探 i-t";
            end
        else
            messages(end + 1) = "未找到指定标探 i-t";
        end

        if anchorPath ~= ""
            try
                siliconAnchorTable.Data = ...
                    ipceReadAnchors(anchorPath);
                state.siliconAnchorRow = 1;
                messages(end + 1) = sprintf("标探锚点 %d 行", ...
                    size(siliconAnchorTable.Data, 1));
                plotAlignmentPreview();
            catch exception
                messages(end + 1) = "标探锚点载入失败：" + ...
                    string(exception.message);
            end
        else
            messages(end + 1) = "未找到指定标探锚点";
        end

        if spectrumPath ~= ""
            loadSpectrumFile(spectrumPath, false);
            if ~isempty(state.spectrum)
                messages(end + 1) = "标准太阳能光谱";
            end
        end
        if ~isempty(messages)
            setLocalizedStatus("启动检查：" + strjoin(messages, "；") + ...
                "。请核对数据批次与参数。");
        end
    end

    function metadata = settingsTable(settings)
        parameter = [ ...
            "SiliconWavelengthStart_nm"; "SiliconWavelengthEnd_nm"; ...
            "SiliconWavelengthStep_nm"; "SiliconWavelengthPointCount"; ...
            "SampleWavelengthStart_nm"; "SampleWavelengthEnd_nm"; ...
            "SampleWavelengthStep_nm"; "SampleWavelengthPointCount"; ...
            "AlignmentMode"; ...
            "SiliconAnchorCount"; "SampleAnchorCount"; ...
            "SiliconStartTime_s"; "SampleStartTime_s"; ...
            "SiliconDelay_s"; "SampleDelay_s"; ...
            "SiliconAverageTime_s"; "SampleAverageTime_s"; ...
            "SubtractDark"; ...
            "SiliconDarkStart_s"; "SiliconDarkEnd_s"; ...
            "SampleDarkStart_s"; "SampleDarkEnd_s"; ...
            "SiliconIlluminatedArea_cm2"; "SampleIlluminatedArea_cm2"; ...
            "CalibrationFile"; "SiliconFile"; "SampleFile"; ...
            "SiliconOriginalTimeUnit"; "SiliconOriginalCurrentUnit"; ...
            "SiliconTimeToSecondsFactor"; ...
            "SiliconCurrentToAmperesFactor"; ...
            "SampleOriginalTimeUnit"; "SampleOriginalCurrentUnit"; ...
            "SampleTimeToSecondsFactor"; ...
            "SampleCurrentToAmperesFactor"; ...
            "ExternalIPCEFile"; "IPCESource"; "SpectrumFile"; ...
            "SpectrumSheet"; "SpectrumWavelengthColumn"; ...
            "SpectrumWavelengthHeader"; "SpectrumIntegrationColumn"; ...
            "SpectrumIntegrationHeader"; "IntegrationStart_nm"; ...
            "IntegrationEnd_nm"; ...
            "GeneratedAt"];
        value = [ ...
            string(settings.SiliconWavelengthStart_nm); ...
            string(settings.SiliconWavelengthEnd_nm); ...
            string(settings.SiliconWavelengthStep_nm); ...
            string(settings.SiliconWavelengthPointCount); ...
            string(settings.SampleWavelengthStart_nm); ...
            string(settings.SampleWavelengthEnd_nm); ...
            string(settings.SampleWavelengthStep_nm); ...
            string(settings.SampleWavelengthPointCount); ...
            string(settings.AlignmentMode); ...
            string(size(settings.SiliconAnchors, 1)); ...
            string(size(settings.SampleAnchors, 1)); ...
            string(settings.SiliconStartTime_s); ...
            string(settings.SampleStartTime_s); ...
            string(settings.SiliconDelay_s); ...
            string(settings.SampleDelay_s); ...
            string(settings.SiliconAverageTime_s); ...
            string(settings.SampleAverageTime_s); ...
            string(settings.SubtractDark); ...
            string(settings.SiliconDarkRange_s(1)); ...
            string(settings.SiliconDarkRange_s(2)); ...
            string(settings.SampleDarkRange_s(1)); ...
            string(settings.SampleDarkRange_s(2)); ...
            string(settings.SiliconIlluminatedArea_cm2); ...
            string(settings.SampleIlluminatedArea_cm2); ...
            state.calibrationFile; state.siliconFile; state.sampleFile; ...
            tableMetadataValue(state.siliconTrace, "OriginalTimeUnit"); ...
            tableMetadataValue(state.siliconTrace, "OriginalCurrentUnit"); ...
            tableMetadataValue(state.siliconTrace, ...
            "TimeToSecondsFactor"); ...
            tableMetadataValue(state.siliconTrace, ...
            "CurrentToAmperesFactor"); ...
            tableMetadataValue(state.sampleTrace, "OriginalTimeUnit"); ...
            tableMetadataValue(state.sampleTrace, "OriginalCurrentUnit"); ...
            tableMetadataValue(state.sampleTrace, "TimeToSecondsFactor"); ...
            tableMetadataValue(state.sampleTrace, ...
            "CurrentToAmperesFactor"); ...
            state.externalIPCEFile; string(ipceSourceDropDown.Value); ...
            state.spectrumFile; string(spectrumSheetField.Value); ...
            string(spectrumWavelengthColumnDropDown.Value); ...
            string(selectedSpectrumHeader( ...
            spectrumWavelengthColumnDropDown.Value)); ...
            string(spectrumIrradianceColumnDropDown.Value); ...
            string(selectedSpectrumHeader( ...
            spectrumIrradianceColumnDropDown.Value)); ...
            string(integrationStartField.Value); ...
            string(integrationEndField.Value); ...
            string(datetime("now", "Format", "yyyy-MM-dd HH:mm:ss"))];
        metadata = table(parameter, value, ...
            'VariableNames', {'Parameter', 'Value'});
    end

    function metadata = postprocessSettingsTable()
        parameter = [ ...
            "ExternalIPCEFile"; "ExternalWavelengthHeader"; ...
            "ExternalIPCEHeader"; "IPCESource"; "SpectrumFile"; ...
            "SpectrumSheet"; "SpectrumWavelengthColumn"; ...
            "SpectrumWavelengthHeader"; "SpectrumIntegrationColumn"; ...
            "SpectrumIntegrationHeader"; "IntegrationStart_nm"; ...
            "IntegrationEnd_nm"; "GeneratedAt"];
        value = [ ...
            state.externalIPCEFile; ...
            tableMetadataValue(state.externalIPCE, ...
            "WavelengthHeader"); ...
            tableMetadataValue(state.externalIPCE, "IPCEHeader"); ...
            string(ipceSourceDropDown.Value); ...
            state.spectrumFile; string(spectrumSheetField.Value); ...
            string(spectrumWavelengthColumnDropDown.Value); ...
            string(selectedSpectrumHeader( ...
            spectrumWavelengthColumnDropDown.Value)); ...
            string(spectrumIrradianceColumnDropDown.Value); ...
            string(selectedSpectrumHeader( ...
            spectrumIrradianceColumnDropDown.Value)); ...
            string(integrationStartField.Value); ...
            string(integrationEndField.Value); ...
            string(datetime("now", ...
            "Format", "yyyy-MM-dd HH:mm:ss"))];
        metadata = table(parameter, value, ...
            'VariableNames', {'Parameter', 'Value'});
    end

    function output = anchorOutputTable(anchors)
        output = array2table(anchors, ...
            'VariableNames', {'Wavelength_nm', 'ConfirmedTime_s'});
    end

    function showError(exception)
        setLocalizedErrorStatus(exception);
        uialert(appFigure, localizedException(exception), ...
            localized("无法完成计算"));
    end
end

function label = fileLabel(parent, initialText)
label = uilabel(parent, "Text", initialText, ...
    "FontColor", [0.38, 0.38, 0.38], ...
    "FontSize", 11);
end

function updateFileLabel(label, filePath)
[~, name, extension] = fileparts(filePath);
label.Text = string(name) + string(extension);
label.Tooltip = filePath;
end

function configureTraceAxes(axesHandle, axesTitle, clickCallback)
xlabel(axesHandle, "时间 (s)");
ylabel(axesHandle, "电流 (A)");
title(axesHandle, axesTitle);
grid(axesHandle, "on");
axesHandle.ButtonDownFcn = clickCallback;
end

function addAnalysisToolbar(axesHandle)
try
    axtoolbar(axesHandle, {"zoomin", "zoomout", "pan", "restoreview"});
catch
end
end

function plotTrace(axesHandle, trace, extracted, startTime, axesTitle, ...
        darkRange, showDark, language)
if nargin < 6
    darkRange = [];
end
if nargin < 7
    showDark = false;
end
if nargin < 8
    language = "zh-CN";
end
axesTitle = ipceLocalizeLiteral(language, axesTitle);
cla(axesHandle);
if isempty(trace)
    title(axesHandle, axesTitle);
    return
end

rawLine = plot(axesHandle, trace.Time_s, trace.Current_A, ...
    "Color", [0.10, 0.35, 0.70], "LineWidth", 0.8, ...
    "DisplayName", ipceLocalizeLiteral(language, "原始 i-t"), ...
    "HitTest", "off");
try
    rawLine.PickableParts = "none";
catch
end
hold(axesHandle, "on");

if showDark && numel(darkRange) == 2 && ...
        all(isfinite(darkRange)) && darkRange(2) > darkRange(1)
    yLimits = ylim(axesHandle);
    darkPatch = patch(axesHandle, ...
        [darkRange(1), darkRange(2), darkRange(2), darkRange(1)], ...
        [yLimits(1), yLimits(1), yLimits(2), yLimits(2)], ...
        [0.35, 0.35, 0.35], ...
        "FaceAlpha", 0.13, "EdgeColor", [0.25, 0.25, 0.25], ...
        "LineStyle", "--", "DisplayName", ...
        ipceLocalizeLiteral(language, "暗电流区间"), ...
        "HitTest", "off");
    try
        darkPatch.PickableParts = "none";
    catch
    end
end

if ~isempty(extracted)
    marker = plot(axesHandle, extracted.MeanTime_s, ...
        extracted.MeanCurrent_A, "o", ...
        "Color", [0.85, 0.20, 0.12], ...
        "MarkerFaceColor", [1.00, 0.78, 0.30], ...
        "MarkerSize", 4, "DisplayName", ...
        ipceLocalizeLiteral(language, "窗口均值"), ...
        "HitTest", "off");
    try
        marker.PickableParts = "none";
    catch
    end
end

startLine = xline(axesHandle, startTime, "--", ...
    ipceLocalizeLiteral(language, "首个对齐参考"), ...
    "Color", [0.15, 0.55, 0.20], "LineWidth", 1.2, ...
    "LabelVerticalAlignment", "bottom", "HitTest", "off", ...
    "HandleVisibility", "off");
try
    startLine.PickableParts = "none";
catch
end
hold(axesHandle, "off");
xlabel(axesHandle, ipceLocalizeLiteral(language, "时间 (s)"));
ylabel(axesHandle, ipceLocalizeLiteral(language, "电流 (A)"));
title(axesHandle, axesTitle);
grid(axesHandle, "on");
legend(axesHandle, "Location", "best");
end

function textValue = traceSummary(name, trace)
metadata = trace.Properties.UserData;
if isstruct(metadata) && isfield(metadata, "OriginalTimeUnit") && ...
        isfield(metadata, "OriginalCurrentUnit")
    unitText = sprintf("；原单位 %s/%s，已换算为 s/A", ...
        metadata.OriginalTimeUnit, metadata.OriginalCurrentUnit);
else
    unitText = "";
end
textValue = sprintf( ...
    "%s i-t 已读取：%d 点，%.4f–%.4f s，中位采样间隔 %.4g s%s。", ...
    name, height(trace), trace.Time_s(1), trace.Time_s(end), ...
    metadata.SampleInterval_s, unitText);
end

function value = tableMetadataValue(data, fieldName)
value = "";
if isempty(data)
    return
end
metadata = data.Properties.UserData;
if isstruct(metadata) && isfield(metadata, fieldName)
    value = string(metadata.(fieldName));
end
end

function setLayout(component, row, column)
component.Layout.Row = row;
component.Layout.Column = column;
end

function registry = captureLocalizationRegistry(figureHandle, excludedComponents)
registry = cell(0, 3);
propertyNames = ["Name", "Title", "Text", "Tooltip", "Items", "ColumnName"];
handles = findall(figureHandle);
for handleIndex = 1:numel(handles)
    component = handles(handleIndex);
    if any(cellfun(@(excluded)isequal(component, excluded), ...
            excludedComponents))
        continue
    end
    for propertyIndex = 1:numel(propertyNames)
        propertyName = propertyNames(propertyIndex);
        if ~isprop(component, propertyName)
            continue
        end
        try
            sourceValue = component.(propertyName);
            if ischar(sourceValue) || isstring(sourceValue) || iscell(sourceValue)
                registry(end + 1, :) = {component, char(propertyName), sourceValue}; %#ok<AGROW>
            end
        catch
        end
    end
end
end

function applyLocalizationRegistry(registry, language)
for index = 1:size(registry, 1)
    component = registry{index, 1};
    if ~isvalid(component)
        continue
    end
    try
        component.(registry{index, 2}) = ipceLocalizeLiteral( ...
            language, registry{index, 3});
    catch
    end
end
end

function texts = collectLocalizedTexts(registry)
texts = strings(0, 1);
for index = 1:size(registry, 1)
    component = registry{index, 1};
    if ~isvalid(component)
        continue
    end
    try
        value = component.(registry{index, 2});
        if ischar(value)
            texts = [texts; string(value)]; %#ok<AGROW>
        elseif isstring(value)
            texts = [texts; value(:)]; %#ok<AGROW>
        elseif iscell(value)
            texts = [texts; string(value(:))]; %#ok<AGROW>
        end
    catch
    end
end
end
