// 求助相关状态 —— 发布、详情、游客匿名会话
import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type {
  GuestClosePayload,
  GuestCloseResult,
  GuestHelpLookupResult,
  GuestHelpSession,
  GuestLookupPayload,
  HelpListItem,
  HelpRequestDetail,
  PublishHelpPayload,
  PublishHelpResponse,
  SubmitHelpReportPayload,
  SubmitHelpReportResponse,
} from '@/types/help'
import {
  fetchHelpDetail,
  fetchHelpList,
  guestCloseHelp,
  guestQueryHelp,
  publishHelp,
  submitHelpReport,
} from '@/api/help'
import {
  captureAuthSession,
  isAuthEpochCurrent,
  type AuthSessionContext,
} from '@/services/auth-session'

const GUEST_HELP_SESSION_STORAGE_KEY = 'aed_guest_help_session'

class HelpStoreController {
  readonly currentRequest = ref<HelpRequestDetail | null>(null)
  readonly helpList = ref<readonly HelpListItem[]>([])
  readonly guestSession = ref<GuestHelpSession | null>(loadGuestSession())
  readonly lookupResult = ref<GuestHelpLookupResult | null>(null)
  readonly loadingList = ref(false)
  readonly publishing = ref(false)
  readonly activeRequest = computed(() => this.helpList.value.find((item) => (
    item.userRole === 'requester' && isActiveHelpStatus(item.status)
  )) ?? null)

  private authenticatedGeneration = 0
  private latestDetailRequest = 0
  private latestListRequest = 0
  private visibleListRequests = 0

  async publish(payload: PublishHelpPayload): Promise<PublishHelpResponse> {
    this.publishing.value = true
    try {
      const result = await publishHelp(payload)
      this.updateGuestSession(result)
      return result
    } finally {
      this.publishing.value = false
    }
  }

  async loadDetail(id: string, silent = false): Promise<void> {
    const context = captureAuthSession()
    const generation = this.authenticatedGeneration
    const request = ++this.latestDetailRequest
    const detail = await fetchHelpDetail(id, { silent })
    if (!this.isCurrentAuthenticatedRead(context, generation)) return
    if (request !== this.latestDetailRequest) return
    this.currentRequest.value = detail
  }

  async loadList(page = 1, pageSize = 20, silent = false): Promise<void> {
    const context = captureAuthSession()
    const generation = this.authenticatedGeneration
    const request = ++this.latestListRequest
    this.startListLoading(silent)
    try {
      const result = await fetchHelpList(page, pageSize, { silent })
      if (!this.isCurrentAuthenticatedRead(context, generation)) return
      if (request !== this.latestListRequest) return
      this.helpList.value = result.items
    } finally {
      this.finishListLoading(silent, generation)
    }
  }

  async submitReport(
    id: string,
    payload: SubmitHelpReportPayload,
  ): Promise<SubmitHelpReportResponse> {
    const context = captureAuthSession()
    const generation = this.authenticatedGeneration
    const result = await submitHelpReport(id, payload)
    if (!this.isCurrentAuthenticatedRead(context, generation)) return result
    await this.loadDetail(id)
    await this.loadList()
    return result
  }

  async guestLookup(payload: GuestLookupPayload): Promise<void> {
    this.lookupResult.value = await guestQueryHelp(payload)
  }

  async guestClose(payload: GuestClosePayload): Promise<GuestCloseResult> {
    const result = await guestCloseHelp(payload)
    this.guestSession.value = null
    persistGuestSession(null)
    return result
  }

  clearAuthenticatedState(): void {
    this.authenticatedGeneration += 1
    this.latestDetailRequest += 1
    this.latestListRequest += 1
    this.visibleListRequests = 0
    this.currentRequest.value = null
    this.helpList.value = []
    this.loadingList.value = false
  }

  clearHelpState(): void {
    this.clearAuthenticatedState()
    this.guestSession.value = null
    this.lookupResult.value = null
    persistGuestSession(null)
  }

  private updateGuestSession(result: PublishHelpResponse): void {
    if (result.followupCode && result.closeToken) {
      this.guestSession.value = {
        requestNo: result.requestNo,
        followupCode: result.followupCode,
        closeToken: result.closeToken,
      }
    } else {
      this.guestSession.value = null
    }
    persistGuestSession(this.guestSession.value)
  }

  private startListLoading(silent: boolean): void {
    if (silent) return
    this.visibleListRequests += 1
    this.loadingList.value = true
  }

  private finishListLoading(silent: boolean, generation: number): void {
    if (silent || generation !== this.authenticatedGeneration) return
    this.visibleListRequests = Math.max(0, this.visibleListRequests - 1)
    this.loadingList.value = this.visibleListRequests > 0
  }

  private isCurrentAuthenticatedRead(
    context: AuthSessionContext,
    generation: number,
  ): boolean {
    return isAuthEpochCurrent(context)
      && generation === this.authenticatedGeneration
  }
}

export const useHelpStore = defineStore('help', () => {
  const controller = new HelpStoreController()
  return {
    currentRequest: controller.currentRequest,
    helpList: controller.helpList,
    guestSession: controller.guestSession,
    lookupResult: controller.lookupResult,
    activeRequest: controller.activeRequest,
    loadingList: controller.loadingList,
    publishing: controller.publishing,
    publish: (payload: PublishHelpPayload) => controller.publish(payload),
    loadDetail: (id: string, silent = false) => controller.loadDetail(id, silent),
    loadList: (page = 1, pageSize = 20, silent = false) => (
      controller.loadList(page, pageSize, silent)
    ),
    submitReport: (id: string, payload: SubmitHelpReportPayload) => (
      controller.submitReport(id, payload)
    ),
    guestLookup: (payload: GuestLookupPayload) => controller.guestLookup(payload),
    guestClose: (payload: GuestClosePayload) => controller.guestClose(payload),
    clearAuthenticatedState: () => controller.clearAuthenticatedState(),
    clearHelpState: () => controller.clearHelpState(),
  }
})

function isActiveHelpStatus(status: HelpListItem['status']): boolean {
  return status === 'published'
    || status === 'responding'
    || status === 'pending_review'
}

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
