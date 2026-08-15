# IPCE Windows 绿色包设计

## 目标

把当前 MATLAB R2023b IPCE 程序编译为可在 64 位 Windows 电脑上运行的
绿色 ZIP 包。目标用户不会编程，已自行从 MathWorks 官网安装 MATLAB
Runtime R2023b Update 6 或同版本更高更新级别。

交付物不包含 MATLAB Runtime，不创建系统级安装器。用户解压 ZIP 后直接
双击 `IPCEApp.exe`。

## 已确认的交付形式

- 文件名：`IPCEApp_R2023b_Windows_x64.zip`。
- 平台：64 位 Windows。
- 编译环境：MATLAB R2023b Update 6、MATLAB Compiler 23.2。
- Runtime：不包含在 ZIP 中，由用户自行安装。
- 默认数据：嵌入编译后的 deployable archive，不作为容易被误删的散装文件。
- 其他批次数据：继续由用户通过现有文件选择界面导入。
- 项目不是 Git 仓库，本次不初始化 Git、不创建提交。

## 默认数据

以下四个文件作为打包附加文件嵌入程序：

1. `标准硅探测器校准结果_证书编号 GXgf2026-01645.xlsx`
2. `标准太阳能光谱数据.xls`
3. `Si-i t [300 1100] nm-grating 2-filter.txt`
4. `Si-i t [300 1100] nm-grating 2-filter-time match.txt`

开发环境运行时，程序继续优先读取当前工作目录中的匹配文件，保留现有的
本地调试和数据替换行为。部署环境中，如果当前目录没有匹配文件，则从
`ctfroot` 指向的只读部署归档读取上述默认文件。

默认数据不存在或读取失败时，应用仍须正常打开，并在状态区说明未自动载入
的项目。手动导入及独立外部 IPCE 后处理不得依赖这些默认文件。

## 代码结构

新增一个独立的启动文件解析函数，负责在普通 MATLAB 环境和部署环境中定位
默认文件。该函数只返回已存在的文件路径，不负责解析数据或更新 UI。

`IPCEApp.m` 的自动加载逻辑调用该函数，随后继续使用现有的
`loadCalibration`、`loadSilicon`、`ipceReadAnchors` 和
`loadSpectrumFile` 路径，不改变计算流程。

新增一个可重复运行的构建入口，负责：

1. 检查 MATLAB Compiler 产品与许可证。
2. 检查四个默认数据文件。
3. 运行 `run_ipce_selftest`。
4. 使用 `mcc -e` 生成不显示命令行窗口的 Windows 应用。
5. 把完整编译输出、中文运行说明和版本信息复制到单一发布目录。
6. 生成 `IPCEApp_R2023b_Windows_x64.zip`。

构建输出放在项目的 `dist` 目录。重新构建时只替换本构建入口管理的同名输出
目录和 ZIP，不删除项目中的其他文件。

## 用户运行流程

1. 从 MathWorks 官网安装 MATLAB Runtime R2023b。
2. 解压 `IPCEApp_R2023b_Windows_x64.zip` 到普通可写目录。
3. 双击 `IPCEApp.exe`。
4. 首次启动等待 Runtime 初始化。
5. 核对自动加载的数据批次，按现有说明完成测量、后处理和导出。

中文运行说明必须包含 Runtime 官方下载页面、所需版本、解压运行步骤，以及
“Runtime 未安装或版本不匹配”“Windows 阻止未知来源程序”“首次启动较慢”
三类常见问题。

## 错误处理

- MATLAB Compiler 不存在或许可证不可用：构建立即失败并给出明确错误。
- 任一默认数据文件缺失：构建立即失败并列出文件名，避免发布不完整 ZIP。
- 自检失败：不开始编译。
- `mcc` 返回失败：不生成或不保留声称可发布的 ZIP。
- ZIP 生成后验证文件存在且大小大于零。
- 部署时默认数据读取失败：应用保持可用，状态区显示失败原因。

## 测试与验收

所有生产代码变更遵循先失败、后实现的回归测试流程。

自动化测试至少覆盖：

1. 当前目录中存在匹配文件时优先返回该文件。
2. 当前目录无匹配文件时可从指定部署根目录返回精确默认文件。
3. 两处都不存在时返回空路径，不误选相似文件。
4. 默认配置包含四个明确文件名。
5. 现有完整 `run_ipce_selftest` 继续通过。

构建验收依次执行：

```matlab
run_ipce_selftest
app = IPCEApp;
drawnow;
assert(isvalid(app));
close(app);
build_ipce_portable
```

完成编译后验证：

- 发布目录包含 `IPCEApp.exe` 及编译器生成的全部必要文件。
- ZIP 存在、非空，并可正常解压。
- ZIP 中不包含 MATLAB Runtime 安装程序。
- 在开发电脑上启动发布目录中的 `IPCEApp.exe`，确认窗口创建并能关闭。
- 最终交付前仍建议在一台未安装 MATLAB、仅安装 R2023b Runtime 的干净
  Windows 10/11 电脑或虚拟机上执行完整用户流程验收。

## 不在本次范围

- 把 MATLAB Runtime 放入 ZIP。
- 制作 MSI、Inno Setup 或其他系统级安装器。
- 重写为 Python、C# 或 C++。
- 修改 IPCE 计算、光谱积分、插值、单位或导出格式。
- 自动更新、联网下载 Runtime 或程序内升级。
