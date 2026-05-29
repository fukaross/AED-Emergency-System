// 急救课堂统计相关的类型

export type KnowledgeStatKey =
  | 'aed_count'
  | 'trained_people'
  | 'certified_people'
  | 'coverage_rate'

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
