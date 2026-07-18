import type { Ref } from 'vue'
import { fetchHelpDetail } from '@/api/help'
import type { HelpRequestDetail } from '@/types/help'
import { ERROR_CODES } from '@/utils/constants'
import { isActiveHelpStatus } from './rescue-view-state'

const HELP_POLL_INTERVAL_MS = 10_000

interface HelpDetailPollingOptions {
  readonly detail: Ref<HelpRequestDetail | null>
  readonly applyDetail: (detail: HelpRequestDetail | null) => void
  readonly handleAccessEnded: (error: unknown) => void
}

interface RequestIdentity {
  readonly id: number
  readonly generation: number
  readonly helpId: string
}

class HelpDetailPoller {
  private pollTimer: ReturnType<typeof setInterval> | null = null
  private generation = 0
  private requestSequence = 0
  private activeRequestId: number | null = null

  constructor(private readonly options: HelpDetailPollingOptions) {}

  async refresh(): Promise<void> {
    const current = this.options.detail.value
    if (this.activeRequestId !== null || !current?.id) return
    if (!isActiveHelpStatus(current.status)) {
      this.stop()
      return
    }
    const request = this.beginRequest(current.id)
    try {
      const next = await fetchHelpDetail(request.helpId, { silent: true })
      if (this.ownsRequest(request)) this.options.applyDetail(next)
    } catch (error) {
      if (!this.ownsRequest(request)) return
      if (isAccessEnded(error)) this.options.handleAccessEnded(error)
      else logPollingError(error)
    } finally {
      if (this.activeRequestId === request.id) this.activeRequestId = null
    }
  }

  start(): void {
    this.stop()
    this.pollTimer = setInterval(
      () => this.refresh(),
      HELP_POLL_INTERVAL_MS,
    )
  }

  stop(): void {
    this.generation += 1
    this.activeRequestId = null
    if (this.pollTimer) clearInterval(this.pollTimer)
    this.pollTimer = null
  }

  private beginRequest(helpId: string): RequestIdentity {
    const request = {
      id: ++this.requestSequence,
      generation: this.generation,
      helpId,
    }
    this.activeRequestId = request.id
    return request
  }

  private ownsRequest(request: RequestIdentity): boolean {
    return this.activeRequestId === request.id
      && this.generation === request.generation
      && this.options.detail.value?.id === request.helpId
  }
}

export function useHelpDetailPolling(options: HelpDetailPollingOptions) {
  const poller = new HelpDetailPoller(options)
  return {
    refreshHelpDetail: () => poller.refresh(),
    startHelpPolling: () => poller.start(),
    stopHelpPolling: () => poller.stop(),
  }
}

function isAccessEnded(error: unknown): boolean {
  if (typeof error !== 'object' || error === null) return false
  const apiError = error as { readonly code?: number; readonly statusCode?: number }
  return apiError.code === ERROR_CODES.UNAUTHORIZED
    || apiError.statusCode === 401
    || apiError.statusCode === 403
    || apiError.statusCode === 409
}

function logPollingError(error: unknown): void {
  const apiError = error as { readonly statusCode?: number; readonly code?: number }
  console.warn(
    `[HelpDetailPolling] GET /api/help/{id} status=${apiError?.statusCode ?? 'network'} code=${apiError?.code ?? 'none'}`,
  )
}
