IPCE Measurement and Analysis (Windows Portable Package)
=========================================================

English quick start
-------------------

1. Install the 64-bit MATLAB Runtime R2023b for Windows. Use Update 6 or a
   later R2023b update:
   https://www.mathworks.com/products/compiler/matlab-runtime.html
2. Extract every file from this ZIP into one ordinary folder.
3. Run IPCEApp.exe. The first launch can take about as long as starting
   MATLAB while the Runtime initializes.
4. Use the language selector at the top of the left workflow panel to switch
   between English and 中文. The choice is remembered. Switching language
   never clears imported data, parameters, calculation results, or exports.
5. Verify the automatically loaded detector calibration, solar spectrum,
   silicon-detector i-t trace, time-alignment anchors, and all parameters
   before measurement or post-processing.

This ZIP does not include MATLAB Runtime. MATLAB itself and a MATLAB license
are not required. Do not move IPCEApp.exe out of the extracted package; keep
all packaged files together. External-IPCE post-processing works without
detector calibration or detector/sample i-t data.

IPCE 测量与分析软件（Windows 绿色版）
========================================

一、运行环境

本软件由 MATLAB R2023b Update 6 编译。
软件压缩包不包含 MATLAB Runtime。

首次使用前，请从 MathWorks 官网下载安装 64 位 Windows 版 MATLAB Runtime R2023b：
https://www.mathworks.com/products/compiler/matlab-runtime.html

建议安装 R2023b 的最新更新；其更新级别必须不低于 Update 6。
MATLAB Runtime 免费，运行本软件不需要安装 MATLAB，也不需要 MATLAB 许可证。

二、运行步骤

1. 安装 MATLAB Runtime R2023b。
2. 把本 ZIP 的全部内容解压到同一个普通文件夹。
3. 双击 IPCEApp.exe。
4. 首次启动需要初始化 Runtime，耗时可能接近启动一次 MATLAB，请耐心等待。
5. 软件打开后，务必核对自动载入的标探校准、太阳光谱、硅标探 i-t、时间匹配文件及计算参数。
6. 其他批次的数据仍通过软件界面的“导入”按钮选择。
7. 左侧流程面板顶部可在 `English` 与 `中文` 之间随时切换；选择会被记住。
   切换语言不会清除导入数据、参数、计算结果、积分结果或改变导出数值。

请勿只把 IPCEApp.exe 单独拖出压缩包运行；必须保留压缩包内的全部文件。

三、常见问题

1. 提示找不到 MATLAB Runtime 或无法初始化
   请确认安装的是 64 位 MATLAB Runtime R2023b，而不是其他版本。
   更新级别应为 Update 6 或更高的 R2023b 更新。

2. Windows 阻止未知来源程序
   本程序未购买商业代码签名证书，Windows 可能显示 SmartScreen 提示。
   请先确认文件来源，再选择“更多信息”并决定是否运行。

3. 双击后暂时没有窗口
   首次启动会解压和初始化 MATLAB Runtime 组件，可能需要较长时间。
   请等待，不要连续多次双击。

4. 自动数据未载入
   软件仍可继续使用。请通过界面手动导入相应文件，并核对数据批次。

四、重要提醒

测量和后处理前必须核对数据批次、波长范围、时间锚点、暗电流区间和受光面积。
外部 IPCE 后处理可独立使用，不要求先导入标探或样品 i-t 数据。
