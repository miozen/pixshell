# PixShell 当前架构

## 定位与边界

PixShell 是单仓库维护的 macOS / Windows 原生 SSH、SFTP 与本机自动化客户端。它没有独立服务端、数据库、容器编排或远程业务 API；所有业务状态主要存于用户本机，远程交互经 SSH/SFTP 或可选 WebDAV 完成。

本文件描述当前有效架构。早期原生化拆分和布局契约保留在 [mac/docs/BLUEPRINT.md](../mac/docs/BLUEPRINT.md)、[win/BLUEPRINT.md](../win/BLUEPRINT.md)、[mac/docs/LAYOUT-PARITY.md](../mac/docs/LAYOUT-PARITY.md) 与 [win/LAYOUT-PARITY.md](../win/LAYOUT-PARITY.md)，仅作详细背景，不能覆盖当前源码事实。

## 技术栈

| 平台 | 应用与终端 | SSH / SFTP | 构建 |
| --- | --- | --- | --- |
| macOS 13+ | Swift、AppKit、SwiftTerm | SwiftNIO SSH、自研 SFTP v3；系统 OpenSSH 回退 | SwiftPM / Xcode |
| Windows | C#、.NET 9 WPF、WebView2、xterm.js | SSH.NET；FIDO2/兼容路径使用系统 OpenSSH | `dotnet` / Inno Setup |

版本和依赖以 [mac/Package.swift](../mac/Package.swift)、[mac/Package.resolved](../mac/Package.resolved) 与 [win/PixShell.csproj](../win/PixShell.csproj) 为准。

## 模块与职责

| 模块 | macOS | Windows | 职责 |
| --- | --- | --- | --- |
| 应用编排 | `Sources/PixShell/App/`、`main.swift` | `MainWindow*.cs` | 五区 UI、会话选择、功能入口和生命周期 |
| 会话与终端 | `Session/TermSession.swift` | `TerminalSession.cs` | 每标签终端、输入输出、状态、输出缓冲与节流 |
| SSH | `SSH/` | `TerminalSession.cs` | PTY shell、认证、resize、exec、keepalive 与回退 |
| SFTP | `SFTP/` | `Sftp/` | 远端文件系统、传输、编辑和权限操作 |
| 本地桥 | `Bridge/` | `Bridge/` | CLI/MCP/Web SSH 的回环 HTTP 路由和无头会话池 |
| 本地数据 | `Store/`、`Proxy/` | `Storage.cs`、`Store/`、`Proxy/` | 主机、凭据、配置、命令、下载、密钥和备份 |
| 辅助 UI | `UI/`、`Monitor/`、`LSP/` | `UI/`、`Monitor/`、`LSP/`、`Theme/` | 监控、编辑、主题、工具和管理窗口 |

## 关键流程

### SSH 与终端

`连接管理/快速连接 → AppDelegate 或 MainWindow → TermSession/TerminalSession → 内置 SSH 引擎 → PTY shell → 原生终端渲染`。

每个标签持有独立会话与输出缓冲。内置引擎遇到算法兼容性、私钥或 FIDO2 限制时可使用系统 OpenSSH；SFTP 复用主机/凭据，但建立独立连接，不与交互 shell 共用通道。

### Agent / CLI

`pixshell CLI、MCP 或 AI SSH wrapper → AgentBridge (127.0.0.1 + token) → BridgeRoutes → HeadlessBridgeHost → 独立 SSH/SFTP 会话池`。

GUI 与 Agent 会话池隔离，桥绑定回环地址、限制请求大小、要求 token 并限制 Origin。路由契约以 `Bridge/BridgeRoutes.*` 为准。

### 持久化

主机清单为 JSON（不含密码）；macOS 凭据为 AES-GCM 文件、密钥由 Keychain 保护，Windows 凭据由 DPAPI 保护。应用设置和辅助数据保存在相应平台用户目录；WebDAV 备份为可选远程 JSON 备份。不存在数据库或迁移。

## 架构约束

- 两端独立原生实现，保持主机模型、五区布局和桥路由语义对齐；不得假设存在共享运行时代码。
- Agent API 仅面向本机自动化，不能改为网络服务。
- 兼容旧 SSH 服务器是既有要求；回退实现必须保持认证、代理和错误语义。
- 主机、密码和私钥内容不得写入日志或主机 JSON。
- 当前 macOS 内置 NIO SSH/SFTP 路径接受所有主机密钥；`KnownHosts` 目前是管理功能，未接入该验证链。这是已知安全缺口，不应在无完整跨路径设计时局部改变。

## 构建、运行与交付

- macOS：`cd mac && DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer swift build`；打包参见 [mac/scripts/package-mac.sh](../mac/scripts/package-mac.sh)。
- Windows：`cd win && dotnet publish PixShell.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64`；本地脚本见 [win/build.ps1](../win/build.ps1)。
- CI：`.github/workflows/build.yml` 在 `main` push、PR(main)、tag 和手动触发时构建 macOS arm64/x64 与 Windows x64；Windows job 运行 `dotnet test`，并检查 zip/setup 发布产物。`dev` 不自动构建，需要时通过 `workflow_dispatch` 手动选择 ref；tag 负责 GitHub Release。

详细用户构建说明见 [README.md](../README.md)、[mac/README.md](../mac/README.md) 和 [mac/docs/WIN-CONN.md](../mac/docs/WIN-CONN.md)。版本号以产品配置和最近提交为准，README 的发布版本文本可能滞后。
