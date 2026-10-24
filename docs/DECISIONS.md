# PixShell 决策台账

状态含义：**Proposed** 待确认，**Accepted** 已确认并应遵守，**Superseded** 已被后续决策替代。这里记录稳定且影响开发方式的决策；实现细节以引用的源码或详细文档为准。

## D-001：Repository-driven Handoff 核心文档

- 状态：**Accepted**（2026-10-12）
- 决策：以 `AGENTS.md`、`docs/ARCHITECTURE.md`、`docs/DECISIONS.md`、`docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 作为 AI 协作的统一恢复入口；Git 是跨机器同步的唯一可靠事实来源。
- 理由：支持新会话、上下文压缩和多电脑接力，避免依赖聊天历史或重复审计。
- 依据：[AGENTS.md](../AGENTS.md)。

## D-002：双原生单仓库

- 状态：**Accepted**
- 决策：macOS 使用 AppKit/SwiftTerm/SwiftNIO，Windows 使用 WPF/WebView2/xterm.js/SSH.NET；两端独立实现但对齐核心模型、布局和桥契约。
- 理由：保留平台原生集成和 SSH 实现选择，同时维持用户工作流一致。
- 依据：[mac/Package.swift](../mac/Package.swift)、[win/PixShell.csproj](../win/PixShell.csproj)、[mac/docs/LAYOUT-PARITY.md](../mac/docs/LAYOUT-PARITY.md)。

## D-003：本地优先、无服务端数据库

- 状态：**Accepted**
- 决策：主机与应用数据保存在本机；密码不进入 hosts JSON，使用平台安全存储保护。WebDAV 仅是可选备份渠道。
- 理由：SSH 客户端需要离线可用，并减少集中式服务依赖与敏感数据暴露面。
- 依据：[mac/Sources/PixShell/Store/HostStore.swift](../mac/Sources/PixShell/Store/HostStore.swift)、[mac/Sources/PixShell/Store/Keychain.swift](../mac/Sources/PixShell/Store/Keychain.swift)、[win/Storage.cs](../win/Storage.cs)。

## D-004：本机 Agent 桥与 GUI 会话隔离

- 状态：**Accepted**
- 决策：CLI/MCP 经 token 保护的回环 HTTP 桥调用无头会话池；不得复用、抢占或在 GUI 中隐式创建用户标签会话。
- 理由：自动化并发、断线重连与用户交互彼此隔离，避免 Agent 影响可见工作区。
- 依据：[mac/Sources/PixShell/App/AppDelegate+Bridge.swift](../mac/Sources/PixShell/App/AppDelegate+Bridge.swift)、[mac/Sources/PixShell/Bridge/HeadlessBridgeHost.swift](../mac/Sources/PixShell/Bridge/HeadlessBridgeHost.swift)、[win/Bridge/HeadlessBridgeHost.cs](../win/Bridge/HeadlessBridgeHost.cs)。

## D-005：兼容优先的 SSH 回退

- 状态：**Accepted**
- 决策：常规交互优先使用内置 SSH 实现；遇到算法兼容、私钥或 FIDO2 限制时，允许使用系统 OpenSSH 回退。SFTP 同样可采用回退实现。
- 理由：覆盖老旧服务器、网络设备和硬件安全密钥场景。
- 依据：[mac/Sources/PixShell/SSH/OpenSSHSession.swift](../mac/Sources/PixShell/SSH/OpenSSHSession.swift)、[mac/Sources/PixShell/App/AppDelegate+Sessions.swift](../mac/Sources/PixShell/App/AppDelegate+Sessions.swift)、[win/TerminalSession.cs](../win/TerminalSession.cs)。

## D-006：Windows FinalShell 工作流对齐

- 状态：**Accepted**（仅范围原则）
- 决策：借鉴 FinalShell 的高频工作流，仅改造 Windows 端；优先复用已存在的远端编辑器和快捷命令模块，不复制第三方代码、资源或品牌，不为功能表对齐进行大规模重构。
- 理由：用户明确要求 Windows 端改造，目标是改善日常运维效率而非替换现有架构。
- 依据：[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)。具体 W2–W4 功能仍为 Proposed，须逐项确认。

## D-007：dev 集成分支与可安装 Windows 交付门

- 状态：**Accepted**
- 决策：`main` 仅作为发布分支；Windows 改造及后续功能切片完成后，必须经验证并推送到远程 `dev` 集成分支，不直接合并 `main`。`dev` 不自动运行完整 GitHub 构建，需要时通过手动触发构建；`main` push 自动运行构建。`dev → main` 需用户明确批准，合入后以 `main` CI 作为发布构建证据。
- 理由：让多电脑/多会话接力有稳定集成点，同时避免每次开发提交消耗完整跨平台打包资源。
- 约束：`origin/dev` 已于 F0 创建；功能切片仍不得以直接推送 `main` 替代。CI 已支持手动触发，且 `main` push 自动构建已由 F0 补齐；`dev` 不得添加自动完整构建。
- 依据：[AGENTS.md](../AGENTS.md)、[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)、[.github/workflows/build.yml](../.github/workflows/build.yml)。

## D-008：W4 分阶段交付

- 状态：**Accepted**（2026-10-14）
- 决策：Windows W4 按风险拆分：先实现 W4a（监控图表时间窗与网卡选择），再实现 W4b（命令建议），最后单独设计 W4c（批量主机任务）。W4a 保持既有三秒采样频率与单会话监控模型；W4b 仅提供本地候选回填，不能发送命令、触发远端查询或改变广播语义；W4c 不创建后台常驻任务。
- 理由：批量主机任务涉及目标选择、并发与失败呈现，属于独立业务语义；先交付可复用且不改变 SSH 会话边界的监控增强。
- 依据：用户于 2026-10-14 确认 W4 并同意顺序；[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)、[win/UI/MonitorSidebar.xaml.cs](../win/UI/MonitorSidebar.xaml.cs)。

## D-009：W4c 已连接会话批量执行语义

- 状态：**Accepted**（2026-10-14）
- 决策：W4c 只允许用户显式选择当前已连接的 GUI 会话，不自动连接已保存主机、不读取或扩展凭据范围。命令通过各会话独立 `ExecAsync` 通道执行，最大并发为 3；单台失败继续其余任务；结束后按会话汇总状态和输出，每台输出最多保留 12,000 字符。
- 理由：在不改变现有交互式广播语义、会话隔离或凭据边界的前提下，提供可见、可控的批量维护能力，并防止无界并发和结果窗口膨胀。
- 依据：用户于 2026-10-14 确认 W4c；[win/MainWindow.xaml.cs](../win/MainWindow.xaml.cs)。

## D-010：W2 分阶段 SFTP 可靠性与目标保护

- 状态：**Accepted**（2026-10-14）
- 决策：W2 按风险拆分为 W2a–W2c。W2a 先建立 Windows 端进程内统一传输任务模型、直传进度、失败重试入口与下载 `.part` 临时文件后原子落盘；W2b 只为单文件直传实现取消和断点续传；W2c 单独定义打包传输的取消、覆盖与清理，不将其宣称为文件级续传。对于已存在的完整同名目标，默认保留，必须由用户明确选择覆盖、另存为或取消；上传使用远端临时文件，完成后才改名到最终路径。W2b 的续传仅限当前应用运行期间；SCP 后端可正常重新开始但不承诺续传。
- 理由：当前 SFTP/SCP/系统 SFTP 三条后端路径只有同步流传输接口，且既有直传会直接覆盖同名目标。先统一任务状态与下载安全落盘，避免为追求“续传”而在未覆盖全部后端和打包流程时损坏完整文件。
- 依据：用户于 2026-10-14 确认默认保护与续传范围；[win/SftpPanel.xaml.cs](../win/SftpPanel.xaml.cs)、[win/Sftp/RemoteFs/IRemoteFs.cs](../win/Sftp/RemoteFs/IRemoteFs.cs)、[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)。

## D-011：Windows SSH 重连前先释放 SFTP

- 状态：**Accepted**（2026-10-14）
- 决策：Windows GUI 会话执行断开、原地重连、密码重试或关闭活动标签前，必须先释放该会话关联的 SFTP 客户端，并取消、等待在途 SFTP 后端探测和编辑器下载结束；清理操作必须使在途 SFTP 建连结果失效，旧的异步结果不得回填到新会话。SFTP 连接失败路径必须立即 Dispose 新建客户端。原地重连期间串行化重复请求，并只对版本交换前关闭或 TCP reset/abort 最多进行三次短退避连接尝试。
- 理由：SFTP 使用独立 SSH 连接。对 OpenWrt Dropbear 等资源释放较敏感的服务端，先关闭终端而延后清理 SFTP 会扩大立即重连时的残留连接窗口；异步探测竞态以及失败客户端未 Dispose 都可能令短时 socket/子会话延后回收。有限重试只缓解已知瞬态握手中断，不能掩盖认证和持久配置错误。
- 约束：版本交换前被关闭只能记录为诊断线索，不能据此断言为客户端或 Dropbear 单方故障；必须以 Windows + OpenWrt 的重复手工复测和服务端日志确认。
- 依据：[win/MainWindow.xaml.cs](../win/MainWindow.xaml.cs)、[win/SftpPanel.xaml.cs](../win/SftpPanel.xaml.cs)、[win/Sftp/RemoteFs/RemoteFsFactory.cs](../win/Sftp/RemoteFs/RemoteFsFactory.cs)、[win/Sftp/SftpConnectionEpoch.cs](../win/Sftp/SftpConnectionEpoch.cs)、[win/TerminalSession.cs](../win/TerminalSession.cs)。

## D-012：W3 SSH 隧道管理不纳入本轮 Windows 改造

- 状态：**Accepted**（2026-10-14）
- 决策：取消 W3（SSH 隧道/端口转发管理）的实施；本轮 Windows FinalShell 工作流对齐不新增本地、远程或动态端口转发的管理界面、持久化或重连语义。
- 理由：用户明确决定不做该功能。既有代理与 SSH Jump 能力维持原状，不能借此决策删除或改变它们。
- 依据：用户于 2026-10-14 明确确认；[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)。

## D-013：备份包 v1 的完整、非破坏性恢复

- 状态：**Accepted**（2026-10-16）
- 决策：Windows 本地导入和 WebDAV 恢复必须共用同一条 v1 备份包应用路径：主机与快捷命令按 ID 合并（同 ID 以备份更新、缺失项新增、不删除仅存在于本机的项），刷新命令面板，并恢复备份包已声明且可识别的主题、终端配色与终端背景设置。密码仍不进入备份包。旧版纯主机数组导入保持兼容，且不虚构不存在的命令或设置。
- 理由：`BackupBundle` 已将 `Hosts`、`QuickCommands` 与 `Settings` 定义为同一备份快照；此前两个恢复入口只应用主机，导致“下载/导入成功但命令未出现”的已证实数据恢复缺口。与既有主机 `Upsert` 一致的合并语义避免恢复时静默删除本机独有命令。
- 依据：[win/Store/BackupBundle.cs](../win/Store/BackupBundle.cs)、[win/Store/QuickCommands.cs](../win/Store/QuickCommands.cs)、[win/MainWindow.xaml.cs](../win/MainWindow.xaml.cs)。

## D-014：Windows 远端新建文件的安全语义

- 状态：**Accepted**（2026-10-24）
- 决策：Windows 文件管理器新增“新建文件…”，允许任意扩展名和无扩展名文件；仅在当前远端目录创建单个空文件，禁止输入路径、`.`、`..`、NUL 与空白名称。创建不得覆盖同名文件或目录；成功后刷新目录并使用既有图形编辑器打开。SSH.NET、系统 SFTP/Proc 与 SCP 回退均须实现同一“不覆盖创建”语义，不能借用会覆盖的上传流程。
- 理由：满足 FinalShell 式高频文件维护工作流，同时保留 W2 的完整目标保护原则，避免“新建”意外破坏远端现有内容。
- 依据：用户于 2026-10-24 确认；[WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)、[win/Sftp/RemoteFs/IRemoteFs.cs](../win/Sftp/RemoteFs/IRemoteFs.cs)。
