// 急救课堂统计相关的类型
// 统计项 key 由后端/管理端维护（demo 数据含 4 类），客户端不做封闭枚举，避免新增统计项即报错。
export type KnowledgeStatKey = string

export interface KnowledgeStatItem {
  readonly key: KnowledgeStatKey
  readonly title: string
  readonly value: number
  readonly sortOrder: number
}

export interface KnowledgeSummary {
  readonly items: readonly KnowledgeStatItem[]
  readonly updatedAt: string
}

export interface QuickActionStep {
  readonly order: number
  readonly title: string
  readonly description: string
  readonly image: string
  readonly keyPoints: readonly string[]
}

export interface QuickAction {
  readonly id: string
  readonly title: string
  readonly icon: string
  readonly summary: string
  readonly sortOrder: number
  readonly steps: readonly QuickActionStep[]
}
