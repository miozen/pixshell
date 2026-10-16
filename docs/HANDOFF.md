# 开发接力状态

- **Last Updated**: 2026-10-16（当前修复已推送；Windows CI 交付链通过，等待 Windows 实机集中验收）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `6a8d625` — `fix(win): restore backup commands and settings`，已推送到 `origin/dev`。
- **Working Tree**: **DIRTY** — 仅本文件和 `docs/CURRENT_PLAN.md` 的 CI 结果记录待提交；无业务代码未提交。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连与 Windows 备份完整恢复均已实现，等待真实设备/Windows 复核；W3 已由 D-012 排除。
- **Current Task**: 执行 Windows 实机集中验收，并根据结果处理失败项或完成切片验收。
- **Last Completed**: `6a8d625` 已推送；本地/WebDAV 恢复现合并主机、快捷命令和可识别设置，并新增持久化回归测试。GitHub Actions run `38904010347` 的 Windows job 已完成 Test、self-contained publish、zip、Inno Setup 和 artifact 上传。此前 `84d5fa4` Dropbear 重连修复保持不变。
- **Uncommitted Work**: `docs/CURRENT_PLAN.md`、`docs/HANDOFF.md` 的本次 CI 结果记录待提交；无业务代码未提交。
- **Validation Performed**: WSL `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：10/10 通过（包含本次备份命令合并回归）；`git diff --check` 通过。GitHub Actions run `38904010347` 的 Windows `win-x64` job 成功完成 Restore、Test、publish、资源检查、zip、Inno Setup 和 artifact 上传。
- **Validation Pending**: 在 Windows 上验证本地 v1 导入与 WebDAV 恢复的命令/设置恢复、OpenWrt 立即重连并回归 Alpine；完成 W1/W2/W4 Windows UI 验收、W0 上传失败保留脏内容补测。macOS 两个 job 在既有 Swift 包解析步骤失败，仍须独立诊断。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 提交并推送本次 CI 结果记录；随后下载 run `38904010347` 的 Windows artifact，在 Windows 上执行备份恢复、OpenWrt/Alpine 重连及 W0/W1/W2/W4 集中手工验收，并逐项记录结果。
- **Cross-machine Ready**: **NO** — 仅 CI 结果文档尚未提交；提交并推送后即可恢复为 YES。
