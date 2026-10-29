# 当前开发计划

## Current Phase

**Windows FinalShell 工作流对齐：F0 已完成；W0 主路径验收通过且失败路径暂缓；W1、W2a–W2c、W4a–W4c 与 W5 已完成实现；OpenWrt/Dropbear 立即重连的客户端补强已完成，本轮不再作为 PixShell 独有故障继续修复；Windows 备份完整恢复修复已实现并待集中验证；W3 已排除。**

历史原生化路线保留在 [mac/docs/BLUEPRINT.md](../mac/docs/BLUEPRINT.md) 与 [win/BLUEPRINT.md](../win/BLUEPRINT.md)。其 P0–P4 状态是早期计划，不作为当前实时进度；当前阶段以代码、Git 和 [HANDOFF.md](HANDOFF.md) 为准。

## Goal

在不改动 macOS 和既有 SSH/SFTP/Agent 架构的前提下，先建立可在 GitHub 构建 Windows 安装产物的质量门与 `dev` 集成流程，再完成 Windows 端已有远端编辑工作流的验收改进和快捷命令同组排序。详细范围和后续决策门见 [WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)。

## Completed

- 双端原生 SSH/SFTP 客户端、会话、SFTP、代理、监控、编辑、备份与本机 Agent/CLI 桥已有实现。
- 最近一轮代码工作集中完成 Agent 无头隔离、并发 SSH 复用、死会话自动重连和输出洪峰卡顿修复（见最近 Git 提交）。
- 本轮已建立五份核心协作文档及 Session Start/Handoff 协议。
- 已完成 Windows 与 FinalShell 工作流差异审查，并固化 Windows 对齐改造契约。
- F0（Windows）：已建立 `origin/dev`、最小快捷命令测试、`main` 自动 CI / `dev` 手动 CI 策略与 Windows artifact 交付链。手动 `dev` run `38252736394` 和自动 `main` run `38253363885` 的 Windows job 均通过 Test、Publish、zip、Inno Setup 与 artifact 上传。
- W0：Windows 实机已通过目录双击、文本文件双击打开编辑窗口、编辑保存回传与超 4 MiB 文件明确提示。上传失败时保留脏内容因未构造安全失败条件，按用户指示暂不测试；不得记录为通过。

## In Progress

- W1：同组“上移/下移”、跨组移动追加到目标组末尾、显式 `sortOrder` 持久化和同组拖拽排序均已实现；用户要求后续集中验证，待测项已记入对齐契约。
- W4a：监控图表时间窗与远端网卡选择已实现并形成未验证的 `dev` 检查点；待整个 Windows W 路线完成后集中验证。
- W4b：命令建议已实现本地历史/快捷命令候选、建议按钮与 `Ctrl+Space` 回填入口；待后续集中验证。
- W4c：当前已连接会话的显式批量执行、并发 3、失败继续和逐会话结果汇总已实现并形成未验证的 `dev` 检查点；待后续集中验证。
- W2a：统一传输任务、进度、失败重试入口和下载 `.part` 原子落盘已实现并形成未验证 `dev` 检查点；验证仍按用户约定集中执行。
- W2b：单文件直传取消/续传、远端临时文件上传和 SCP 重启降级已实现；待集中验证。
- W2c：打包传输的本地临时解压、冲突保护、任务取消和远端临时目录尽力清理已实现；待集中验证。
- OpenWrt/Dropbear SSH 立即重连：已完成 SFTP 优先释放、在途操作取消、失败客户端释放、生命周期串行化与有限瞬态重试。2026-10-18 实机观察表明，在 PixShell 无法连接后 FinalShell 也出现“connection is closed by foreign host”；故障不再满足“PixShell 独有”前提，本轮停止继续叠加客户端修复。该结论不等于已定位 OpenWrt 根因；仅在未来能稳定复现“同一时刻 FinalShell 正常而 PixShell 失败”时重开调查。
- Windows 备份恢复：已确认并修复本地导入与 WebDAV 恢复只写回主机、遗漏 `QuickCommands`/`Settings` 的缺口。两入口现共用备份应用路径，命令按 ID 非破坏性合并并刷新面板，已识别设置恢复；新增纯逻辑回归测试，按用户要求等待集中运行。
- W5：远端新建文件已实现：右键菜单与底部坞入口、单文件名校验、排他零字节创建、目录刷新后选中并打开既有编辑器；SSH.NET、Proc SFTP 和 SCP 后端均覆盖。并修复 Proc SFTP 的目录/删除此前静默空操作。Windows 手工与 WPF 编译仍待集中验证。
- D-015：非空远端目录递归删除的二次确认已实现；Windows 手工验证仍待集中执行。
- D-016：双平台源码、CI/安装包默认版本和更新仓库已对齐至 `0.2.0` / `miozen/pixshell`，增加版本一致性检查及发布版本门槛；Windows/macOS 实际构建和更新检查尚待验收。旧发布工作流现在仅允许从 `main` 发布与源码匹配的版本。

## Remaining

