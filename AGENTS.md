# PixShell AI 开发协议

本文件定义 Codex 及其他 AI 协作者的稳定工作方式。项目当前架构、已确认决策、阶段计划和接力状态分别以 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/DECISIONS.md](docs/DECISIONS.md)、[docs/CURRENT_PLAN.md](docs/CURRENT_PLAN.md)、[docs/HANDOFF.md](docs/HANDOFF.md) 为准。

## Session Start Protocol

每次新对话、上下文恢复或换电脑时，按顺序：

1. 阅读本文件及四份核心文档；只阅读与任务相关的 Accepted 决策。
2. 执行只读 Git 检查：当前分支、HEAD、`git status --short --branch`、`git diff` 和最近提交；必要时核对远端跟踪状态。
3. 用源码、配置和测试/构建记录核验 Handoff，不以聊天记录替代仓库事实。
4. 有冲突或 DIRTY 工作区时先查明来源；不得覆盖、丢弃或清理未知修改。
5. 状态一致且任务已确认时，从 `docs/HANDOFF.md` 的 **Exact Next Action** 继续。

## 默认执行模式

已确认的开发任务默认直接执行：恢复状态 → 检查现状 → 实施 → 补充相关测试 → 验证 → 检查 diff → 更新计划与接力文档 → 汇报。不要重复询问已冻结的业务/架构决策，也不要因切换会话重新设计已完成工作。

仅在业务规则真实歧义、需要改变 Accepted 决策、存在重大方案分歧、涉及破坏性/不可逆操作，或关键环境/依赖缺失而无法安全继续时暂停并请求确认。未确认的新需求先讨论设计。

## 代码、验证与 Git

- 保持 macOS 与 Windows 既有边界和对等行为；除非任务明确要求，不做顺带重构。
- 修改前检查工作区；不使用会覆盖用户改动的 Git 操作，不擅自切分支、提交、推送或安装依赖。
- 按风险运行最相关的构建、测试、静态检查或手工验证。未运行的验证必须明确记录为 pending，不能写成通过。
- 跨电脑接力以已提交、已推送的 Git 检查点为准。DIRTY 工作区不能标为可同步接力点；接收端先 fetch/pull 并核对本地 HEAD 与远端。

### 分支与交付流程

- `main` 是受保护的发布分支：不得直接在其上开发、提交、推送或合并功能切片。
- `dev` 是远程集成分支：每个已验收切片完成后，更新计划/Handoff，提交并推送到 `origin/dev`。当前仓库尚无 `origin/dev` 时，必须先由用户或获授权的操作者创建它；不得回退为推送 `main`。
- 多任务并行时，从最新 `dev` 创建 `codex/<slice>` 主题分支，完成验证后通过合并/PR 进入 `dev`；串行开发可直接在 `dev` 完成切片。无论哪种方式，都不允许直接合入 `main`。
- `dev` 不自动触发完整 GitHub 构建；需要云端安装包时，只能手动触发 GitHub Actions 并选择 `dev`/对应主题分支。`main` 的 push 自动触发构建。
- `dev → main` 仅用于发布候选：要求本地验证完成、必要时的手动 `dev` 构建成功、验收记录完整，并经用户明确批准。合入 `main` 后由自动 CI 生成构建证据与产物。
- 每个切片的固定顺序：同步 `dev` → 检查工作区 → 实施和测试 → 本地 Windows 构建/手工验收 → 更新文档 → 检查 diff → commit → push `dev`/主题分支。只在需要云端验证时手动触发 GitHub Actions；不等待每次 `dev` push 的完整构建。

## Session Handoff Protocol

在实质开发完成、阶段检查点或准备换会话/电脑时：检查 diff 和状态，运行相关验证，按实际进度更新 `docs/CURRENT_PLAN.md`，再更新 `docs/HANDOFF.md`。准确记录未提交工作、验证结果、阻塞项和下一动作。普通讨论或无实质状态变化时无需更新。

## 文档职责

- `ARCHITECTURE.md`：当前有效架构；引用详细设计，不复制历史文档。
- `DECISIONS.md`：决策台账；不得擅自推翻 Accepted 决策。
- `CURRENT_PLAN.md`：阶段级计划与验收；不放会话流水账。
- `HANDOFF.md`：最小恢复状态；不重复架构或完整计划。

修改代码、架构约束、Accepted 决策、阶段状态或接力状态时，按上述职责同步更新相应文件；其余已有文档保持为详细设计或历史记录。
