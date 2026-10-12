# 当前开发计划

## Current Phase

**Windows FinalShell 工作流对齐：先建立 F0 质量与交付基线，再实施 W0/W1。**

历史原生化路线保留在 [mac/docs/BLUEPRINT.md](../mac/docs/BLUEPRINT.md) 与 [win/BLUEPRINT.md](../win/BLUEPRINT.md)。其 P0–P4 状态是早期计划，不作为当前实时进度；当前阶段以代码、Git 和 [HANDOFF.md](HANDOFF.md) 为准。

## Goal

在不改动 macOS 和既有 SSH/SFTP/Agent 架构的前提下，先建立可在 GitHub 构建 Windows 安装产物的质量门与 `dev` 集成流程，再完成 Windows 端已有远端编辑工作流的验收改进和快捷命令同组排序。详细范围和后续决策门见 [WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md](WINDOWS_FINALSHELL_ALIGNMENT_CONTRACT.md)。

## Completed

- 双端原生 SSH/SFTP 客户端、会话、SFTP、代理、监控、编辑、备份与本机 Agent/CLI 桥已有实现。
- 最近一轮代码工作集中完成 Agent 无头隔离、并发 SSH 复用、死会话自动重连和输出洪峰卡顿修复（见最近 Git 提交）。
- 本轮已建立五份核心协作文档及 Session Start/Handoff 协议。
- 已完成 Windows 与 FinalShell 工作流差异审查，并固化 Windows 对齐改造契约。

## In Progress

- F0：`origin/dev` 已建立，最小快捷命令自动化测试及 CI 触发策略已提交并推送到 `dev`；待恢复 GitHub workflow 登记、验证 `dev` Windows 产物，并经用户批准后以 PR 合入 `main` 使 `main` 自动 CI 生效。

## Remaining

- 手动触发 `dev` 的 GitHub build，并确认 Windows 测试、zip 与 setup artifact；在 Handoff 记录运行链接和结果。
- F0 云端验证通过后，等待用户明确批准 `dev → main` 的 PR；不得直接推送或合并 `main`。
- 完成 F0 后实施 W0：验收 Windows 端既有远端文件双击—弹窗编辑—保存回传闭环，并仅修复发现的问题。
- 完成 W0 后实施 W1：快捷命令同组“上移/下移”与向后兼容的持久化排序。
- W2–W4（传输可靠性、隧道、效率与监控增强）保持 Proposed；未逐项确认前不得实施。
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

## Blockers

- Linux 环境缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，不能执行完整 Windows 应用构建。当前仓库是 `hehudieyiqi/pixshell` 的 fork：上游 Actions API 登记 4 个 active workflow，而本 fork 在默认分支含有同样的 workflow 文件时仍返回 0；须先完成 fork 的 Actions 启用/初始化。

## Next Action

在 fork 的 Actions 页面启用 workflows（而非只设置 Actions permissions）；`build` 出现后手动以 ref `dev` 运行，确认 Windows 测试、zip 与 setup artifact，并将运行链接/结果写回 Handoff。若启用后仍为 0，需用户明确批准 `dev → main` PR 以让 GitHub 重新索引默认分支；不得直接推送 `main`。验证通过后，在 Windows 工具链环境执行 W0，再实施 W1。W2–W4 必须先获得逐项确认。
