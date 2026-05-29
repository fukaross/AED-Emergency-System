// 急救课堂相关的接口 —— 顶部统计横幅
import { get } from '@/services/request'
import { KNOWLEDGE_SUMMARY_MOCK } from '@/mock/knowledge'
import type { KnowledgeSummary } from '@/types/knowledge'
import { USE_MOCK } from '@/utils/runtime-env'

/** 获取课堂顶部统计横幅数据 */
export function fetchKnowledgeSummary(): Promise<KnowledgeSummary> {
  if (USE_MOCK) {
    return Promise.resolve({
      ...KNOWLEDGE_SUMMARY_MOCK,
      items: [...KNOWLEDGE_SUMMARY_MOCK.items],
    })
  }

  return get<KnowledgeSummary>('/api/knowledge/summary')
}
