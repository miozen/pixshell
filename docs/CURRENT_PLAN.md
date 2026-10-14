# 当前开发计划

## Current Phase

**Windows FinalShell 工作流对齐：F0 已完成；W0 主路径验收通过且失败路径暂缓；W1 已完成；W4a（监控时间窗与网卡选择）正在实施。**

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
- W4a：已确认并实现监控图表时间窗与远端网卡选择；待静态复核和后续集中验证。

## Remaining

- 在适当的可控环境补测 W0 的“上传失败保留编辑内容”路径；该项暂缓不等于验收通过。
- W2、W3 保持 Proposed；W4 按 D-008 分阶段实施，W4b/W4c 未确认前不得开始。
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

## Blockers

- Linux 环境缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，不能执行完整 Windows 应用构建。Windows F0 已由 GitHub Windows runner 验证；macOS 两个 job 在 Swift 包解析阶段失败，须作为独立跨平台 CI 问题处理。

## Next Action

复核 W4a diff 并形成 `dev` 检查点；整个已确认 Windows W 路线完成后再集中执行 W0/W1/W4a 的自动化、Windows UI、发布和 CI 验收。下一功能切片为 W4b 命令建议；W4c 在另行确认批量任务语义前不得开始。
