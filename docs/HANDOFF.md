# 开发接力状态

- **Last Updated**: 2026-10-15（编辑器下载生命周期修复已推送，待 CI 与实机复核）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `119358e` — `fix(win): await editor SFTP cleanup before reconnect`，已推送至 `origin/dev`。
- **Working Tree**: 本文档状态检查点待提交；代码修复本身已提交并推送。提交本状态检查点后应为 clean。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连最小修复已实现，等待真实设备验证；W3 已由 D-012 排除。
- **Current Task**: 验证编辑器双击下载期间立即断开/重连的 Windows + OpenWrt 生命周期修复。
- **Last Completed**: 已将编辑器后台下载的取消/收尾等待修复推送为 `119358e`：`win/SftpPanel.xaml.cs` 在断开时取消并等待下载任务，`win/MainWindow.xaml.cs` 在断开、重连和密码重试前等待 SFTP 收尾，避免旧独立 SFTP 通道与新 SSH 握手并发。
- **Uncommitted Work**: 仅本次状态检查点文档更新；不含业务代码。
- **Validation Performed**: `git diff --check` 通过。WSL .NET SDK 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：9/9 通过。GitHub run `38731668452` 的 Windows job 已通过，但它早于本次工作区修复；此环境不能编译 WPF 或运行真实 SSH 服务端。
- **Validation Pending**: 手动运行 GitHub `build`（ref 必须包含 `119358e`）确认 Windows Test、publish、zip、setup 和 artifact；然后在 Windows + OpenWrt 复现“打开未修改文件后立即断开并重连”，并回归 Alpine。后续仍待 W1/W2/W4 Windows UI 验收、W0 上传失败保留脏内容补测和 macOS Swift package 解析失败诊断。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 提交本状态检查点并推送；随后手动运行 GitHub `build`，选择包含 `119358e` 的 `dev`，并在 Windows + OpenWrt 执行“打开文件后立即断开并重连”复测。
- **Cross-machine Ready**: **YES**（提交本状态检查点后）— 代码修复已在 `origin/dev`；接收端从 `dev` 恢复即可。
