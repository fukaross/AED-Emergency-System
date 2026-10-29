import { ref } from 'vue'
import { getRescueSessions } from '@/api/rescue-sessions'
import { getApiErrorMessage } from '@/api/client'
import { logApiError } from '@/utils/safe-api-error'
import { createRequestSequencer, type RequestTicket } from '@/utils/request-sequencer'
import type {
  AdminRescueSessionListItem,
  AdminRescueSessionListResponse,
} from '@/types/rescue-session'

const DEFAULT_POLL_INTERVAL_MS = 10_000

export interface RescueSessionListQuery {
  readonly status: 'active' | 'history'
  readonly page: number
  readonly pageSize: number
}

export type RescueSessionListFetcher = (
  query: RescueSessionListQuery,
  signal: AbortSignal,
) => Promise<AdminRescueSessionListResponse>

interface RescueSessionListOptions {
  readonly query: RescueSessionListQuery
  readonly fetchList?: RescueSessionListFetcher
  readonly pollIntervalMs?: number
  readonly initiallyVisible?: boolean
  readonly now?: () => string
}

interface ResolvedRescueSessionListOptions {
  readonly query: RescueSessionListQuery
  readonly fetchList: RescueSessionListFetcher
  readonly pollIntervalMs: number
  readonly initiallyVisible: boolean
  readonly now: () => string
}

type RequestMode = 'initial' | 'refresh' | 'background'

interface RequestErrorContext {
  readonly error: unknown
  readonly ticket: RequestTicket
  readonly controller: AbortController
  readonly mode: RequestMode
}

async function fetchListFromApi(
  query: RescueSessionListQuery,
  signal: AbortSignal,
): Promise<AdminRescueSessionListResponse> {
  const response = await getRescueSessions(query, signal)
  return response.data.data
}

class RescueSessionListController {
  readonly items = ref<AdminRescueSessionListItem[]>([])
  readonly totalItems = ref(0)
  readonly loading = ref(false)
  readonly errorMessage = ref('')
  readonly refreshErrorMessage = ref('')
  readonly lastSuccessfulAt = ref<string | null>(null)
  readonly hasLoadedSuccessfully = ref(false)

  private readonly requestSequencer = createRequestSequencer()
  private query: RescueSessionListQuery
  private pollTimer: ReturnType<typeof setTimeout> | null = null
  private abortController: AbortController | null = null
  private pollGeneration = 0
  private visible: boolean
  private disposed = false

  constructor(private readonly options: ResolvedRescueSessionListOptions) {
    this.query = { ...options.query }
    this.visible = options.initiallyVisible
  }

  async initialize(): Promise<void> {
    if (!this.canRequest()) return
    await this.requestList('initial')
    this.restartPolling()
  }

  async retry(): Promise<void> {
    this.stopPollingLoop()
    await this.requestList('initial')
    this.restartPolling()
  }

  async setVisible(visible: boolean): Promise<void> {
    this.visible = visible
    if (!visible) {
      this.pauseRequests()
      return
    }
    if (!this.canRequest()) return
    const mode = this.hasLoadedSuccessfully.value ? 'refresh' : 'initial'
    await this.requestList(mode)
    this.restartPolling()
  }

  async setQuery(query: RescueSessionListQuery): Promise<void> {
    if (sameQuery(this.query, query)) return
    this.pauseRequests()
    this.query = { ...query }
    this.resetResult()
    await this.initialize()
  }

  destroy(): void {
    this.disposed = true
    this.visible = false
    this.pauseRequests()
  }

  private async requestList(mode: RequestMode): Promise<void> {
    if (!this.canRequest()) return
    if (mode === 'initial') {
      this.loading.value = true
      this.errorMessage.value = ''
    }
    const ticket = this.beginRequest()
    const controller = this.abortController
    if (!controller) return
    try {
      const result = await this.options.fetchList(this.query, controller.signal)
      if (!this.canApply(ticket, controller)) return
      this.applyResult(result)
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

  private applyResult(result: AdminRescueSessionListResponse): void {
    this.items.value = [...result.items]
    this.totalItems.value = result.total
    this.hasLoadedSuccessfully.value = true
    this.lastSuccessfulAt.value = this.options.now()
    this.errorMessage.value = ''
    this.refreshErrorMessage.value = ''
  }

  private handleRequestError(context: RequestErrorContext): void {
    if (!this.canApply(context.ticket, context.controller)) return
    const fallback = '加载急救会话列表失败'
    logApiError({ context: `rescue-session-list.${context.mode}`, error: context.error, fallback })
    const message = getApiErrorMessage(context.error, fallback)
    if (this.hasLoadedSuccessfully.value) {
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

  private canRequest(): boolean {
    return !this.disposed && this.visible
  }

  private restartPolling(): void {
    this.stopPollingLoop()
    this.schedulePolling(this.pollGeneration)
  }

  private schedulePolling(generation: number): void {
    if (generation !== this.pollGeneration || !this.canRequest()) return
    this.pollTimer = setTimeout(async () => {
      this.pollTimer = null
      await this.requestList('background')
      this.schedulePolling(generation)
    }, this.options.pollIntervalMs)
  }

  private pauseRequests(): void {
    this.stopPollingLoop()
    this.requestSequencer.invalidate()
    this.abortController?.abort()
    this.abortController = null
    this.loading.value = false
  }

  private stopPollingLoop(): void {
    this.pollGeneration += 1
    if (!this.pollTimer) return
    clearTimeout(this.pollTimer)
    this.pollTimer = null
  }

  private resetResult(): void {
    this.items.value = []
    this.totalItems.value = 0
    this.hasLoadedSuccessfully.value = false
    this.lastSuccessfulAt.value = null
    this.errorMessage.value = ''
    this.refreshErrorMessage.value = ''
  }
}

function sameQuery(left: RescueSessionListQuery, right: RescueSessionListQuery): boolean {
  return left.status === right.status
    && left.page === right.page
    && left.pageSize === right.pageSize
}

export function useRescueSessionList(options: RescueSessionListOptions) {
  const resolved: ResolvedRescueSessionListOptions = {
    query: options.query,
    fetchList: options.fetchList ?? fetchListFromApi,
    pollIntervalMs: options.pollIntervalMs ?? DEFAULT_POLL_INTERVAL_MS,
    initiallyVisible: options.initiallyVisible ?? true,
    now: options.now ?? (() => new Date().toISOString()),
  }
  const controller = new RescueSessionListController(resolved)
  return {
    items: controller.items,
    totalItems: controller.totalItems,
    loading: controller.loading,
    errorMessage: controller.errorMessage,
    refreshErrorMessage: controller.refreshErrorMessage,
    lastSuccessfulAt: controller.lastSuccessfulAt,
    hasLoadedSuccessfully: controller.hasLoadedSuccessfully,
    initialize: controller.initialize.bind(controller),
    retry: controller.retry.bind(controller),
    setVisible: controller.setVisible.bind(controller),
    setQuery: controller.setQuery.bind(controller),
    destroy: controller.destroy.bind(controller),
  }
}
