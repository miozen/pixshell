# 开发接力状态

- **Last Updated**: 2026-10-17（编辑器打开后立即重连的补强已推送；等待 Windows CI 与 OpenWrt 实机复核）
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Code Commit**: `28051a9` — `fix(win): serialize SSH reconnect lifecycle`，已推送到 `origin/dev`。
- **Working Tree**: 将在本接力文档提交后保持 **CLEAN**；无业务代码或待提交文档。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 完成；W0 主路径验收通过、失败路径暂缓；W1/W2a–W2c/W4a–W4c 实现完成，等待集中验证；SSH/SFTP 立即重连与 Windows 备份完整恢复均已实现，等待真实设备/Windows 复核；W3 已由 D-012 排除。
- **Current Task**: 取得 `28051a9` 的 Windows CI 与 OpenWrt 实机复核证据。
- **Last Completed**: 审查用户提供的 2026-10-17 PixShell 日志：打开 `/etc/shinra/main-profile.json` 成功后，SFTP 于 08:32:44 释放；约 1.5 秒后新 SSH 握手连续三次在版本交换前被关闭。日志实际走 SSH.NET SFTP，未出现 `exec sftp`/SCP 回退。已提交并推送 `28051a9`：关闭标签、新建会话、手动断开、原地重连及密码重试共用同一生命周期门；编辑器下载改为 SSH.NET 原生可取消异步 API，SFTP 关闭增加起止日志。
- **Uncommitted Work**: 无（本接力文档提交后）。
- **Validation Performed**: `git diff --check` 通过。通过 `dotnet vstest win/tests/bin/Release/net9.0/PixShell.Tests.dll --logger:"console;verbosity=normal"` 实际执行 9/9 通过；常规 `dotnet test ... --no-restore` 退出码为 0 但本环境未输出测试汇总，故以上述 vstest 结果为准。历史 GitHub Actions run `38904010347` 的 Windows job 通过，但早于本次补强，不能作为本次 Windows 编译证据。
- **Validation Pending**: 在 GitHub Windows runner 编译本次补强；在 Windows + OpenWrt 重复“连接 → SFTP 打开未修改文件 → 断开 → 立即重连”，收集 PixShell `SFTP client 关闭开始/完成`、SSH 建连日志及同时间段 `logread -e dropbear`。随后回归 Alpine，并按既有计划完成备份恢复、W0/W1/W2/W4 集中验收。macOS 两个 job 的 Swift 包解析失败仍是独立问题。
- **Known Issues / Blockers**: SSH.NET `2025.1.0` 在 restore 时报告两个高危安全公告；这是 warning 而非本次编译失败，必须另立依赖升级兼容性审查，不能在构建修复中盲目升级。现有多个 CS4014、CS8602 和 CS0414 警告同样不阻断构建，待集中验收后按风险单独处理。WSL 缺少 WPF targets 且 Windows 互操作不可执行 `powershell.exe`；`gh` CLI 的 HTTPS API token 已失效，不能从此环境手动触发工作流。其余历史风险见当前计划。
- **Exact Next Action**: 手动触发包含 `28051a9` 的 Windows `build`；随后在 Windows + OpenWrt 运行复现步骤并附上 PixShell 和 Dropbear 日志。
- **Cross-machine Ready**: **YES** — 本接力状态提交并推送后，代码与文档可跨机器恢复；Windows CI 与实机验收仍待完成。
