import { ref } from 'vue'
import { getRescueSessionDetail } from '@/api/rescue-sessions'
import { getApiErrorMessage } from '@/api/client'
import { logApiError } from '@/utils/safe-api-error'
import { createRequestSequencer, type RequestTicket } from '@/utils/request-sequencer'
import {
  InvalidRescueSessionPayloadError,
  validateAdminRescueSessionDetail,
} from '@/utils/rescue-session'
import type { AdminRescueSessionDetail } from '@/types/rescue-session'
import type { HelpRequestStatus } from '@/types/help'

const DEFAULT_POLL_INTERVAL_MS = 10_000
const CLOSED_STATUSES: ReadonlySet<HelpRequestStatus> = new Set(['closed', 'false_alarm'])

export type RescueSessionDetailFetcher = (
  helpId: string,
  signal: AbortSignal,
) => Promise<AdminRescueSessionDetail>

interface RescueSessionDetailOptions {
  readonly helpId: string
  readonly fetchDetail?: RescueSessionDetailFetcher
  readonly pollIntervalMs?: number
  readonly initiallyVisible?: boolean
  readonly now?: () => string
}

interface ResolvedRescueSessionDetailOptions {
  readonly helpId: string
  readonly fetchDetail: RescueSessionDetailFetcher
  readonly pollIntervalMs: number
  readonly initiallyVisible: boolean
  readonly now: () => string
}

type RequestMode = 'initial' | 'refresh'

interface RequestErrorContext {
  readonly error: unknown
  readonly ticket: RequestTicket
  readonly controller: AbortController
  readonly mode: RequestMode
}

async function fetchDetailFromApi(
  helpId: string,
  signal: AbortSignal,
): Promise<AdminRescueSessionDetail> {
  const response = await getRescueSessionDetail(helpId, signal)
  return response.data.data
}

class RescueSessionDetailController {
  readonly detail = ref<AdminRescueSessionDetail | null>(null)
  readonly loading = ref(false)
  readonly errorMessage = ref('')
  readonly refreshErrorMessage = ref('')
  readonly lastSuccessfulAt = ref<string | null>(null)

  private readonly requestSequencer = createRequestSequencer()
  private pollTimer: ReturnType<typeof setTimeout> | null = null
  private abortController: AbortController | null = null
  private pollGeneration = 0
  private disposed = false

  private helpId: string
  private visible: boolean

  constructor(private readonly options: ResolvedRescueSessionDetailOptions) {
    this.helpId = options.helpId
    this.visible = options.initiallyVisible
  }

  async loadData(): Promise<void> {
    if (!this.canRequest()) return
    this.loading.value = true
    this.errorMessage.value = ''
    await this.requestDetail('initial')
    if (this.abortController === null) this.restartPolling()
  }

  async pollData(): Promise<void> {
    if (!this.canPoll()) return
    await this.requestDetail('refresh')
  }

  private restartPolling(): void {
    this.pollGeneration += 1
    this.schedulePolling(this.pollGeneration)
  }

  async setVisible(visible: boolean): Promise<void> {
    this.visible = visible
    if (!visible) {
      this.pause()
      return
    }
    if (!this.canPoll()) return
    if (this.detail.value) {
      await this.pollData()
      this.restartPolling()
    } else {
      await this.loadData()
    }
  }

  async setHelpId(helpId: string): Promise<void> {
    const nextHelpId = helpId.trim()
    if (nextHelpId === this.helpId) return
    this.pause()
    this.helpId = nextHelpId
    this.detail.value = null
    this.refreshErrorMessage.value = ''
    this.lastSuccessfulAt.value = null
    this.errorMessage.value = nextHelpId ? '' : '缺少求助编号'
    if (!this.canRequest()) return
    await this.loadData()
  }

  destroy(): void {
    this.disposed = true
    this.visible = false
    this.pause()
  }

