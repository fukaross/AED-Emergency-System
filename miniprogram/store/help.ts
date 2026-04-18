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

export const useHelpStore = defineStore('help', () => {
  // ===== State =====
  const currentRequest = ref<HelpRequestDetail | null>(null)
  const guestSession = ref<GuestHelpSession | null>(null)
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
    return result
  }

  function clearHelpState(): void {
    currentRequest.value = null
    guestSession.value = null
    lookupResult.value = null
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
