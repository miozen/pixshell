# 开发接力状态

- **Last Updated**: 2026-10-16（Dropbear SSH/SFTP 重连可靠性修复已提交，待 CI 与实机复核）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `84d5fa4` — `fix: serialize Dropbear SSH reconnect lifecycle`。本状态提交尚未创建。
- **Working Tree**: **DIRTY** — `docs/DECISIONS.md`、`docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 待提交，用于记录本次修复的真实状态；无业务代码未提交。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连最小修复已实现，等待真实设备验证；W3 已由 D-012 排除。
- **Current Task**: 验证 SFTP 探测取消/等待、失败客户端释放和有限退避后，Windows + OpenWrt 的立即断开/重连稳定性。
- **Last Completed**: 已提交 `84d5fa4`：失败的 `SftpClient.Connect()` 立即 Dispose；断开/重连取消并等待在途 SFTP 后端探测与编辑器下载；忽略重复重连请求；仅对 pre-banner/TCP reset/abort 进行两次短退避重试（总共最多三次尝试）。
- **Uncommitted Work**: `docs/DECISIONS.md`、`docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 本次状态更新待提交；无业务代码未提交。
- **Validation Performed**: `git diff --check` 通过。WSL .NET SDK 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：9/9 通过。GitHub run `38731668452` 的 Windows job 已通过，但早于 `84d5fa4`；此环境不能编译 WPF 或运行真实 SSH 服务端。
- **Validation Pending**: 手动运行 GitHub `build`（ref 必须包含 `84d5fa4`）确认 Windows Test、publish、zip、setup 和 artifact；然后在 Windows + OpenWrt 复现“打开未修改文件后立即断开并重连”，并回归 Alpine。后续仍待 W1/W2/W4 Windows UI 验收、W0 上传失败保留脏内容补测和 macOS Swift package 解析失败诊断。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 提交本状态检查点并推送；随后手动运行 GitHub `build`，选择包含 `84d5fa4` 的 `dev`，并在 Windows + OpenWrt 执行“打开文件后立即断开并重连”复测。
- **Cross-machine Ready**: **YES** — `84d5fa4` 与状态提交 `b67810e` 已推送到 `origin/dev`；接收端应从本文件的 Exact Next Action 开始。
