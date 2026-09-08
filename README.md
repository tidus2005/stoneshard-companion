# Stoneshard Companion · 晶石助手

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**0.3.4 · Windows x64 · 非官方游戏增强工具**

An open-source Windows companion for Stoneshard, featuring a movable overlay,
engine speed controls, persistent item labels, compass navigation, and save backups.

Windows 单面板游戏增强工具，适配 Steam 原版 **Stoneshard 0.9.4.25 / Build 24780451**。

从源码下载后，请先按下方“开发”步骤构建。构建完成后双击根目录 `启动晶石助手.cmd`，或运行 `artifacts/release/StoneshardCompanion-0.3.4/StoneshardCompanion.exe`。新版使用独立目录，保留旧版成品。EXE、桥接 DLL 和 Assets 文件夹须一起保留；升级后需重启游戏以载入新桥接组件。

- 单面板：拖动顶部移动，拖动四角调整宽高，按钮自适应排列，记住位置和尺寸。
- 物品常显：移动和装备栏切换时持续显示地面物品名称，切图及重连后恢复开启状态。
- 九宫格导航：左侧固定八方向和中心九键；上下左右保留到边缘后跨入相邻地图，四个斜向走到地图角点后停下，中心键走到地图中央。遇敌、受伤或手动接管时停止，不自动攻击。
- 方向键自动移动：面板顶部开关默认关闭，开启后轻按普通方向键，沿人物当前行或列寻路到边缘后停下；再次按键停步，长按不重复，仅在游戏前台生效。
- 面板常驻：只在打开状态栏或物品栏时临时隐藏；主菜单、保存退出及游戏关闭后仍可一键备份。
- 补给：一键喝水、切换火把，可选按口渴阈值自动喝水、保持火把点亮并使用备用火把。
- 存档：一键完整备份、刷新最新副本、历史列表、选择还原和进度提示；兼容现有存档助手备份。
- 保留 1～4 倍引擎速度及切图记忆、面甲、镜头居中、四项角色状态。

0.3.4 仅运行离线检查并构建发布，未启动游戏。九宫格角点、普通方向键模式，以及此前的自动跨图、中心行走和常驻面板尚未实机验证。旧版已验证项目和未覆盖项见历史报告；自动火把点亮及耗尽换备用仍未实机验证。

- [使用说明](docs/使用说明.md)
- [0.3.4 实现与离线验证](docs/0.3.4实现与离线验证.md)
- [0.3.3 实现与离线验证](docs/0.3.3实现与离线验证.md)
- [0.3.2 修复与实机验证](docs/0.3.2修复与实机验证.md)
- [0.3.1 实机稳定性测试](docs/0.3.1实机稳定性测试.md)
- [0.3 实现与离线验证](docs/0.3实现与离线验证.md)
- [0.2 历史实现与实测](docs/0.2实现与验证.md)
- [详细设计](docs/详细设计.md)

## 开发

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)、
[Zig 0.13.0](https://ziglang.org/download/0.13.0/) 和
[PowerShell 7](https://github.com/PowerShell/PowerShell/releases)。
Zig 解压为 `.tools/zig-windows-x86_64-0.13.0/`，或将 `zig.exe` 所在目录加入 PATH。
离线构建、测试不需要安装游戏。

```powershell
git clone https://github.com/tidus2005/stoneshard-companion.git
cd stoneshard-companion
pwsh -File scripts/test-offline.ps1
pwsh -File scripts/build.ps1 -Publish
```

离线检查不访问游戏内存、不发送输入、不打开窗口，存档检查只写独立临时目录。发布生成版本目录、完整文件 SHA256 清单及 ZIP。

`src/Overlay` 为 WPF 界面；`src/Core` 管理连接、策略、布局和存档服务；`native/Bridge` 为游戏主线程适配层；`native/Tests` 模拟原生调用；`src/Probe` 包含诊断和离线检查；`tests` 包含原存档核心回归。**Probe 除 SelfTest/OfflineTest 外的命令会接触游戏，不属于离线检查。**

桥接以完整 EXE 和 data.win 哈希锁定适配版本，版本不符时拒绝连接。
研究脚本使用说明见 [research/README.md](research/README.md)，原始诊断和游戏副本不纳入公开仓库。

## 开源许可与贡献

原创源码和文档采用 [MIT 许可证](LICENSE)。Game-icons.net 图标保持
[CC BY 3.0 署名](src/Overlay/Assets/Icons/ATTRIBUTION.md)，完整说明见
[第三方声明](THIRD_PARTY_NOTICES.md)。本项目与 Stoneshard 开发商、发行商无隶属关系。

公开仓库不包含游戏程序、素材、个人存档、测试安装副本或本机环境快照。
欢迎提交 Issue 和 Pull Request，参与方式见 [CONTRIBUTING.md](CONTRIBUTING.md)。
