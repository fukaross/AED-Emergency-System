import { ref } from 'vue'
import { getRescueSessionDetail } from '@/api/rescue-sessions'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer, type RequestTicket } from '@/utils/request-sequencer'
import type { AdminRescueSessionDetail } from '@/types/rescue-session'

const DEFAULT_POLL_INTERVAL_MS = 10_000
const CLOSED_STATUSES = new Set(['closed', 'false_alarm'])

export type RescueSessionDetailFetcher = (
  helpId: string,
  signal: AbortSignal,
) => Promise<AdminRescueSessionDetail>

interface RescueSessionDetailOptions {
  readonly helpId: string
  readonly fetchDetail?: RescueSessionDetailFetcher
  readonly pollIntervalMs?: number
  readonly initiallyVisible?: boolean
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

  private readonly requestSequencer = createRequestSequencer()
  private pollTimer: ReturnType<typeof setTimeout> | null = null
  private abortController: AbortController | null = null
  private pollGeneration = 0
  private disposed = false

  constructor(
    private helpId: string,
    private readonly fetchDetail: RescueSessionDetailFetcher,
    private readonly pollIntervalMs: number,
    private visible: boolean,
  ) {}

  async loadData(): Promise<void> {
    if (!this.canRequest()) return
    this.loading.value = true
    this.errorMessage.value = ''
    await this.requestDetail(true)
  }

  async pollData(): Promise<void> {
    if (!this.canPoll()) return
    await this.requestDetail(false)
  }

  startPolling(): void {
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
    } else {
      await this.loadData()
    }
    if (this.detail.value) this.startPolling()
  }

  async setHelpId(helpId: string): Promise<void> {
    const nextHelpId = helpId.trim()
    if (nextHelpId === this.helpId) return
    this.pause()
    this.helpId = nextHelpId
    this.detail.value = null
    this.errorMessage.value = nextHelpId ? '' : '缺少求助编号'
    if (!this.canRequest()) return
    await this.loadData()
    if (this.detail.value) this.startPolling()
  }

  destroy(): void {
    this.disposed = true
    this.visible = false
    this.pause()
  }

  private async requestDetail(showError: boolean): Promise<void> {
    if (this.disposed) return
    const ticket = this.beginRequest()
    const controller = this.abortController
    if (!controller) return
    try {
      const nextDetail = await this.fetchDetail(this.helpId, controller.signal)
      if (!this.canApply(ticket, controller)) return
      this.detail.value = nextDetail
      this.errorMessage.value = ''
      if (this.isClosed()) this.stopPollingLoop()
    } catch (error: unknown) {
      this.handleRequestError(error, ticket, controller, showError)
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

  private handleRequestError(
    error: unknown,
    ticket: RequestTicket,
    controller: AbortController,
    showError: boolean,
  ): void {
    if (!this.canApply(ticket, controller) || !showError) return
    console.error(error)
    this.errorMessage.value = getApiErrorMessage(error, '加载急救会话失败')
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
    }, this.pollIntervalMs)
  }

  private stopPollingLoop(): void {
    this.pollGeneration += 1
    this.clearPollTimer()
  }
}

export function useRescueSessionDetail(options: RescueSessionDetailOptions) {
  const controller = new RescueSessionDetailController(
    options.helpId,
    options.fetchDetail ?? fetchDetailFromApi,
    options.pollIntervalMs ?? DEFAULT_POLL_INTERVAL_MS,
    options.initiallyVisible ?? true,
  )
  return {
    detail: controller.detail,
    loading: controller.loading,
    errorMessage: controller.errorMessage,
    loadData: controller.loadData.bind(controller),
    pollData: controller.pollData.bind(controller),
    startPolling: controller.startPolling.bind(controller),
    setVisible: controller.setVisible.bind(controller),
    setHelpId: controller.setHelpId.bind(controller),
    destroy: controller.destroy.bind(controller),
  }
}
