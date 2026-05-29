// 求助相关状态 —— 发布、详情、游客匿名会话
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type {
  PublishHelpPayload,
  PublishHelpResponse,
  HelpRequestDetail,
  GuestHelpSession,
  GuestHelpLookupResult,
  GuestLookupPayload,
  GuestClosePayload,
  GuestCloseResult,
  HelpListItem,
  SubmitHelpReportPayload,
  SubmitHelpReportResponse,
} from '@/types/help'
import {
  fetchHelpList,
  publishHelp,
  fetchHelpDetail,
  guestQueryHelp,
  guestCloseHelp,
  submitHelpReport,
} from '@/api/help'

const GUEST_HELP_SESSION_STORAGE_KEY = 'aed_guest_help_session'

function loadGuestSession(): GuestHelpSession | null {
  try {
    const raw = uni.getStorageSync(GUEST_HELP_SESSION_STORAGE_KEY) as string
    return raw ? (JSON.parse(raw) as GuestHelpSession) : null
  } catch {
    return null
  }
}

function persistGuestSession(session: GuestHelpSession | null): void {
  try {
    if (!session) {
      uni.removeStorageSync(GUEST_HELP_SESSION_STORAGE_KEY)
      return
    }

    uni.setStorageSync(GUEST_HELP_SESSION_STORAGE_KEY, JSON.stringify(session))
  } catch {
    console.warn('[HelpStore] 游客会话持久化失败')
  }
}

export const useHelpStore = defineStore('help', () => {
  // ===== State =====
  const currentRequest = ref<HelpRequestDetail | null>(null)
  const helpList = ref<readonly HelpListItem[]>([])
  const guestSession = ref<GuestHelpSession | null>(loadGuestSession())
  const lookupResult = ref<GuestHelpLookupResult | null>(null)
  const loadingList = ref(false)
  const publishing = ref(false)

  const activeRequest = computed(() =>
    helpList.value.find((item) =>
      item.userRole === 'requester' && (
        item.status === 'published'
          || item.status === 'responding'
          || item.status === 'pending_review'),
    ) ?? null,
  )

  // ===== Actions =====
  async function publish(
    payload: PublishHelpPayload,
  ): Promise<PublishHelpResponse> {
    publishing.value = true
    try {
      const result = await publishHelp(payload)

      // 游客态保存会话信息
      if (result.followupCode && result.closeToken) {
        guestSession.value = {
          requestNo: result.requestNo,
          followupCode: result.followupCode,
          closeToken: result.closeToken,
        }
        persistGuestSession(guestSession.value)
      } else {
        guestSession.value = null
        persistGuestSession(null)
      }

      return result
    } finally {
      publishing.value = false
    }
  }

  async function loadDetail(id: string, silent = false): Promise<void> {
    currentRequest.value = await fetchHelpDetail(id, { silent })
  }

  async function loadList(page = 1, pageSize = 20, silent = false): Promise<void> {
    if (!silent) loadingList.value = true
    try {
      const result = await fetchHelpList(page, pageSize, { silent })
      helpList.value = result.items
    } finally {
      if (!silent) loadingList.value = false
    }
  }

  async function submitReport(
    id: string,
    payload: SubmitHelpReportPayload,
  ): Promise<SubmitHelpReportResponse> {
    const result = await submitHelpReport(id, payload)
    await loadDetail(id)
    await loadList()
    return result
  }

  async function guestLookup(
    payload: GuestLookupPayload,
  ): Promise<void> {
    lookupResult.value = await guestQueryHelp(payload)
  }

  async function guestClose(
    payload: GuestClosePayload,
  ): Promise<GuestCloseResult> {
    const result = await guestCloseHelp(payload)
    guestSession.value = null
    persistGuestSession(null)
    return result
  }

  function clearHelpState(): void {
    currentRequest.value = null
    helpList.value = []
    guestSession.value = null
    lookupResult.value = null
    persistGuestSession(null)
  }

  return {
    currentRequest,
    helpList,
    guestSession,
    lookupResult,
    activeRequest,
    loadingList,
    publishing,
    publish,
    loadDetail,
    loadList,
    submitReport,
    guestLookup,
    guestClose,
    clearHelpState,
  }
})
