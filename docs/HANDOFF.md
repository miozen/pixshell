# 开发接力状态

- **Last Updated**: 2026-10-24（D-015 已推送 `origin/dev`；等待 Windows 集中验收）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: 当前 `HEAD` — `fix(win): confirm recursive remote directory deletion` 已推送到 `origin/dev`；以 `git rev-parse --short HEAD` 获取可验证提交号。
- **Working Tree**: **CLEAN** — D-015 源码与计划/决策/契约文档均已提交。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c/W5 实现完成，等待集中验证；SSH/SFTP 重连补强已完成但本轮不再作为 PixShell 独有故障推进；Windows 备份完整恢复待 Windows 复核；W3 已由 D-012 排除。
- **Current Task**: 执行当前 `dev` 的剩余 Windows 集中手工验收。
- **Last Completed**: D-015 已实现：删除前检测选中目录是否非空；非空时显示风险并要求第二次确认，确认后逐层删除子项再删除目录。空目录直接删除；目录列举失败或用户取消不会删除任何项。SSH.NET 文件列表补齐符号链接标记，递归删除不跟随目录链接。
- **Uncommitted Work**: 无。
- **Validation Performed**: `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore` 20/20 通过；`git diff --check` 通过。当前 Linux SDK 缺少 `Microsoft.NET.Sdk.WindowsDesktop` targets，未能编译 WPF。
- **Validation Pending**: Windows WPF 编译/安装包；D-015 在 SSH.NET、Proc SFTP、SCP 三后端的空目录、非空目录确认/取消、权限失败和目录链接手工验收；其余备份恢复、W0 上传失败路径、W1/W2/W4/W5 UI 集中验收。未来重开 SSH 调查前，必须先证明同一时刻 FinalShell 正常而 PixShell 失败，并采集 Dropbear、conntrack/防火墙及代理路径证据。
- **Known Issues / Blockers**: WSL 缺少 WPF targets，不能在本机编译 Windows UI；应在 Windows 或按需手动的 GitHub `dev` build 验证。OpenWrt/Dropbear 的间歇性 pre-banner 关闭未定位到单方根因，且当前证据显示会同时影响 FinalShell。SSH.NET `2025.1.0` 安全公告及既有编译警告仍待独立审查。
- **Exact Next Action**: 在 Windows 对目录删除的空/非空/取消路径及 W1/W2/W4/W5、备份恢复和 W0 暂缓项执行集中手工验收；逐项记录通过、失败或暂缓原因。
- **Cross-machine Ready**: **YES** — D-015 已提交并推送；接收端按 Session Start 核对 `origin/dev` 后可继续。
