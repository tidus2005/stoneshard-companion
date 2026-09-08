# Windows 置顶控制条技术研究

研究日期：2026-09-05。本文仅交付技术研究和详细设计，未实现控制条，未向游戏发送输入，未进行真实游戏显示与输入实测，也未验证 mod 通信能力。

## 建议采用的结构

采用 **C# WPF 桌面控制条 + Win32 窗口/快捷键服务 + 可替换游戏适配层**。置顶 UI 与游戏能力解耦：可以先完成控制条、进程识别、快捷键和错误状态，再接入经实际版本验证的游戏功能。

WPF 的顶层窗口有对应 HWND，可通过 `HwndSource` 处理必要的 Win32 消息。这样可以使用 WPF 排版中文界面，同时精确控制窗口激活和热键行为。[Microsoft：WPF 与 Win32 互操作](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation)

| 部件 | 职责 | 关键边界 |
|---|---|---|
| OverlayWindow | 顶部控制条、倍速按钮、头盔操作、地图居中、状态 | 不承担游戏偏移地址、图像识别或时钟劫持逻辑 |
| GameSessionTracker | 识别游戏进程、主窗口、前台/最小化、位置与显示器变化 | 使用路径、进程 ID、启动时间和版本建立会话，不能只匹配窗口标题 |
| HotkeyService | 快捷键注册、冲突检测、防重复触发、紧急停止 | 只有正确游戏会话在前台时才发出操作 |
| CommandCoordinator | 串行操作、取消、超时、确认回执 | 头盔和相机指令不能无限排队或失焦后继续执行 |
| GameAdapter | 提供 SetSpeed、Helmet、CenterCamera、Reset | 对每个能力报告支持、状态是否可读以及版本匹配情况 |
| Diagnostics | 本地兼容性信息、命令耗时、失败原因 | 不记录游戏存档内容；诊断与产品按钮分离 |

外部适配器先后顺序应由本机游戏研究决定：优先使用能调用游戏原有逻辑的 mod 适配器；纯 UI 自动化可作为头盔快捷键的备用实现；原生速度 hook 作为独立、可关闭的实验适配器。控制条自身不能实现游戏时钟加快，也不能凭屏幕大小推算地图真实中心。

## 置顶、焦点与鼠标

1. 控制条使用无边框、小尺寸顶层窗口，设置 `WS_EX_TOOLWINDOW` 和 `WS_EX_NOACTIVATE`；WPF 显示时关闭激活行为。
2. 使用 `SetWindowPos(HWND_TOPMOST, ..., SWP_NOACTIVATE)` 设置置顶并移动位置。`TOPMOST` 表示位于非置顶窗口之上，不表示能覆盖所有系统界面，也不等于取得键盘焦点。
3. 处理 `WM_MOUSEACTIVATE` 返回 `MA_NOACTIVATE`，保留点击消息；按钮不获取键盘焦点，不调用 `Activate` 或循环抢回前台。
4. 设置页是单独的普通窗口，用户主动打开时才获得键盘焦点，负责热键录入、路径和校准。游戏中控制条避免文本框、会弹出新 HWND 的下拉菜单和复杂上下文菜单。

以上行为分别由 [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)、[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) 和 [WM_MOUSEACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate) 文档支持；具体 WPF 控件组合仍需通过真实游戏点击验证。

**首版采用紧凑实体控制条。** 例如上方居中排列：`1×  2×  3×  4× | 头盔 | 地图居中 | 暂停辅助 | 收起`。只有控制条本身占据鼠标区域，不创建一个覆盖全屏的透明交互窗口。默认固定在游戏所在显示器顶部，允许用户解锁拖动并保存位置。

需要收起后仍显示倍速时，可以分离为两层：交互控制条隐藏，非交互状态窗保留。状态窗作为 layered window 使用整窗鼠标穿透；再通过快捷键或托盘重新显示交互条。Microsoft 明确指出，layered window 的 alpha 为零区域可透过鼠标，layered window 配合 `WS_EX_TRANSPARENT` 时鼠标会传给其下方窗口。[Window Features：Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)

不能仅依赖 `WM_NCHITTEST → HTTRANSPARENT` 宣称穿透到另一个进程的游戏：官方定义涉及同一线程的下层窗口。透明、不可激活、不可点击是三个不同属性，应分别测试。[WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)

## 全屏支持的明确范围

