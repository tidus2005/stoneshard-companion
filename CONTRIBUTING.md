# 参与开发

欢迎通过 Issue 提交问题，通过 Pull Request 提交修改。

## 构建和检查

- Windows x64、.NET 8 SDK、PowerShell 7、Zig 0.13.0。
- 将 Zig 放入 PATH，或解压为 `.tools/zig-windows-x86_64-0.13.0/`。
- 在项目根目录执行 `pwsh -File scripts/test-offline.ps1`。
- 构建发行包：`pwsh -File scripts/build.ps1 -Publish`。

离线检查不要求安装或启动游戏，存档测试只使用独立临时目录。
提交桥接修改时，应同时检查原生结构、协议版本、托管解码及相关测试。
不应取消游戏文件指纹校验来宣称支持新版本。

## 游戏测试

先备份自己的存档。实机测试与离线测试应分开报告，注明游戏版本、
复现步骤和未测试的场景；不把模拟检查当作真实游戏通过。
诊断脚本使用方法见 [research/README.md](research/README.md)。

不要在 Issue 或提交中附带游戏文件、个人存档、访问令牌、账户数据、
完整本机环境报告或未经检查的桌面截图。提交日志前请去除个人路径。

贡献的原创代码按项目 MIT 许可证分发；第三方素材保留原有许可证和署名。
