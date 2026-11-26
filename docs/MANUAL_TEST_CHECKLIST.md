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

## 四、R2 阶段验收清单（`SSH.NET` 升级、主机身份、凭据与代理安全）

- **最新阶段构建（R2 完整发布）**：`Release / net10.0-windows / win-x64`（self-contained），完整路径 `D:\20_Projects\pixshell\win\publish\win-x64\PixShell.exe`（构建时间 `2026/11/26 15:53:25`，`0` 错误，xUnit **`58/58` 通过**）。

### 4.1 R2 自动化单元测试项（12 项新增，xUnit `net10.0`）

| 追溯编号 | 关联缺陷 | 覆盖内容与验证边界 | 对应测试方法（`win/tests/`） | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R2-1** | `WIN-BUG-002` | `SSH.NET 2026.0.1` 程序集版本、`RemotePathTransformation.ShellQuote` 对 `$(...)`/反引号/单引号安全转义、`ScpClient`/`SftpClient`/`ProxyTypes` 构造兼容性及 NuGet 漏洞清零 | `SshNetUpgradeTests.SshNetAssemblyIsPatched2026_0_1OrNewer`<br>`SshNetUpgradeTests.ScpShellQuoteNeutralizesShellMetacharactersAndConfiguresScpClientSafely`<br>`SshNetUpgradeTests.ConnectionInfoAndSftpClientConstructCleanlyWithProxyTypesAndModernAlgorithms` | `Passed` |
| **R2-2** | `WIN-BUG-012A`, `WIN-BUG-024` | 首次建连写入规范主机/`[host]:port`、复连严格校验匹配、`HostKeyEventArgs` 拦截指纹篡改并返回包含期待/实际 SHA256 指纹的 `HostKeyVerificationException`、支持 `HashKnownHosts`（`\|1\|...`）/通配/否定/撤销 `@revoked`、并发写入加锁不损坏 `known_hosts` | `HostKeyVerificationTests.FirstConnectRecordsCanonicalHostAndRepeatConnectMatchesAcrossPortAndCase`<br>`HostKeyVerificationTests.SshHostKeyGuardBlocksChangedKeyAndWrapsMismatchWithFingerprints`<br>`HostKeyVerificationTests.MatchesHashedKnownHostsNegatedPatternsAndBlocksRevokedKeys`<br>`HostKeyVerificationTests.StrictUnknownModeRejectsUnknownHostWithoutWritingFile`<br>`HostKeyVerificationTests.ConcurrentFirstConnectsRecordAllHostsCleanlyWithoutCorruption` | `Passed` |
| **R2-3** | `WIN-BUG-013A`, `WIN-BUG-018A`, `WIN-BUG-024` | 禁止 `ssh-jump` / 残缺主机 / 非法端口 / FIDO2 OpenSSH 代理静默直连降级；兼容历史 `"type": 200` 反序列化并拒绝直连；修复 `ProxyConfig.NewId()` Base36 崩溃；阻止非回环明文 HTTP WebDAV 凭据外泄 | `ProxyAndSecurityPolicyTests.SshJumpAndMalformedProxyAreRejectedInsteadOfSilentDirectFallback`<br>`ProxyAndSecurityPolicyTests.Fido2OpenSshRejectsActiveProxyRatherThanBypassing`<br>`ProxyAndSecurityPolicyTests.LegacyNumericProxyType200DeserializesAsSshJumpAndIsBlocked`<br>`ProxyAndSecurityPolicyTests.WebDavSecurityPolicyAllowsHttpsAndLoopbackHttpButBlocksRemotePlaintextHttp` | `Passed` |

### 4.2 R2 自动化协议集成测试项

| 追溯编号 | 关联缺陷 | 隔离环境与验证内容 | 依赖条件 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R2-IT1** | `WIN-BUG-002`, `012A`, `013A`, `024` | 真实回环 `sshd` / SOCKS5 代理下的 `SshClient`/`SftpClient`/`ScpClient` 指纹篡改拒连、首次自动记入、经代理禁用直连 `ProcSftpClient` 及 `ScpBackend` 传输回环 | Docker / Windows OpenSSH Server / WSL `openssh-server` + 本地代理服务 | `Environment-Limited`（未安装 `sshd`/Docker，未执行，**未标记通过**） |

### 4.3 R2 精简人工 GUI 冒烟清单（仅交互与基础体验，4 项）

不再要求人工造 `known_hosts` 哈希行、`@revoked` 条目或手动篡改服务端密钥；仅确认日常交互与明确报错反馈：

