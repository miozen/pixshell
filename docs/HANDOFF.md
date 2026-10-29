# 开发接力状态

- **Last Updated**: 2026-10-29（Windows 0.2.0 dev 手动构建通过；待实机验收）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `6e14dd9` — `feat(win): isolate build and release workflow`，已推送到 `origin/dev`。
- **Working Tree**: **DIRTY** — 仅先前未验证的 macOS 和共享版本检查脚本改动保留本机；Windows 代码已提交推送。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c/W5 实现完成，等待集中验证；SSH/SFTP 重连补强已完成但本轮不再作为 PixShell 独有故障推进；Windows 备份完整恢复待 Windows 复核；W3 已由 D-012 排除。
- **Current Task**: 按用户最新要求仅完成 Windows 独立版本、更新与构建验证；macOS 工作区改动保留，暂不交付。
- **Last Completed**: `dev` 的 `6e14dd9` 手动 Windows build run `40878384519` 成功；Windows 测试、self-contained publish、zip、Inno Setup 与 artifact 上传全部通过，macOS 和 Release 按选择跳过。
- **Uncommitted Work**: macOS 的两个旧工作流、更新检查、打包脚本及共享版本检查脚本为先前未验证修改，保留本机、不纳入 Windows 提交。
- **Validation Performed**: GitHub Actions run `40878384519` 的 Windows job 全部通过，产物 `PixShell-0.2.0-win-x64` 已上传；WSL 纯逻辑单元测试 22/22 通过。构建不等于安装及 UI 验收。
- **Validation Pending**: 下载并安装 0.2.0 后验证应用版本和更新检查；D-015 在 SSH.NET、Proc SFTP、SCP 三后端的空目录、非空目录确认/取消、权限失败和目录链接手工验收；其余备份恢复、W0 上传失败路径、W1/W2/W4/W5 UI 集中验收。Windows tag 发布及 main 自动构建尚未实测。
- **Known Issues / Blockers**: WSL 缺少 WPF targets，不能在本机编译 Windows UI；应在 Windows 或按需手动的 GitHub `dev` build 验证。OpenWrt/Dropbear 的间歇性 pre-banner 关闭未定位到单方根因，且当前证据显示会同时影响 FinalShell。SSH.NET `2025.1.0` 安全公告及既有编译警告仍待独立审查。
- **Exact Next Action**: 下载 `40878384519` 的 Windows 0.2.0 artifact，在 Windows 安装并验证关于页、更新检查、非空目录删除及既定集中验收项；记录结果。保留的 macOS 修改须另行处理，禁止误提交。
- **Cross-machine Ready**: **NO** — 先前 macOS 相关修改仍未提交、不可随 Git 跨电脑同步；Windows 已推送检查点可单独恢复。
