# 开发接力状态

- **Last Updated**: 2026-10-12 (首次 main CI 已运行；Windows publish 修复待 dev 验证)
- **Current Branch**: `main`（跟踪 `origin/main`；修复将转入 `dev`）
- **HEAD / Last Commit**: `8ee9223` — `merge: establish F0 delivery baseline`（已推送 `main`）
- **Working Tree**: **DIRTY** — `win/PixShell.csproj` 和本计划/Handoff 更新待转入 `dev` 提交。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 的首次 main CI 已触发，Windows 测试通过但 publish 失败；修复待 dev 手动 CI 验证；随后 W0/W1；W2–W4 为 Proposed。
- **Current Task**: 将生产项目排除 `tests/**` 的修复推送到 `dev`，并手动验证 Windows publish/artifact。
- **Last Completed**: 已完成 `dev → main` 的用户批准合并，恢复 fork Actions workflow 登记；自动 build run `38252076365` 已启动。Windows `dotnet test` 通过；publish 发现测试源文件被生产项目默认通配符编译，已在本地修复。
- **Uncommitted Work**: `win/PixShell.csproj`、`docs/CURRENT_PLAN.md`、`docs/HANDOFF.md`。
- **Validation Performed**: `main` 已推送且工作流已登记 4 项。CI run `38252076365`：Windows restore/test 成功，publish 失败；两项 macOS job 在 Resolve Swift packages 失败。WSL .NET SDK 9.0.121 已重新运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release --no-restore`：3/3 通过；`git diff --check` 通过。
- **Validation Pending**: 将 Windows publish 修复推送到 `dev` 后，手动触发 `build`（ref `dev`）验证 Windows publish、zip/setup artifact；完整 WPF Windows 构建；macOS Swift package 解析失败的独立诊断；后续 W0 手工验收和 W1 专属验证。
- **Known Issues / Blockers**: `main` 的首次 CI 已恢复 fork Actions，但 run `38252076365` 整体失败：Windows publish 受 `tests/**` 被错误编入生产项目影响，修复尚未在 CI 验证；macOS arm64/x64 均在 Swift 包解析阶段失败。Linux SDK 缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，不能构建完整 Windows 应用。`gh` CLI 的 HTTPS API token 已失效，不能从此环境使用 `gh workflow run`；Git SSH 443 推送正常。README/部分 CI 默认版本滞后；macOS 内置 NIO SSH/SFTP 当前接受所有主机密钥。W2–W4 未获逐项确认，不得实施。
- **Exact Next Action**: 切换到 `dev`，提交并推送 `win/PixShell.csproj` 的 `tests/**` 编译排除修复；随后在 GitHub Actions 手动运行 `build`（ref `dev`），确认 Windows Test、Publish、zip 和 setup artifact。通过后再请求用户批准下一次 `dev → main` 合并。
- **Cross-machine Ready**: **NO** — 当前修复尚未提交/推送到 `dev`。
