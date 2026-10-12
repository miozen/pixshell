# 开发接力状态

- **Last Updated**: 2026-10-12 (F0 纯逻辑测试已在 WSL 通过；GitHub workflow 登记异常待处理)
- **Current Branch**: `dev`（跟踪 `origin/dev`）
- **HEAD / Last Commit**: `dev` 与 `origin/dev` 在本状态更新提交并推送后应保持一致；F0 基线提交为 `7a90400`。
- **Working Tree**: **CLEAN** — 本次测试基线与状态更新提交并推送后应无未提交修改。
- **Current Phase**: Windows FinalShell 工作流对齐：F0 纯逻辑测试已通过，待 GitHub Windows 验证；随后 W0/W1；W2–W4 为 Proposed。
- **Current Task**: 解决 GitHub 未登记 workflow 的仓库级异常，再手动触发并记录 `dev` 的 F0 GitHub build。
- **Last Completed**: 已建立并推送 `origin/dev`；`origin` 已改用 GitHub SSH 443；为 `QuickCommandStore` 增加独立目录构造入口及最小 xUnit 回归测试；测试项目已与 WPF 应用解耦，可在 WSL 执行；加入 `win/build.ps1 -Action test`；CI 增加 `main` push 自动触发和 Windows `dotnet test`，未增加 `dev` 自动触发。
- **Uncommitted Work**: `.gitignore`、`.github/workflows/build.yml`、`win/build.ps1`、`win/tests/PixShell.Tests.csproj`、`win/tests/TestDoubles.cs` 及本状态/计划更新，均待提交。
- **Validation Performed**: 已核对 Git 分支、HEAD、最近提交、CI 触发定义、测试入口与源码调用兼容性；五份标准文件均存在；F0 提交前 `git diff --check` 通过。`ssh -T -p 443 git@ssh.github.com` 已认证为 `miozen`，`git ls-remote` 与 `git push -u origin dev` 已成功。WSL .NET SDK 9.0.121 已运行 `dotnet test win/tests/PixShell.Tests.csproj -c Release`：3/3 通过。
- **Validation Pending**: GitHub Actions workflow 登记恢复后，手动触发 `dev` 的 GitHub build，确认 Windows `dotnet test`、zip/setup artifact；完整 WPF Windows 构建；后续 W0 手工验收和 W1 专属验证。
- **Known Issues / Blockers**: Linux SDK 缺少 WPF `Microsoft.NET.Sdk.WindowsDesktop` targets，不能构建完整 Windows 应用；GitHub `main` 虽包含 4 个 workflow 文件，但公开 Actions API 返回 `total_count: 0`，Actions 页面也未列出 workflow，须由仓库/组织设置或 GitHub Support 处理。`gh` CLI 的 HTTPS API token 已失效，不能从此环境使用 `gh workflow run`；Git SSH 443 推送正常。README/部分 CI 默认版本滞后；macOS 内置 NIO SSH/SFTP 当前接受所有主机密钥。W2–W4 未获逐项确认，不得实施。
- **Exact Next Action**: 恢复 GitHub 对 `main` 中 workflow 的登记（确认仓库/组织 Actions 设置；若仍为 0，向 GitHub Support 提供 API 结果）；`build` 出现后手动以 ref `dev` 运行，确认 Windows 测试、zip 与 setup artifact，并将结果写回本文件。验证通过后开始 W0。
- **Cross-machine Ready**: **YES** — 本状态更新提交并推送后，接收端可 fetch `origin/dev` 并从 Exact Next Action 恢复。
