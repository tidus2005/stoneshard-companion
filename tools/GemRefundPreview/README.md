# 退点材料窗口离屏验证

独立 WPF 测试宿主，仅链接材料选择窗口与 Core；不创建助手主窗口、不连接游戏、不读取个人设置或存档。使用内存中的虚拟持有量检查空选择、自动凑值、超额、缺料、错误输入和历练不足，并输出界面预览。

```powershell
dotnet run --project tools/GemRefundPreview/GemRefundPreview.csproj -c Release -- artifacts/gem-refund-value-preview
```

逻辑检查：`dotnet artifacts/development/0.3.32/probe/StoneshardCompanion.Probe.dll LiveBuildTest`。
该命令只写随机临时测试目录，即使游戏正在运行也不会访问游戏。

价格来源验证：设置 `STONESHARD_DIR` 后运行 `python research/check_gem_refund_catalog.py`，只读取本机原版 EXE 内的物品表，确认原生、托管两端的顺序、全部 14 种宝石和价格一致。
