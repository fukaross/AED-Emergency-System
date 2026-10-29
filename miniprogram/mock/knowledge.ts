// 急救课堂 Mock 数据 —— 统计横幅与关键步骤来自 demo/data 生成产物。
import type { KnowledgeSummary, QuickAction } from '@/types/knowledge'
import {
  DEMO_KNOWLEDGE_SUMMARY,
  DEMO_QUICK_ACTIONS,
} from './generated/demo-classroom.generated'

export const KNOWLEDGE_SUMMARY_MOCK: KnowledgeSummary = {
  updatedAt: DEMO_KNOWLEDGE_SUMMARY.updatedAt,
  items: DEMO_KNOWLEDGE_SUMMARY.items
    .filter((item) => item.key.trim() !== '')
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => ({ ...item })),
}

export const QUICK_ACTION_MOCK_DATA: readonly QuickAction[] =
  DEMO_QUICK_ACTIONS.filter((action) => action.isEnabled)
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((action) => ({
      id: action.id,
      title: action.title,
      icon: action.icon,
      summary: action.summary,
      sortOrder: action.sortOrder,
      steps: action.steps.map((step) => ({
        ...step,
        keyPoints: [...step.keyPoints],
      })),
    }))