- 在适当的可控环境补测 W0 的“上传失败保留编辑内容”路径；该项暂缓不等于验收通过。
- OpenWrt/Dropbear 重连问题本轮按外部/服务端或链路共享状态处理并结束客户端修复；未来仅在存在可重复的 PixShell/FinalShell 对照差异时，补齐 Dropbear、conntrack/防火墙和代理路径证据后再重开。
- W3 已由 D-012 排除；W1、W2、W4 与 W5 的全部已确认实现事项已完成，待按用户约定统一验证。
- 集中验证时覆盖 W5：`.env`、`config.json`、无扩展名和 Unicode 文件名；同名文件/目录不覆盖；权限失败反馈；三种远端后端不静默成功；创建后能打开、保存并在刷新后可见。
- 集中验证时覆盖 v1 本地导入与 WebDAV 恢复：新增/更新的快捷命令应立即显示并在重启后保留；本机独有命令不得被删除；旧纯主机数组仍可导入；有效主题、终端配色和背景设置应恢复，密码不得出现在备份包或日志中。
- 作为独立后续事项评估并设计 macOS 内置 SSH/SFTP 的 host-key 验证统一策略；不得在未覆盖 OpenSSH 回退和两端行为前局部修改。
- 在后续变更中逐步扩大最小自动化测试，优先桥路由、持久化和 SSH 状态机等非 UI 核心逻辑。
- 发布前统一 README、CI 默认值和产品版本信息，并验证 macOS 正式签名/公证策略。

## Acceptance Criteria

下一项任务开始前：

- Session Start 能从五份核心文档、Git 和源码恢复真实状态。
- 不覆盖未提交修改，不把未验证工作写为完成。
- 所有变更保持两端既有架构约束，运行与任务风险相称的验证。

F0/W0/W1 的验收标准以 [WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md) 为准；详细历史切片标准继续参考 `mac/docs/` 和 `win/` 下的原始 SPEC/蓝图。

## Validation

- 本轮文档：检查标准文件存在、相对链接有效、Git 状态与 Handoff 一致，并执行 `git diff --check`。
- F0 静态复核：已核对测试项目、`win/build.ps1 -Action test` 入口和 CI 触发范围。WSL 的 .NET 9.0.121 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release`，3/3 通过；完整 WPF 构建仍须 Windows 工具链或 GitHub Windows runner。
- W1：第一部分曾在 WSL 运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`，4/4 通过。`sortOrder` 与拖拽新增实现尚未执行集中验证；待测清单见 [WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md#w1-集中验证清单实现后执行)。
- SSH/SFTP 生命周期：WSL 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`，9/9 通过（含清理使在途 SFTP 建连失效的回归测试）。该测试不覆盖 WPF、SSH.NET 或真实 Dropbear。
- SSH/SFTP 最新可靠性修复：`84d5fa4` 已完成失败 `SftpClient` 的即时 Dispose、在途后端探测的取消/等待和有限重连退避；`git diff --check` 与 WSL 纯逻辑测试 9/9 通过。仍须 GitHub Windows WPF 编译及真实 OpenWrt 验证。
- Windows 构建修复：GitHub Windows 构建日志在 W2 传输代码出现 15 条编译错误诊断，归结为三个根因：`uint` 条件表达式、`NotSupportedException` 命名空间、以及将 `SftpPanel` 误作 `MessageBox` 的 `Window` owner；已由 `de066bf` 最小修复。WSL 回归测试 9/9 通过，仍须手动触发 `dev` 的 Windows CI 完成 WPF 编译、publish 和安装包验证。
- GitHub Actions run `38731668452`（`dev` 的 `a25dfd8`）的 Windows job 已通过 Restore、Test、self-contained Publish、资源检查、zip、Inno Setup 与 artifact 上传；该 run 早于本次“编辑器下载生命周期”工作区修复，因此不能作为后者的编译证据。
- 备份恢复修复：已新增 `QuickCommandStore.MergeFromBackup` 的持久化回归测试；WSL 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`，10/10 通过。Windows WPF 构建及 WebDAV/本地导入手工验证待集中执行；`git diff --check` 已通过。
- W5 远端新建文件：已新增纯逻辑名称校验测试；WSL 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`，20/20 通过，`git diff --check` 通过。`dotnet build win/PixShell.csproj -c Release --no-restore` 未能开始编译，因为当前 Linux SDK 缺少 `Microsoft.NET.Sdk.WindowsDesktop` targets；Windows WPF 编译和三后端手工验收仍待集中执行。
- D-015 递归删除：WSL 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`，20/20 通过，`git diff --check` 通过。该纯逻辑测试项目不覆盖 WPF 对话框、真实 SSH.NET/Proc/SCP 递归删除，三后端的空/非空目录与取消路径待 Windows 手工验证。
- Windows 0.2.0：GitHub Actions `40878384519`（`dev` 的 `6e14dd9`）Windows job 的测试、self-contained publish、zip、Inno Setup 和 artifact 上传全部通过；macOS 和 Release 任务跳过。产物为 `PixShell-0.2.0-win-x64`；Windows 安装及实机功能验收仍待完成。
- GitHub Actions run `38904010347`（`dev` 的 `6a8d625`）中 Windows `win-x64` job 已通过 Restore、10 项测试、self-contained publish、资源检查、zip、Inno Setup、artifact 上传；工作流整体失败仅因两个 macOS job 均在既有的 Swift 包解析步骤失败，Windows 交付链本身通过。
- 编辑器后立即重连补强：已运行 `dotnet vstest win/tests/bin/Release/net9.0/PixShell.Tests.dll --logger:"console;verbosity=normal"`，9/9 通过；`git diff --check` 通过。该程序集不覆盖 WPF UI、SSH.NET 实际 SFTP 下载或真实 Dropbear；本 Linux 环境仍不能编译 WPF。

## Blockers

- Linux 环境缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，且本机 WSL 的 Windows 互操作不可执行 `powershell.exe`，不能执行完整 Windows 应用构建。Windows F0 已由 GitHub Windows runner 验证；macOS 两个 job 在 Swift 包解析阶段失败，须作为独立跨平台 CI 问题处理。

## Next Action

在 Windows 安装 `40878384519` 的 0.2.0 artifact，核验程序版本、更新检查及既定 Windows 集中验收；尚未发布 `0.2.0`。Windows 专属 tag 发布及 main 自动构建尚未实测，macOS 相关未提交修改不纳入当前 Windows 工作。