  private async requestDetail(mode: RequestMode): Promise<void> {
    if (this.disposed) return
    const ticket = this.beginRequest()
    const controller = this.abortController
    if (!controller) return
    try {
      const response = await this.options.fetchDetail(this.helpId, controller.signal)
      if (!this.canApply(ticket, controller)) return
      const nextDetail = validateAdminRescueSessionDetail(response)
      this.detail.value = nextDetail
      this.errorMessage.value = ''
      this.refreshErrorMessage.value = ''
      this.lastSuccessfulAt.value = this.options.now()
      if (this.isClosed()) this.stopPollingLoop()
    } catch (error: unknown) {
      this.handleRequestError({ error, ticket, controller, mode })
    } finally {
      this.finishRequest(ticket, controller)
    }
  }

  private beginRequest(): RequestTicket {
    this.abortController?.abort()
    const ticket = this.requestSequencer.next()
    this.abortController = new AbortController()
    return ticket
  }

  private handleRequestError(context: RequestErrorContext): void {
    if (!this.canApply(context.ticket, context.controller)) return
    const fallback = context.error instanceof InvalidRescueSessionPayloadError
      ? context.error.message
      : '加载急救会话失败'
    logApiError({ context: `rescue-session-detail.${context.mode}`, error: context.error, fallback })
    const message = getApiErrorMessage(context.error, fallback)
    if (this.detail.value) {
      this.refreshErrorMessage.value = message
      return
    }
    this.errorMessage.value = message
  }

  private finishRequest(ticket: RequestTicket, controller: AbortController): void {
    if (!this.requestSequencer.isCurrent(ticket)) return
    if (this.abortController === controller) this.abortController = null
    this.loading.value = false
  }

  private canApply(ticket: RequestTicket, controller: AbortController): boolean {
    return !this.disposed
      && !controller.signal.aborted
      && this.requestSequencer.isCurrent(ticket)
  }

  private canPoll(): boolean {
    return this.canRequest() && !this.isClosed()
  }

  private canRequest(): boolean {
    return !this.disposed && this.visible && Boolean(this.helpId)
  }

  private isClosed(): boolean {
    return this.detail.value ? CLOSED_STATUSES.has(this.detail.value.helpStatus) : false
  }

  private pause(): void {
    this.stopPollingLoop()
    this.requestSequencer.invalidate()
    this.abortController?.abort()
    this.abortController = null
    this.loading.value = false
  }

  private clearPollTimer(): void {
    if (!this.pollTimer) return
    clearTimeout(this.pollTimer)
    this.pollTimer = null
  }

  private schedulePolling(generation: number): void {
    if (generation !== this.pollGeneration || !this.canPoll()) return
    this.clearPollTimer()
    this.pollTimer = setTimeout(async () => {
      this.pollTimer = null
      await this.pollData()
      this.schedulePolling(generation)
    }, this.options.pollIntervalMs)
  }

  private stopPollingLoop(): void {
    this.pollGeneration += 1
    this.clearPollTimer()
  }
}

export function useRescueSessionDetail(options: RescueSessionDetailOptions) {
  const controller = new RescueSessionDetailController({
    helpId: options.helpId,
    fetchDetail: options.fetchDetail ?? fetchDetailFromApi,
    pollIntervalMs: options.pollIntervalMs ?? DEFAULT_POLL_INTERVAL_MS,
    initiallyVisible: options.initiallyVisible ?? true,
    now: options.now ?? (() => new Date().toISOString()),
  })
  return {
    detail: controller.detail,
    loading: controller.loading,
    errorMessage: controller.errorMessage,
    refreshErrorMessage: controller.refreshErrorMessage,
    lastSuccessfulAt: controller.lastSuccessfulAt,
    loadData: controller.loadData.bind(controller),
    pollData: controller.pollData.bind(controller),
    setVisible: controller.setVisible.bind(controller),
    setHelpId: controller.setHelpId.bind(controller),
    destroy: controller.destroy.bind(controller),
  }
}
