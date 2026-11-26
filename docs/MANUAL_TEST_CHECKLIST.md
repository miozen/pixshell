# Windows 阶段级验收与测试职责清单

本清单按“**自动化单元测试 / 自动化集成测试 / 精简人工 GUI 冒烟测试**”分层整理各阶段验收覆盖项，完整保留缺陷追溯关系与数据安全标准：

- **状态约束**：严格区分 `Passed`、`Not Run`、`Failed` 与 `Environment-Limited`；**绝不将受环境限制未执行的集成测试记为通过，也不为精简人工测试而删减原有缺陷覆盖要求**。
- **数据隔离铁律**：所有自动化测试均运行在临时隔离目录（`%TEMP%\pixshell-itest-*` / WSL `/tmp/pixshell_itest_*`），绝不触碰用户真实 `%APPDATA%\PixShell` 或真实生产主机。

## 阶段构建与环境产物记录（R1）

- **当前阶段**：**R1 阶段（`R1-1`、`R1-2`、`R1-3`）编码、单元测试、隔离本地/WSL 集成验证与最新 Release 发布已完成**
- **最新构建产物**：`Release / net10.0-windows / win-x64`（self-contained），完整路径 `D:\20_Projects\pixshell\win\publish\win-x64\PixShell.exe`（构建时间 `2026/11/26 09:58:17`，`0` 错误；其后仅新增测试与文档，未改动生产源码）
- **自动化回归入口**：
  - 单元测试 + 本地归档集成：`win/build.ps1 -Action test -Configuration Release`（`46/46` 通过，`0` 失败，`0` 跳过，`0` 编译警告）
  - 隔离环境集成脚本：[Invoke-IsolatedIntegrationTests.ps1](file:///d:/20_Projects/pixshell/win/tests/Invoke-IsolatedIntegrationTests.ps1)（含环境探测、`PixShell.Tests` 全量及 WSL2 Ubuntu GNU `tar` / POSIX `chmod` / `ls -la` 隔离检查）
- **本机隔离 SSH/SFTP/SCP 服务端评估结论**：本机未安装 Docker CLI、Windows OpenSSH Server（`sshd.exe` + `sftp-server.exe`）或 WSL `Ubuntu` `openssh-server`（`/usr/sbin/sshd` + `/usr/lib/openssh/sftp-server`）；WSL `Ubuntu` 具备 `/usr/bin/tar`、`/usr/bin/ssh-keygen`、`/usr/bin/python3`。按协议不擅自安装系统级服务端软件，真实协议守护进程集成测试明确标记为 `Environment-Limited`。

---

## 一、R1 自动化单元测试项（xUnit `net10.0`，无外部依赖）

覆盖原 `R1-M1`～`R1-M8` 中全部纯逻辑、编码往返、权限计算、状态机、本地并发锁与异常回滚边界：

| 追溯编号 | 关联缺陷 | 覆盖内容与验证边界 | 对应测试方法（`win/tests/`） | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R1-M1** | `WIN-BUG-029` | 目录刷新乱序返回丢弃、切签/断开使在途快照与操作句柄立即失效 | `SftpEditorAndRefreshTests.OutOfOrderDirectoryRefreshResultIsRejected`<br>`SftpEditorAndRefreshTests.SessionSwitchInvalidatesInFlightRefreshAndOperationSnapshots` | `Passed` |
| **R1-M2** | `WIN-BUG-001A`, `WIN-BUG-001C`, `WIN-BUG-014A` | 文本缩减保存无旧尾字节残留；非原子后端不放宽限制权限；传中中断/大小校验不匹配回滚原文件；双失败暴露 `.bak` 路径；`UTF-8/UTF-16/UTF-32 BOM`、`GB18030`、`Latin1`、`\r\n`/混合换行精确往返；损坏 BOM 与 `\0` 二进制拦截 | `SftpEditorAndRefreshTests.ShorterSaveOverwritesCleanlyAndPreservesPosixPermissions`<br>`SftpEditorAndRefreshTests.AtomicOverwriteBackendUsesSingleStepRenameWithoutBackupWindow`<br>`SftpEditorAndRefreshTests.RestrictedModeRestoreFailureAbortsWithoutWideningPermissionsOrReplacingTarget`<br>`SftpEditorAndRefreshTests.MidUploadFailureAndCancellationLeaveOriginalFileIntactAndCleanTemp`<br>`SftpEditorAndRefreshTests.SizeVerificationMismatchAbortsBeforeTouchingOriginalFile`<br>`SftpEditorAndRefreshTests.SwapRenameFailureRollsBackAndDoubleFailureSurfacesBackupPath`<br>`SftpEditorAndRefreshTests.CreatingNewFileViaAtomicSaveWritesExactBytes`<br>`SftpEditorAndRefreshTests.EncodingRoundTripPreservesUtf8Utf16AndUtf32BomsAndRejectsCorruptBom`<br>`SftpEditorAndRefreshTests.EncodingPreservesGb18030AndLatin1WithoutReplacementCorruption`<br>`SftpEditorAndRefreshTests.EncodingPreservesPureCrlfAndMixedNewlinesAndRejectsBinaryNulFiles` | `Passed` |
| **R1-M3** | `WIN-BUG-001B` | 切至其他会话标签时阻止保存写错主机并保留脏标记，切回原已连接会话可保存成功 | `SftpEditorAndRefreshTests.SaveTargetGuardBlocksInactiveTabButAllowsSaveAfterReturningToOriginalSession` | `Passed` |
| **R1-M4** | `WIN-BUG-003B` | `ExtractStagedCommand` 校验每次 `mv` 退出码、覆盖 `..*` 与点文件且不误判假绿 | `SftpScpAndTransferTests.ExtractStagedCommandChecksMvExitCodeAndHandlesDoubleDotFiles` | `Passed` |
| **R1-M5** | `WIN-BUG-004A`, `WIN-BUG-004B` | ISO/BusyBox/传统 `ls -l` 带空格文件名与软链接解析；错误信息不误判为空目录（防误触发 `D-015`） | `SftpScpAndTransferTests.ScpLsParserParsesIsoBusyBoxAndTraditionalListingsWithSpacesAndSymlinks`<br>`SftpScpAndTransferTests.ScpLsParserDoesNotMisinterpretErrorOutputAsEntries` | `Passed` |
| **R1-M6** | `WIN-BUG-009A` | 非原子 SFTP 直传覆盖回滚与保留目标文件（权限逻辑另见 `RemoteFileOverwrite` 单元测试及 `ChmodWindow` 源码审查） | `SftpScpAndTransferTests.DirectUploadOverwriteSucceedsAcrossNonAtomicSftpAndRollsBackOnFailure` | `Passed` |
| **R1-M7** | `WIN-BUG-011A`, `WIN-BUG-011B` | 合法空数组 `[]` 持久化不被默认值填回；损坏 JSON 生成 `.corrupt.*` 备份并阻止单条写入覆写；多任务并发 `MutateJsonStrict` 命名互斥锁零丢更新 | `LocalTransactionAndTargetTests.QuickCommandStorePreservesIntentionalEmptyArrayAndBacksUpCorruptFile`<br>`LocalTransactionAndTargetTests.MutateJsonStrictRefusesToOverwriteCorruptStoreAndCreatesBackup`<br>`LocalTransactionAndTargetTests.MutateJsonStrictPreservesAllConcurrentReadModifyWriteUpdates` | `Passed` |
| **R1-M8** | `WIN-BUG-015B`, `WIN-BUG-019A` | 跨卷回退递归复制目录与清理源路径；`CommandTargetResolver` 按稳定 `SessionId` 锁定目标、防前序关闭漂移并拦截已断开目标 | `LocalTransactionAndTargetTests.MoveEntryWithCrossVolumeFallbackCopiesNestedDirectoriesAndRemovesSource`<br>`LocalTransactionAndTargetTests.CommandTargetResolverTracksStableSessionIdAndBlocksDisconnectedTarget` | `Passed` |

---

## 二、R1 自动化集成测试与环境受限项（隔离环境）

| 追溯编号 | 关联缺陷 | 隔离环境与验证内容 | 执行方式 / 依赖 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R1-M4 / R1-M8** | `WIN-BUG-003A`, `WIN-BUG-015B` | 调用真实 Windows `tar.exe` 验证 `SftpTransfer.PackLocalAsync` / `ExtractLocalAsync` 对嵌套含空格目录、`.env` 点文件、缺少源文件报错传播，及 `MoveEntryWithCrossVolumeFallback` 跨卷落盘与清理 | `LocalArchiveIntegrationTests`（已并入 `win/tests/PixShell.Tests.csproj`，隔离 `%TEMP%` 目录自动清理，2/2 通过） | `Passed` |
| **R1-M4 / R1-M5** | `WIN-BUG-003B`, `WIN-BUG-004A`, `WIN-BUG-004B` | 在 WSL2 `Ubuntu` 隔离 `/tmp/pixshell_itest_*`（非 root 用户 `mio`，退出自动 `rm -rf`）实测真实 GNU `tar` 分阶段解压：冲突跳过 `SKIP:existing.txt`、`.env`/`..weird`/含空格文件名迁移、`chmod 555` 只读目录 `mv` 返回非零 `rc=1` 与 `MV_ERR:`、真实 `LC_ALL=C ls -la` 含 `0600`/含空格软链接与 `chmod 000` 不可读目录报错 | [Invoke-IsolatedIntegrationTests.ps1](file:///d:/20_Projects/pixshell/win/tests/Invoke-IsolatedIntegrationTests.ps1)（第 3 节实时通过） | `Passed` |
| **R1-M2 / R1-M4 / R1-M5 / R1-M6** | `WIN-BUG-001A/C`, `003A/B`, `004A/B`, `009A`, `015A` | `SSH.NET` SFTP v3/v4+、Proc SFTP、SCP 回退端到端真实回环连接（直传覆盖、真实 `chmod`/`stat` 特殊位 `4755`/`2755`/`0600`、打包上传/取消及 Dropbear/OpenWrt SCP 解析） | 需要 Docker、Windows OpenSSH Server 或 WSL `openssh-server`（`/usr/sbin/sshd` + `/usr/lib/openssh/sftp-server`） | `Environment-Limited`（本机均未安装，未执行，**未标记通过**；保留在清单待环境就绪或人工烟测补齐） |
| **R1-M7** | `WIN-BUG-011B` | 双 Windows 进程同时写入真实 DPAPI `CredentialStore` 与 `%APPDATA%` JSON 配置的独立端到端测试进程 | 跨平台 `PixShell.Tests`（`net10.0`）未引用 Windows DPAPI；事务锁与损坏阻断已在 `LocalTransactionAndTargetTests` 覆盖，DPAPI 读写已在 R0.4 与 R1 GUI 冒烟覆盖 | `Not Run`（自动化独立双进程 DPAPI 未单列；单机并发逻辑已测） |

> [!NOTE]
> 如后续希望在本机启用“SSH/SFTP/SCP 自动化回环集成测试”，可在 WSL `Ubuntu` 中手动执行 `sudo apt update && sudo apt install -y openssh-server`（提供 `/usr/sbin/sshd` 与 `/usr/lib/openssh/sftp-server`，无需全局启用开机服务，测试脚本可按隔离临时配置与随机高端口启停用户级 `sshd`）。

---

## 三、R1 精简人工 GUI 冒烟清单（仅交互与基础体验，6 项）

不再要求手工构造特殊编码、`0600`/`4755` 权限、损坏 JSON 或只读目录故障；仅验证日常真实连接下的 GUI 交互、取消响应与目标防错：

| 冒烟编号 | 追溯覆盖 | 测试前置条件 | 精简操作步骤 | 预期结果 | 验收状态 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **R1-S1** | 原 `R1-M1`（`WIN-BUG-029`） | 已连接两台常用 SSH 主机（Tab A、Tab B），远端有含子目录的目录。 | 1. 在文件列表快速双击切换目录；<br>2. 浏览/刷新期间切换终端标签到另一台主机。 | 1. 列表与路径栏切换跟手，不串入上一目录或另一主机内容；<br>2. 切换标签后 SFTP 面板同步切换至当前主机。 | `Preliminary Pass`（11/26 实机测试反馈“基本通过”，待确认） |
| **R1-S2** | 原 `R1-M2`（`WIN-BUG-001A/C`, `014A`） | 已连接测试主机，存在普通远端小文本文件；可选一个 >4 MiB 文件。 | 1. 双击打开普通文本文件，删减部分内容后点击保存，关闭重开查看；<br>2. 尝试双击打开 >4 MiB 大文件（可选）。 | 1. 保存成功且无旧尾部字节残留；<br>2. 超大文件被明确拦截并提示改用下载，界面不卡死。 | `Preliminary Pass`（用户 10:08–10:15 实机测试含编辑器打开 UTF-8 与关闭流程，待确认） |
| **R1-S3** | 原 `R1-M3`（`WIN-BUG-001B`） | 已打开 Tab A 与 Tab B 两个连接会话，在 Tab A 打开一个远端文本编辑器并修改未保存。 | 1. 保持编辑器打开，切到 Tab B 后在原编辑器点击“保存”；<br>2. 切回 Tab A 再次点击“保存”。 | 1. 在 Tab B 时保存被明确拦截提示且不写入 Tab B，内容与脏标记保留；<br>2. 切回 Tab A 后成功保存至原文件。 | `Preliminary Pass`（待确认是否已覆盖或需补点） |
| **R1-S4** | 原 `R1-M4`, `R1-M6`, `R1-M8`（`WIN-BUG-003A/B`, `009A`, `015B`） | 正常已连接主机，本地任意测试文件/小目录（若测试跨盘下载可将左侧本地路径切至非 `%TEMP%` 盘如 `D:\`）。 | 1. 上传与下载文件/目录，并在传输过程中测试一次“取消”；<br>2. 确认覆盖一次已有同名远端文件。 | 1. 传输及打包过程中窗口拖动、切换标签与点击取消均流畅响应；<br>2. 跨盘下载落盘成功，同名覆盖与状态提示正确。 | `Preliminary Pass`（待确认） |
| **R1-S5** | 原 `R1-M5`, `R1-M6`（`WIN-BUG-004A/B`, `015A`） | 正常已连接主机上的测试目录。 | 1. 在远端测试目录新建一个临时文件/目录并重命名；<br>2. 右键查看「文件权限…」并确认无异常；<br>3. 删除该临时测试文件/目录。 | 1. 新建、重命名、权限回显与删除流程正常，列表及时刷新；<br>2. 非空目录删除弹出 `D-015` 二次确认。 | `Preliminary Pass`（待确认） |
| **R1-S6** | 原 `R1-M8`, `R1-M7`（`WIN-BUG-019A`, `011A`） | 打开 3 个会话标签（A / B / C）并打开底部/侧边命令面板。 | 1. 在命令下拉选中 Server B，然后关闭 Server A 后发送一条安全只读命令（如 `pwd`）；<br>2. 断开 Server B 再查看下拉与发送拦截提示；<br>3. 快捷命令常规增删/清空后重启确认是否保留。 | 1. 关闭 Server A 后命令仍准确发往 Server B（不漂移到 C）；<br>2. Server B 断开后显示“（已断开）”并拦截误发送；<br>3. 快捷命令变更重启后保持符合预期。 | `Preliminary Pass`（待确认） |

---

## 四、R2 阶段验收追踪（`R2-1` 已完成，待 R2 收尾集中冒烟）

| 切片 / 编号 | 关联缺陷 | 验证层级 | 验证内容与工具链覆盖 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R2-1 / 单元与依赖检查** | `WIN-BUG-002` | 自动化单元测试 / 包扫描 | `SSH.NET` 升级至 `2026.0.1`，`dotnet list package --vulnerable` 确认 2 条 `NU1903` 漏洞清零；`SshNetUpgradeTests`（3 项）验证程序集版本、`RemotePathTransformation.ShellQuote` 对 `$(...)`/反引号/单引号的转义及 `ScpClient`/`SftpClient`/`ProxyTypes` 构造兼容性（`49/49 Passed`）。 | `Passed` |
| **R2-1 / 协议回环与阶段冒烟** | `WIN-BUG-002` | 自动化集成 / 精简冒烟 | 实时 SSH/SFTP/SCP 守护进程 SCP 传输回环在无 `sshd` 环境为 `Environment-Limited`；升级后真实连接、SCP 回退下载/上传交互纳入 R2 阶段收尾集中精简冒烟（不逐切片阻塞人工验收）。 | `Environment-Limited`（协议回环）/ `Pending`（阶段汇总冒烟） |
