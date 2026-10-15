# 新功能离屏预览

链接正式的采集配置、属性面板、出发清单和主题，使用独立的演示协调器。不显示窗口，不连接游戏，不读取个人设置或真实存档。

```powershell
dotnet build tools/FeaturePreview/FeaturePreview.csproj -c Release --artifacts-path artifacts/preview-build -o artifacts/feature-preview-host
dotnet artifacts/feature-preview-host/StoneshardCompanion.dll artifacts/development/0.3.18/previews
```

生成宽、窄、长属性面板、采集配置表以及默认/窄屏出发清单预览，同时检查分类下拉框已移除、四角调整控件存在、基线按钮更新真实视图的差值、清单重置会清空本次勾选。游戏内行为另行测试。