| 游戏呈现模式 | 外部控制条方案 | 交付承诺 |
|---|---|---|
| 普通窗口 | 正常置顶合成 | 首版支持，需在本机验证 |
| 无边框全屏 | 正常置顶合成 | 推荐游戏设置，需在本机验证 |
| 标为全屏、实际由 Windows 全屏优化处理 | 可能表现为无边框模式 | 依据真实显示结果记录，不能只看游戏设置文字 |
| 真正独占全屏 | 普通桌面窗口无法给出始终覆盖的保证 | 首版不作为保证场景；使用窗口/无边框设置 |

主任务本机只读检查已读到当前配置：`video.displayMode=fullscreenBorderless`、游戏分辨率 `2560×1440`、显示分辨率 `3840×2160`。这与推荐无边框方案相符，也说明需要认真处理渲染分辨率与屏幕坐标转换；配置文件不能证明屏幕实际呈现模式或置顶覆盖已经通过验证。

Microsoft 说明，全屏优化可以把部分独占全屏游戏作为优化后的无边框窗口运行；独占全屏中的外部绘制通常需要介入游戏渲染与呈现流程。这支持将“外部置顶条”和“游戏内渲染叠加”分成两个技术方案，而不是把一个 Topmost 属性当作全部兼容性保证。[Microsoft DirectX：Fullscreen Optimizations](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/)

现代窗口化 flip model 还可能采用 Independent Flip；出现上层内容时，DWM 可以恢复合成或利用硬件 overlay。不能仅凭全屏时性能较好，推断游戏一定是独占全屏。[Microsoft：DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)

若后续确有独占全屏的强制要求，再研究游戏内 mod 绘制控制条或渲染 hook；这是独立兼容性项目，不能在尚未确认 Stoneshard 渲染后端时定为已可行。

## 快捷键和输入事务

快捷键使用 `RegisterHotKey` 接收 `WM_HOTKEY`，加 `MOD_NOREPEAT` 防止按住时连续触发。支持修改按键并显示注册冲突。避免 F12（官方为调试器保留，也常与截图动作冲突）；Windows 键组合保留给系统。默认具体按键应在读取游戏现有键位后确定。[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)

通过 `SetWinEventHook(WINEVENT_OUTOFCONTEXT)` 监听前台及游戏窗口变化，回调只更新状态并派发任务，不在回调内执行慢操作。该方式不把回调 DLL 映射进游戏进程；需要消息循环并正确持有托管回调引用。游戏离开前台时取消操作，并注销普通游戏热键，防止在其他软件里吞掉组合键；恢复前台时重新尝试注册并报告冲突。紧急停止可保持全局注册。[SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)

每次指令开始、每个输入步骤前，重新检查前台 HWND 是否属于已绑定游戏会话。`GetForegroundWindow` 可能暂时返回空，必须视为不能发送输入。[GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)

头盔 UI 自动化应设计为可取消事务：

1. 校验游戏会话、前台、窗口未最小化、当前分辨率与 UI 缩放校准有效。
2. 等待触发热键所用的 Ctrl/Alt/Shift 等实际释放，避免准备发送的 B 变成 Ctrl+B；不擅自释放用户正在按住的按键。
3. 判断面甲动作所在菜单是否已打开，只在关闭时发送经过校验的菜单键。B 对应的可能是动作/模式菜单，不在未实测前称其为背包键。
4. 等待菜单/目标控件实际出现；超时即结束。固定延迟只能做轮询间隔，不能当作打开成功。
5. 找到头盔操作入口并确认语义，执行一次，再检查结果。
6. 仅当本事务打开该菜单且仍可确认当前状态时关闭菜单。记录原鼠标位置；用户在事务中移动鼠标则停止并放弃恢复鼠标位置。

