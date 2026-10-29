# 开发接力状态

- **Last Updated**: 2026-10-29（独立平台构建进行中；尚未完成）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `e6bd144` — `chore: align product versions and update repository`，已推送到 `origin/dev`。
- **Working Tree**: **DIRTY** — Windows 检查点及既有未验证 macOS 工作区改动均尚待处理；提交 Windows 检查点后 macOS 改动仍保留本机。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c/W5 实现完成，等待集中验证；SSH/SFTP 重连补强已完成但本轮不再作为 PixShell 独有故障推进；Windows 备份完整恢复待 Windows 复核；W3 已由 D-012 排除。
- **Current Task**: 按用户最新要求仅完成 Windows 独立版本、更新与构建验证；macOS 工作区改动保留，暂不交付。
- **Last Completed**: D-015 已实现：删除前检测选中目录是否非空；非空时显示风险并要求第二次确认，确认后逐层删除子项再删除目录。空目录直接删除；目录列举失败或用户取消不会删除任何项。SSH.NET 文件列表补齐符号链接标记，递归删除不跟随目录链接。
- **Uncommitted Work**: Windows 工作流、更新检查和测试待提交；macOS 的两个旧工作流、更新检查、打包脚本及共享版本检查脚本为先前未验证修改，保留本机、不纳入 Windows 提交。
- **Validation Performed**: `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore` 22/22 通过，包含 Windows 更新筛选中的其他平台、旧 tag、草稿及双页读取；版本一致性、shell 语法、YAML 平台选项/条件和发布前 main 祖先检查的静态断言、`git diff --check` 通过。真实 HTTP、macOS 筛选执行、实际 Actions 选择和产物未验证；当前环境没有 WindowsDesktop targets 或 Swift 编译器。
- **Validation Pending**: Windows WPF 编译/安装包；D-015 在 SSH.NET、Proc SFTP、SCP 三后端的空目录、非空目录确认/取消、权限失败和目录链接手工验收；其余备份恢复、W0 上传失败路径、W1/W2/W4/W5 UI 集中验收。未来重开 SSH 调查前，必须先证明同一时刻 FinalShell 正常而 PixShell 失败，并采集 Dropbear、conntrack/防火墙及代理路径证据。
- **Known Issues / Blockers**: WSL 缺少 WPF targets，不能在本机编译 Windows UI；应在 Windows 或按需手动的 GitHub `dev` build 验证。OpenWrt/Dropbear 的间歇性 pre-banner 关闭未定位到单方根因，且当前证据显示会同时影响 FinalShell。SSH.NET `2025.1.0` 安全公告及既有编译警告仍待独立审查。
- **Exact Next Action**: 按需手动选择 Windows 构建并验证安装包与应用更新检查。保留的 macOS 修改须另行处理，禁止误提交；检查远端和本地状态后再继续。
- **Cross-machine Ready**: **NO** — 平台选择尚未完成验证、提交或推送。
