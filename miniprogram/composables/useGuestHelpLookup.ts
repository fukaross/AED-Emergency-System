/**
 * 游客查询/关闭求助 composable
 *
 * 通过 requestNo + followupCode 查询求助状态
 * 通过 closeToken 关闭误报
 *
 * D-05 基线要求
 */
import { ref } from 'vue'
import { useHelpStore } from '@/store/help'
import type { GuestHelpLookupResult, GuestCloseResult } from '@/types/help'

export function useGuestHelpLookup() {
  const helpStore = useHelpStore()

  const requestNo = ref('')
  const followupCode = ref('')
  const lookupResult = ref<GuestHelpLookupResult | null>(null)
  const closeResult = ref<GuestCloseResult | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  /** 查询求助状态 */
  async function lookup(): Promise<void> {
    if (!requestNo.value || !followupCode.value) {
      uni.showToast({ title: '请输入求助编号和查询码', icon: 'none' })
      return
    }

    loading.value = true
    error.value = null
    try {
      await helpStore.guestLookup({
        requestNo: requestNo.value,
        followupCode: followupCode.value,
      })
      lookupResult.value = helpStore.lookupResult
    } catch (err) {
      error.value = (err as Error).message
    } finally {
      loading.value = false
    }
  }

  /** 游客关闭误报 */
  async function closeFalseAlarm(options?: {
    requestNo?: string
    closeToken?: string
    note?: string
  }): Promise<void> {
    const session = helpStore.guestSession
    const requestNoValue = options?.requestNo || session?.requestNo
    const closeTokenValue = options?.closeToken || session?.closeToken

    if (!requestNoValue || !closeTokenValue) {
      uni.showToast({ title: '无可关闭的求助会话', icon: 'none' })
      return
    }

    loading.value = true
    error.value = null
    try {
      const result = await helpStore.guestClose({
        requestNo: requestNoValue,
        closeToken: closeTokenValue,
        note: options?.note,
      })
      closeResult.value = result
      uni.showToast({ title: '已关闭', icon: 'success' })
    } catch (err) {
      error.value = (err as Error).message
    } finally {
      loading.value = false
    }
  }

  /** 从 guestSession 自动填充查询信息 */
  function prefillFromSession(): void {
    const session = helpStore.guestSession
    if (session) {
      requestNo.value = session.requestNo
      followupCode.value = session.followupCode
    }
  }

  return {
    requestNo,
    followupCode,
    lookupResult,
    closeResult,
    loading,
    error,
    lookup,
    closeFalseAlarm,
    prefillFromSession,
  }
}
