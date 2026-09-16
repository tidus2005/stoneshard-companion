# 存档窗口离线回归

使用真实窗口类，但不显示窗口、不运行主程序 App 的启动回调、不连接游戏。指定目录会创建一个测试 StoneShard 子目录，务必使用新建的 artifacts 测试目录。

构建 SaveUiProbe.csproj 后，通过 `dotnet SaveUiProbe.dll <临时测试目录> <助手exe路径>` 运行。按钮链路连续执行三次“更新当前状态”，以测试目录内的 probe-status.txt 记录结果。App.UiTestMode 在创建控制器前启用；不要将宿主替换为真实 App，否则其启动回调会启用正式会话逻辑。
