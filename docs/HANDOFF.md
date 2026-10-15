# 开发接力状态

- **Last Updated**: 2026-10-15 (根据 OpenWrt 实机复现补充编辑器下载生命周期修复，待 CI 与实机复核)
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `a25dfd8` — `docs: mark build diagnosis synchronized`；工作区含未提交的 Windows 编辑器下载生命周期修复。
- **Working Tree**: **DIRTY** — `win/SftpPanel.xaml.cs`、`win/MainWindow.xaml.cs`、`docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 待验证、提交并推送到 `dev`。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连最小修复已实现，等待真实设备验证；W3 已由 D-012 排除。
- **Current Task**: 验证编辑器双击下载期间立即断开/重连的 Windows + OpenWrt 生命周期修复。
- **Last Completed**: GitHub Actions run `38731668452`（ref `dev`，`a25dfd8`）的 Windows job 已通过 Test、self-contained Publish、资源检查、zip、Inno Setup 和 artifact 上传；本地已补充编辑器下载取消与断开前等待收尾，防止旧独立 SFTP 连接与重连握手并发。
- **Uncommitted Work**: `win/SftpPanel.xaml.cs` 增加编辑器下载取消/收尾等待；`win/MainWindow.xaml.cs` 在断开、重连和密码重试前 await SFTP 收尾；本文档与计划同步记录。
- **Validation Performed**: `git diff --check` 通过。WSL .NET SDK 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：9/9 通过。GitHub run `38731668452` 的 Windows job 已通过，但它早于本次工作区修复；此环境不能编译 WPF 或运行真实 SSH 服务端。
- **Validation Pending**: 将本次修复提交并推送后，手动运行 GitHub `build`（ref 必须包含该提交）确认 Windows Test、publish、zip、setup 和 artifact；然后在 Windows + OpenWrt 复现“打开未修改文件后立即断开并重连”，并回归 Alpine。后续仍待 W1/W2/W4 Windows UI 验收、W0 上传失败保留脏内容补测和 macOS Swift package 解析失败诊断。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 检查并提交当前四个已修改文件，推送到 `origin/dev`；随后手动运行 GitHub `build`，选择包含该提交的 `dev`，并在 Windows + OpenWrt 执行“打开文件后立即断开并重连”复测。
- **Cross-machine Ready**: **NO** — 本次编辑器下载生命周期修复尚未提交/推送；接收端不能仅依赖 `origin/dev` 恢复。
