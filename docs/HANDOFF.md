# 开发接力状态

- **Last Updated**: 2026-10-14 (已修复 GitHub Windows 构建中的 W2 SFTP 编译错误，等待手动 `dev` CI 复核)
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `de066bf` — `fix: resolve Windows SFTP build errors`。该提交修复 GitHub Windows 日志中归结为三个根因的 15 条 W2 编译错误诊断；本状态提交尚未创建。
- **Working Tree**: **DIRTY** — `docs/CURRENT_PLAN.md` 与 `docs/HANDOFF.md` 待提交，用于记录构建修复的真实恢复状态。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连最小修复已实现，等待真实设备验证；W3 已由 D-012 排除。
- **Current Task**: 对 `de066bf` 进行 GitHub Windows WPF 编译、publish 和安装包复核，然后继续集中验收。
- **Last Completed**: 已提交 Windows SFTP 构建最小修复：`ProcSftpClient` 的 SFTP 标志使用 `uint`，`ScpAdapter` 引入 `System`，三个冲突提示以宿主 `Window` 作为 `MessageBox` owner。未改变 W2 的传输和冲突保护语义。
- **Uncommitted Work**: `docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 本次状态更新待提交；无业务代码未提交。
- **Validation Performed**: `git diff --check` 通过。WSL .NET SDK 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：9/9 通过。GitHub Windows 日志中归结为三个根因的 15 条编译错误诊断已逐项通过源码签名核对修复；此环境不能编译 WPF 或运行真实 SSH 服务端。
- **Validation Pending**: 手动运行 GitHub `build` 工作流，ref 必须为包含 `de066bf` 的 `dev`，确认 Windows Test、publish、zip、setup 和 artifact。其后待 Windows + OpenWrt 重连重复测试并回归 Alpine，以及 W1/W2/W4 Windows UI 验收、W0 上传失败保留脏内容补测；macOS Swift package 解析失败独立诊断。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 在 GitHub Actions 手动运行 `build`，选择 `dev`（应包含 `de066bf`）；检查 Windows job 的 Test、Publish self-contained、zip、Inno Setup 和 artifact 是否全部通过，并提供 run 链接或日志。
- **Cross-machine Ready**: **YES** — `de066bf`、构建修复状态提交及错误数量更正 `feaa048` 均已推送到 `origin/dev`；接收端应从本文件的 Exact Next Action 开始。