| 冒烟编号 | 关联缺陷 | 测试前置条件 | 精简操作步骤 | 预期结果 | 验收状态 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **R2-S1** | `WIN-BUG-012A`, `024` | 已在最新版程序中准备任意一台可正常连接的常用 SSH 主机。 | 1. 正常连接该主机打开终端及 SFTP 文件列表；<br>2. 打开「文件 → 主机指纹管理…」查看列表中是否出现该主机 SHA256/MD5 指纹。 | 1. 首次连接成功时自动写入 `%USERPROFILE%\.ssh\known_hosts` 且后续重连/SFTP 无异常阻塞；<br>2. 「主机指纹管理…」正常展示与删除（注意：仅删除测试记录即可，勿误删其他重要主机）。 | `Passed`（用户确认符合预期） |
| **R2-S2** | `WIN-BUG-002`, `013A` | 正常主机（若本机恰好有可用 SOCKS5/HTTP 代理，可选关联测试）。 | 1. 打开「代理管理…」点「＋新建代理」保存一条配置（验证不再崩溃），再编辑/删除该临时代理；<br>2. 若使用 SCP 回退或 SFTP 主路径，确认文件浏览/小文件下载正常。 | 1. 新建/编辑/保存代理界面不再抛 `Invalid Base` 或崩溃，列表正常回显；<br>2. SFTP/SCP 浏览与下载正常。 | `Passed`（用户确认符合预期） |
| **R2-S3** | `WIN-BUG-018A`, `013A` | 无需真实服务端即可测试：在「备份与恢复 → WebDAV 设置」填入远端 `http://example.com/backup.json`。 | 1. 尝试保存并触发「上传到 WebDAV」或检查配置提示（随后清回原设置或取消）；<br>2. 如有历史 `ssh-jump` 代理主机尝试连接时观察提示。 | 1. 非回环 `http://` WebDAV 明确报错拒绝发送明文 Basic 凭据；<br>2. 不支持的跳板代理显式提示不支持并拦截，不再静默直连。 | `Passed`（用户确认符合预期） |
| **R2-S4** | `WIN-BUG-024` | 已连接一个正常 SSH 会话标签。 | 1. 执行断开重连操作，并关闭该标签；<br>2. 重新双击原主机验证界面响应与 DPAPI 自动登录正常。 | 1. 断开、重连与关签过程无报错或卡死，既有 DPAPI 凭据正常加载。 | `Passed`（用户确认符合预期） |

---

## 五、R3 阶段验收清单（终端响应性、输出洪峰、高亮与断开生命周期）

- **最新阶段构建（R3 完整发布）**：`Release / net10.0-windows / win-x64`（self-contained），完整路径 `D:\20_Projects\pixshell\win\publish\win-x64\PixShell.exe`（构建时间 `2026/11/26 16:06:26`，`0` 错误，xUnit **`62/62` 通过**）。

### 5.1 R3 自动化单元测试项（4 项新增，xUnit `net10.0`）

| 追溯编号 | 关联缺陷 | 覆盖内容与验证边界 | 对应测试方法（`win/tests/`） | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R3-1** | `WIN-BUG-025`, `026` | 200 次高频输出仅触发 1 次 UI 消费信号；极端背压（超出上限）丢弃最旧块保内存并按 `maxBatchBytes` 分批释放 | `TerminalStabilityTests.OutputBatcherCoalescesHighFrequencyFloodIntoSinglePendingDrainSignal`<br>`TerminalStabilityTests.OutputBatcherDropsOldestChunksUnderBackpressureAndSplitsByMaxBatchBytes` | `Passed` |
| **R3-2** | `WIN-BUG-026`, `027` | 底层流阻塞时 `TerminalSerialWriter.EnqueueInput`/`EnqueueResize` 立即返回不阻塞调用线程，恢复后严格保持 FIFO 输入顺序及合并最新 resize | `TerminalStabilityTests.SerialWriterNeverBlocksCallerDuringStalledStreamAndPreservesFifoPlusCoalescedResize` | `Passed` |
| **R3-3** | `WIN-BUG-027`, `025` | `SemanticHighlight.Decorate` 支持 `\u001b[m` 重置、`>4096` 超长纯文本行 O(1) 快速跳过不卡 CPU，正则配 50ms 超时保护 | `TerminalStabilityTests.SemanticHighlightSkipsOversizedPlainLineAndResetsOnShortEscM` | `Passed` |

### 5.2 R3 自动化协议集成测试项

| 追溯编号 | 关联缺陷 | 隔离环境与验证内容 | 依赖条件 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **R3-IT1** | `WIN-BUG-025/026/027` | 真实远端高吞吐 `yes`/大日志输出 + 窗口拖动/按键 + 并发 `PollMonitor` 与 SFTP 打包 + 断网关签全链路回环压测 | Docker / Windows OpenSSH Server / WSL `openssh-server` | `Environment-Limited`（未安装 `sshd`/Docker，未执行，**未标记通过**） |

### 5.3 R3 精简人工 GUI 冒烟清单（仅交互与流畅度体验，3 项）

| 冒烟编号 | 关联缺陷 | 测试前置条件 | 精简操作步骤 | 预期结果 | 验收状态 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **R3-S1** | `WIN-BUG-025`, `027` | 已连接一个正常 SSH 或本机终端会话。 | 1. 执行一次较大输出命令（如 `ls -laR /usr` 或 `find /etc`）并同时来回切换标签、拖动侧栏或按 `Ctrl+C`；<br>2. 调整一次窗口大小。 | 1. 大量输出刷屏期间 WPF 界面、标签切换、拖动与 `Ctrl+C` 中断保持流畅不假死，中文输出无 `U+FFFD`；<br>2. 窗口/终端尺寸自适应贴合无明显延迟。 | `Passed`（用户确认通过） |
| **R3-S2** | `WIN-BUG-026` | 已连接一个 SSH 会话标签。 | 1. 粘贴一段多行命令/文本到终端；<br>2. 在终端运行命令期间点击顶部「文件/监控」或关闭当前标签。 | 1. 粘贴响应迅速不卡窗口；<br>2. 关闭正在运行输出或待命的会话标签时窗口瞬间关闭标签，不再卡顿 2 秒。 | `Passed`（用户确认通过） |
| **R3-S3** | `WIN-BUG-025`, `026` | 正在开启右侧监控栏并连接到同一台 SSH 主机。 | 1. 等待右侧监控图表刷新一次，同时在左侧 SFTP 浏览或切换目录；<br>2. 断开再重连一次。 | 1. 监控 3 秒自动刷新与 SFTP/工具栏操作互不干扰；<br>2. 断开重连后输入输出正常。 | `Passed`（用户确认通过） |