`SendInput` 将键鼠事件插入系统输入流，并非定向的后台控制 API。它受 UIPI 完整性级别限制，不能从较低权限进程向较高权限进程发送输入；还会受到用户已按下按键影响。游戏是否接受这些输入必须实测。首版默认与游戏同为普通用户运行；不要自动申请管理员权限，也不要宣称改为管理员即可解决所有游戏输入问题。[SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

纯宏模式读不到头盔状态时，应显示“执行切换”以及“结果未验证”，不能把本地布尔值翻转后显示成游戏的真实开关状态。重复点击时拒绝并发；不可将一次 Toggle 超时后自动重试，以免第一次其实成功导致第二次又切回。

## DPI、多显示器与位置校准

声明 Per-Monitor V2 DPI awareness。WPF 布局使用 DIP；游戏窗口与鼠标坐标保持明确的物理像素坐标系，转换集中在一个 GeometryService，处理 `WM_DPICHANGED` 和显示器变更。Microsoft 推荐 PMv2，并要求验证混合 DPI、多屏移动与运行中缩放变更。[High DPI Desktop Application Development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)

控制条位置保存为“显示器标识 + 边缘锚点 + DIP 偏移”，不要只存桌面绝对坐标；显示器缺失时回到游戏所在显示器。窗口化时识别实际游戏客户区，不能把标题栏、边框也算进点击校准。

UI 自动化的校准键包含游戏版本、客户区尺寸、游戏 UI 缩放、布局状态；任一变化都重新确认。跨屏坐标可能为负数。使用绝对鼠标输入时，按虚拟桌面范围归一化，并按需要设置 `MOUSEEVENTF_VIRTUALDESK`，不能只按主显示器宽高换算。[MOUSEINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput)

## 游戏适配与退出恢复

以下是拟定接口和行为，尚不代表 Stoneshard 已存在这些 API：

- `GetCapabilities`：返回版本匹配、倍速、头盔读写和相机操作能力；不支持的按钮禁用并说明原因。
- `SetSpeed(1|2|3|4)`：返回已应用倍率；UI 区分请求值、后端确认值。实际移动耗时是否达到相应比例另行测量。
- `SetHelmetState(open|closed, expectedState)`：能读状态时优先使用显式目标状态；只能切换时使用不可自动重试的 `ToggleHelmet`。
- `CenterCamera(expectedRoomId)`：从游戏当前地图边界求中心，并校验指令期间没有换图；不能用屏幕中心替代地图中心。
- `Reset`：恢复 1×、取消队列、停止自动触发；相机控制应有明确退出/回到玩家机制。

若 mod 能通过原生扩展通信，优先本机命名管道；若只有游戏脚本文件 API，则可研究受控目录中的原子命令文件与回执文件。两者均须先确认实际 mod 能力，不假设 GameMaker 脚本天然提供 .NET 命名管道接口。协议包含版本、会话 ID、命令 ID、预期地图/状态、截止时间，防止旧会话命令在读档或重启后重新执行。

原生速度 hook 单独隔离在匹配游戏位数的适配器中。它只改变目标进程用到的时间视图，不改 Windows 系统时间；但选择哪个时间源、游戏循环是否限速、动画/输入/音频的影响均须以当前游戏版本实测确定。单独 hook 一个计时函数不能保证稳定实现 2×、3×、4×。

后端应自带心跳失效恢复：控制条失联或正常退出时恢复 1×；心跳计时不能来自已被加速的虚拟时钟。正常退出先取消命令、请求 1× 并等待回执，再注销热键和窗口事件。若恢复失败，显示明确状态，不能把“控制条已关闭”当作“游戏速度已恢复”。原生模块退出需保证没有线程仍执行其代码，不做盲目的卸载；保持已禁用的 pass-through 状态直到游戏退出也应作为设计选项。

## 必须完成的真实验收

| 场景 | 验收条件 |
|---|---|
| 进入游戏、切换地图、重新读档、Alt+Tab 返回 | 条的位置与可见性正确，绑定当前进程会话 |
| 点击所有控制条按钮 | 游戏前台 HWND 不变；操作按钮点击不同时落到游戏地图 |
| 控制条周边、透明角、收起状态 | 应穿透的区域点击可到达游戏，不出现全屏隐形遮挡 |
| 100%、125%、150%、200% 缩放与双屏 | 控件可读、位置不漂移、校准不误点；次屏负坐标正确 |
| 游戏失焦时触发快捷键 | 无游戏输入、无菜单后续点击；正常软件快捷键不被持续占用 |
| 动作菜单已开/未开、不同头盔状态、加载中 | 仅执行符合前置条件的操作，超时不重复 Toggle |
| 调整速度后控制条正常退出和被强制结束 | 1× 恢复得到后端或实际行为验证 |
| 独占全屏与无边框分别运行 | 记录真实覆盖结果，失败场景不写为“全屏已支持” |

以上是待开发的验收标准，目前没有完成任何一项游戏运行验证。
