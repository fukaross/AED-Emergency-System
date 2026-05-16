/**
 * 急救课堂统计类型
 *
 * 来源：BackEnd.md §4.6A + CLAUDE.md §4.3
 */

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
