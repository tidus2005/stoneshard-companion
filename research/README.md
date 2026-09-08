# 本地程序研究工具

这些工具用于开发调试，不属于玩家日常使用流程。仓库不包含游戏文件、
反汇编输出、个人环境快照或测试存档。

需要 Python 3.11 或更高版本。静态 PE 分析还需要 `pefile` 和 `capstone`：

```powershell
python -m pip install --target .tools/python pefile capstone
$env:STONESHARD_DIR = '填写你自己的 Stoneshard 安装目录'
python research/analyze_native.py gml_Script_scr_player_move
python research/disassemble_function.py 1805180
python research/find_variable_uses.py grid_x grid_y
```

以上三个脚本只读取本地 EXE，输出留在忽略的 `artifacts/` 目录。
`inspect_game.py` 使用标准库，参数见 `python research/inspect_game.py --help`。

`prepare_sandbox.py` **会复制完整游戏目录、修改副本中的项目标识并创建测试设置**，
不应作为普通静态检查运行。仅用于已适配版本的独立测试环境准备；
后续实际游玩前必须确认测试副本使用 `ShardTest0` 存档空间。

`src/Probe` 中的 `SelfTest`、`OfflineTest` 为离线入口；其他命令可能访问
运行中的游戏、加载桥接组件或发送操作，应先阅读实现再使用。
