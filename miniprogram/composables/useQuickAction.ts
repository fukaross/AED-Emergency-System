import { ref } from 'vue'
import { fetchQuickActionDetail } from '@/api/course'
import type { QuickAction } from '@/types/knowledge'

export function useQuickAction() {
  const action = ref<QuickAction | null>(null)
  const loading = ref(true)
  const errorText = ref<string | null>(null)

  async function loadAction(actionId: string) {
    if (!actionId) {
      errorText.value = '关键步骤不存在'
      loading.value = false
      return false
    }
    loading.value = true
    errorText.value = null
    try {
      const detail = await fetchQuickActionDetail(actionId)
      action.value = detail
      uni.setNavigationBarTitle({ title: detail.title })
      return true
    } catch (err) {
      errorText.value = (err as Error).message || '加载失败'
      return false
    } finally {
      loading.value = false
    }
  }

  return { action, loading, errorText, loadAction }
}
