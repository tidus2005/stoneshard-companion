# Stoneshard 变速技术研究

研究日期：2026-09-05。范围仅为官方文档、作者发布页和公开源码的只读核查；未安装、编译、注入或运行第三方修改器，未验证游戏内效果。

## 结论

1. **1× / 2× / 3× / 4× 有直接的 Stoneshard Mod 源码先例。** 既有实现调用 GameMaker 的 `game_set_speed(..., gamespeed_fps)`，以 40 为基准设定目标游戏帧率，因此四档为 40 / 80 / 120 / 160。它加速依赖游戏帧的执行和动画，不是角色每回合可以多走几格。[SpeedManager 作者说明](https://www.nexusmods.com/stoneshard/mods/9?tab=description)、[GameMaker API](https://manual.gamemaker.io/monthly/en/GameMaker_Language/GML_Reference/General_Game_Control/game_set_speed.htm)
2. **当前正式版不能直接套用这些 GML 补丁。** 原版 MSL 的作者指南要求 Steam `modbranch`，且标题版本带 `-vm`；YYC 是编译成机器码的运行形式。应先用本机包结构及版本能力检查选择后端，而不能仅凭存在 `data.win` 判断可修改逻辑。[MSL 作者安装指南](https://modshardteam.github.io/ModShardLauncher/guides/how-to-play-mod.html)、[GameMaker YYC 文档](https://manual.gamemaker.io/monthly/en/Settings/YoYo_Compiler.htm)
3. **MSL Enhanced 新增的“可加载 YYC data.win”声明不能当作这些功能已经兼容 YYC 的证明。** 作者 v1.12 更新日志有该声明，但本轮未找到相应公开实现证明它能修改当前 Stoneshard YYC 的原生逻辑。应准确写成“资源包可加载的作者声明；速度、头盔、相机逻辑补丁仍未证实”。[Enhanced 作者发布页](https://www.nexusmods.com/stoneshard/mods/103?tab=description)

## 可复核源码

下表的 commit 通过作者 GitHub 仓库 API 当日读取；链接固定 commit，避免后续分支漂移。

| 项目 | 核查到的版本 | 关键位置 | 证据说明 |
|---|---|---|---|
| Nylux/Stoneshard-SpeedManager | 源码 1.1.1；commit `c95b097deaf3d82f3902d5f33bce336f425b85f8`，2024-02-07；Nexus 文件更新日期 2025-01-28 | `SpeedManager.cs` 把 GML 插入 `o_player` 的 F3、F4 和 Alarm7 | 作者源码与后来发布包不是同一时间的快照，不能假定完全一致 |
| remyCases/SpeedshardCore | 源码 2.2.1，`TargetVersion=0.9.3.7`；commit `cc76a0684d2cabfb0467ab2e8c6827bae40a546f`，2025-09-05 | `Speedshard_Core.cs` 中 `Speed()`；`codes/Gml/speed_keypress.gml` | 能确认 API 与接入点，不能据此宣称支持本机 0.9.4.25 |
| ModShardTeam/ModShardLauncher | main commit `368dfb0602945ea88ef675204d7e4ed10497de64`，2025-09-22 | `LoadGML`、`MatchFrom`、`InsertBelow`、`Save`；事件/对象/扩展与设置菜单 API | 原版是离线修改游戏数据包的工具；它不是已提供给悬浮窗使用的运行时控制 API |
| MSL Enhanced | 页面顶部标 1.12，日志还出现 1.13；文件日期 2026-06-12 | 作者更新日志与功能描述 | 本轮未找到可固定 commit 的增强版源码；不能只凭页面版本号建立兼容性白名单 |

SpeedManager 的实际行为：

- [SpeedManager.cs](https://github.com/Nylux/Stoneshard-SpeedManager/blob/c95b097deaf3d82f3902d5f33bce336f425b85f8/SpeedManager.cs#L13)：向 `gml_Object_o_player_KeyPress_114`、`KeyPress_115`、`Alarm_7` 插入逻辑。
- [speedF3.gml](https://github.com/Nylux/Stoneshard-SpeedManager/blob/c95b097deaf3d82f3902d5f33bce336f425b85f8/Codes/speedF3.gml)：每次减 40，最低 40。
- [speedF4.gml](https://github.com/Nylux/Stoneshard-SpeedManager/blob/c95b097deaf3d82f3902d5f33bce336f425b85f8/Codes/speedF4.gml)：每次加 40，没有本产品所需的 4× 上限。
- [speedAlarm7.gml](https://github.com/Nylux/Stoneshard-SpeedManager/blob/c95b097deaf3d82f3902d5f33bce336f425b85f8/Codes/speedAlarm7.gml)：初始化目标 40，调用 `game_set_speed`，再次设置 Alarm7。

Speedshard Core 的实际行为：

- [Speedshard_Core.cs](https://github.com/remyCases/SpeedshardCore/blob/cc76a0684d2cabfb0467ab2e8c6827bae40a546f/Speedshard_Core.cs)：设置菜单提供 40–160 滑条，`Speed()` 替换 `gml_Object_o_player_KeyPress_115`，并在玩家 Create 中恢复 `global.gamespeed`。
- [speed_keypress.gml](https://github.com/remyCases/SpeedshardCore/blob/cc76a0684d2cabfb0467ab2e8c6827bae40a546f/codes/Gml/speed_keypress.gml)：F4 在默认和加速值之间切换，随后设置游戏速度。
- [load_ini.gml](https://github.com/remyCases/SpeedshardCore/blob/cc76a0684d2cabfb0467ab2e8c6827bae40a546f/codes/Gml/load_ini.gml)：默认值 40。

本产品宜只实现独立的速度模块，避免整包引入 Speedshard Core 的经验、声望、技能点、饮水等额外规则。

## “游戏时钟加快”的产品定义

Steam 官方将 Stoneshard 定义为回合制游戏。因此要把“每秒执行得更快”与“每次行动消耗的回合数、世界时间”分开验证。[Steam 官方页面](https://store.steampowered.com/app/625960/Stoneshard/)

建议验收语义为：走相同格数、执行相同行动，消耗相同回合与游戏内资源，现实等待缩短；站立不输入时不应因工具新增回合推进。这里是本产品的预期与待验证约束，不是本轮已经运行证明的结果。若用户以后要求单独改变昼夜、饥渴或回合消耗，应另立功能，不能隐含在变速里。

设置 160 表示目标游戏帧率，不能显示成已实测四倍。GameMaker 官方说明 `game_get_speed` 返回尝试维持的目标值；实际性能要另测。建议界面显示“目标 4× · 已应用”，性能不足时显示“实际加速受性能限制”。使用相同 20–40 格行走路径计时三轮取中位数，记录实际倍速及变动。[game_get_speed 官方说明](https://manual.gamemaker.io/lts/en/GameMaker_Language/GML_Reference/General_Game_Control/game_get_speed.htm)

## 两种后端路线

### A：保留当前正式分支，进程内计时缩放，先做兼容性试验

Cheat Engine 作者代码证实通用变速可以通过目标进程的 QPC、GetTickCount 等计时函数拦截实现；切换倍率时重设实时时基与虚拟时基保持连续。这是通用原理证据，不是本机 Stoneshard 0.9.4.25 的兼容性证明。[作者 speedhack2.pas](https://github.com/cheat-engine/cheat-engine/blob/master/Cheat%20Engine/speedhack2.pas)、[作者 speedhackmain.pas](https://github.com/cheat-engine/cheat-engine/blob/master/Cheat%20Engine/speedhack/speedhackmain.pas)、[作者 SpeedhackV3.lua](https://github.com/cheat-engine/cheat-engine/blob/master/Cheat%20Engine/bin/autorun/SpeedhackV3.lua)

设计约束：只作用于经路径、进程 ID、架构、可执行文件指纹匹配的游戏实例；倍率白名单 1/2/3/4；时钟单调连续；相同命令幂等；恢复 1× 与卸载注入分开建模。时钟缩放会影响使用这些计时 API 的同进程组件，仍需测菜单、过场、声音、切图、加载/保存、长时间运行。未知版本不自动写入。计时后端是否能可靠做到“进入战斗自动恢复”取决于另有游戏状态通道，单靠时钟拦截无法判断战斗。

助手进程与游戏中的后端必须有命令确认及独立于虚拟游戏时钟的心跳。助手退出或连接丢失时后端恢复 1×，不能只在 UI 的退出事件发恢复指令。实际注入与解绑方式留待开发试验选择。

### B：Steam modbranch + 极小语义桥接 Mod

在单独兼容性验证后的 VM 包里，经 MSL 注入极小控制器，提供四档速度、读取结果、头盔操作及相机操作接口。由控制器统一决定当前状态能否执行，避免悬浮窗盲发按键。

速度应保存经验证的原始基准值，设为 `base * multiplier`；初次会话与错误恢复使用基准。检测到其他速度 Mod 应报告冲突并选择唯一控制方，不同时运行多个反复调用 `game_set_speed` 的 Alarm/Step 控制器。首次版本匹配后仍要验证补丁锚点恰好命中、设置生效与读回一致。

跨 YYC / VM 的旧存档兼容性本轮未得到开发者的明确承诺，不能自动切分支或转换现有角色。此路线应先建立隔离测试档，作为语义功能的高确定性候选；是否采用由保留现有游戏进度的需求决定。

## 开发前必须关闭的证据缺口

| 缺口 | 最小验证 | 通过条件 |
|---|---|---|
| 当前 YYC 是否能可靠计时变速 | 测试角色上 1/2/3/4× 相同路线，分别三轮 | 无异常；消耗与终点一致；实际用时有可解释变化 |
| 游戏所有时钟是否保持正确语义 | 相同行动前后核对世界时间、状态持续回合、饥渴；另测站立 | 工具未新增游戏行动或改变每行动代价 |
| 宕机后恢复 | 4× 时模拟助手崩溃及 IPC 断开 | 后端主动恢复 1×，不依赖游戏虚拟计时 |
| VM 当前版本接入点 | 在隔离 VM 数据副本解析命名事件与速度调用 | 接入点存在、补丁可生成、游戏内可读回 |
| 重复控制与场景切换 | 多次切档、切图、开关菜单；检测已有速度 Mod | 基准不漂移，倍率不相乘，不静默互相覆盖 |

证据等级：本报告所引 GameMaker/Steam 官方文档和作者源码属于一手材料；Nexus 作者描述属于作者声明。尚无游戏内运行验证，所有本产品行为、稳定性和兼容性结论均保留为开发验收项。
