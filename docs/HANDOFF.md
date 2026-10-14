# 开发接力状态

- **Last Updated**: 2026-10-14 (W3 已由 D-012 排除；全部已确认的 Windows W 实现事项已完成，等待集中验证)
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Commit**: `2a78445` — `docs: exclude W3 tunnel management scope`。最后的代码提交仍为 `20ec753` — `fix: release SFTP before Windows SSH reconnect`；本状态提交尚未创建。
- **Working Tree**: **DIRTY** — 仅 `docs/HANDOFF.md` 待提交，用于记录 W3 排除后的真实恢复状态。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连最小修复已实现，等待真实设备验证；W3 已由 D-012 排除。
- **Current Task**: 已确认 Windows W 功能实现完成，准备进行集中验证；优先记录 OpenWrt Dropbear 立即重连修复的真实设备证据。
- **Last Completed**: 已提交 SSH/SFTP 生命周期最小修复：活动会话在断开、原地重连、密码重试和关闭前先释放 SFTP；清理使在途异步 SFTP 建连失效；已重连的会话忽略旧断连通知；版本交换前被远端关闭时记录不含凭据的 SSH 诊断日志。决策记录为 D-011。
- **Uncommitted Work**: `docs/HANDOFF.md` 本次状态更新待提交；无业务代码未提交。
- **Validation Performed**: `git diff --check` 通过。WSL .NET SDK 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：9/9 通过，含 SFTP 清理使在途连接失效的回归测试。此测试不运行 WPF、SSH.NET 或真实 SSH 服务端。
- **Validation Pending**: 在 Windows + OpenWrt 上至少连续重复“连接 → 经 SFTP 打开未修改文件 → 断开 → 立即重连”并收集 PixShell `ssh`/`sftp` 日志及 OpenWrt `logread`；回归 Alpine。其后仍待 W1 自动化（含 `sortOrder`、拖拽重排和跨组拒绝）、W2 传输与冲突保护、W4a/W4b/W4c、Windows WPF 编译/发布、Windows 实机交互及按需手动 `dev` GitHub build；W0 上传失败保留脏内容补测；macOS Swift package 解析失败独立诊断。
- **Known Issues / Blockers**: 尚无真实 OpenWrt 的修复验证证据，不能把“版本交换前关闭”单方归因为 PixShell 或 Dropbear。W0 的上传失败保留编辑内容尚未测试，按用户指示暂缓。两个跨平台 build run 均因 macOS arm64/x64 在 Resolve Swift packages 失败而整体标红；Windows job 与 artifact 均成功。Linux SDK 缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，不能构建完整 Windows 应用。`gh` CLI 的 HTTPS API token 已失效，不能从此环境使用 `gh workflow run`；Git SSH 443 推送正常。README/部分 CI 默认版本滞后；macOS 内置 NIO SSH/SFTP 当前接受所有主机密钥。
- **Exact Next Action**: 开始集中验证：先在 Windows 机器连接 OpenWrt，连续重复至少 10 次“连接 → SFTP 双击打开一个未修改的小文本文件 → 关闭 SSH → 立即重连”；记录成功/失败次数、PixShell 的 `ssh`/`sftp` 日志及 OpenWrt `logread`，再以同一序列回归 Alpine。
- **Cross-machine Ready**: **YES** — `2a78445` 与状态提交 `9c2d4b8` 已推送到 `origin/dev`；接收端应从本文件的 Exact Next Action 开始。
