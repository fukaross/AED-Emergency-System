/**
 * 求助 Store
 *
 * 管理求助发布、求助详情、游客匿名会话
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type {
  PublishHelpPayload,
  PublishHelpResponse,
  HelpRequestDetail,
  GuestHelpSession,
  GuestHelpLookupResult,
  GuestLookupPayload,
  GuestClosePayload,
  GuestCloseResult,
} from '@/types/help'
import {
  publishHelp,
  fetchHelpDetail,
  guestQueryHelp,
  guestCloseHelp,
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
  const guestSession = ref<GuestHelpSession | null>(loadGuestSession())
  const lookupResult = ref<GuestHelpLookupResult | null>(null)
  const publishing = ref(false)

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

  async function loadDetail(id: string): Promise<void> {
    currentRequest.value = await fetchHelpDetail(id)
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
    guestSession.value = null
    lookupResult.value = null
    persistGuestSession(null)
  }

  return {
    currentRequest,
    guestSession,
    lookupResult,
    publishing,
    publish,
    loadDetail,
    guestLookup,
    guestClose,
    clearHelpState,
  }
})
