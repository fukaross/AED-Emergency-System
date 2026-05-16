/**
 * 急救课堂 Store
 *
 * 管理课堂顶部统计横幅数据。
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { fetchKnowledgeSummary } from '@/api/knowledge'
import type { KnowledgeSummary } from '@/types/knowledge'

export const useKnowledgeStore = defineStore('knowledge', () => {
  const summary = ref<KnowledgeSummary | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const summaryItems = computed(() => {
    const items = summary.value?.items ?? []
    return [...items].sort((left, right) => left.sortOrder - right.sortOrder)
  })

  async function loadSummary(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      summary.value = await fetchKnowledgeSummary()
    } catch (err) {
      error.value = (err as Error).message
      summary.value = null
    } finally {
      loading.value = false
    }
  }

  return {
    summary,
    summaryItems,
    loading,
    error,
    loadSummary,
  }
})
