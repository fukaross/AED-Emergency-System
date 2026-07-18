// 会话实时通道编排：优先 WS 扇出，断线降级 afterSeq 轮询（ADR 0001；BackEnd.md §4.5A 传输约定）
import { onUnmounted, ref, type Ref } from 'vue'
import { connectRescueSocket, type RescueSocketHandle } from '@/services/rescue-socket'
import type { RescueSocketEnvelope } from '@/types/rescue-session'
import { RESCUE_SESSION_POLL_INTERVAL_MS } from '@/utils/rescue-signal'

const SIGNAL_REFRESH_INTERVAL_MS = 1000

export interface SessionRealtimeOptions {
  readonly getHelpId: () => string
  readonly onEnvelope: (envelope: RescueSocketEnvelope, helpId: string) => void
  readonly onPollRefresh: (helpId: string) => Promise<void>
  readonly onSignalRefresh: (now: number) => void
}

interface RealtimeRun {
  readonly helpId: string
  stopped: boolean
  fallbackStarted: boolean
  refreshScheduled: boolean
  refreshing: boolean
  refreshPending: boolean
  socket: RescueSocketHandle | null
  pollTimer: ReturnType<typeof setInterval> | null
  retryTimer: ReturnType<typeof setTimeout> | null
  signalTimer: ReturnType<typeof setInterval> | null
}

class SessionRealtimeController {
  private currentRun: RealtimeRun | null = null

  constructor(
    private readonly options: SessionRealtimeOptions,
    private readonly usingSocket: Ref<boolean>,
  ) {}

  start(): void {
    if (this.currentRun && !this.currentRun.stopped) return
    const helpId = this.options.getHelpId()
    if (!helpId) throw new Error('缺少求助编号，无法启动实时会话')
    const run = this.createRun(helpId)
    this.currentRun = run
    this.options.onSignalRefresh(Date.now())
    run.signalTimer = setInterval(
      () => this.options.onSignalRefresh(Date.now()),
      SIGNAL_REFRESH_INTERVAL_MS,
    )
    this.connect(run)
  }

  stop(): void {
    const run = this.currentRun
    this.currentRun = null
    this.usingSocket.value = false
    if (!run) return
    run.stopped = true
    this.stopPolling(run)
    this.clearRefreshRetry(run)
    if (run.signalTimer) clearInterval(run.signalTimer)
    run.signalTimer = null
    run.socket?.close()
    run.socket = null
  }

  private createRun(helpId: string): RealtimeRun {
    return {
      helpId,
      stopped: false,
      fallbackStarted: false,
      refreshScheduled: false,
      refreshing: false,
      refreshPending: false,
      socket: null,
      pollTimer: null,
      retryTimer: null,
      signalTimer: null,
    }
  }

  private isActive(run: RealtimeRun): boolean {
    return this.currentRun === run && !run.stopped
  }

  private connect(run: RealtimeRun): void {
    try {
      run.socket = connectRescueSocket(run.helpId, {
        onEnvelope: (envelope) => this.handleEnvelope(run, envelope),
        onInvalidFrame: () => this.requestRefresh(run),
        onOpen: () => this.handleOpen(run),
        onUnavailable: () => this.fallbackToPolling(run),
      })
    } catch (error) {
      logRealtimeError('socket-connect', error)
      this.fallbackToPolling(run)
    }
  }

  private handleOpen(run: RealtimeRun): void {
    if (!this.isActive(run)) return
    run.fallbackStarted = false
    this.stopPolling(run)
    this.usingSocket.value = true
    this.requestRefresh(run)
  }

  private handleEnvelope(run: RealtimeRun, envelope: RescueSocketEnvelope): void {
    if (!this.isActive(run)) return
    this.options.onEnvelope(envelope, run.helpId)
    if (this.isActive(run)) this.requestRefresh(run)
  }

  private fallbackToPolling(run: RealtimeRun): void {
    if (!this.isActive(run) || run.fallbackStarted) return
    run.fallbackStarted = true
    this.usingSocket.value = false
    this.clearRefreshRetry(run)
    this.requestRefresh(run)
    run.pollTimer = setInterval(
      () => this.requestRefresh(run),
      RESCUE_SESSION_POLL_INTERVAL_MS,
    )
  }

  private stopPolling(run: RealtimeRun): void {
    if (!run.pollTimer) return
    clearInterval(run.pollTimer)
    run.pollTimer = null
  }

  private requestRefresh(run: RealtimeRun): void {
    if (!this.isActive(run)) return
    if (run.refreshing) {
      run.refreshPending = true
      return
    }
    if (run.refreshScheduled) return
    run.refreshScheduled = true
    void Promise.resolve().then(() => this.executeRefresh(run))
  }

  private scheduleRefreshRetry(run: RealtimeRun): void {
    if (!this.isActive(run) || run.fallbackStarted || run.retryTimer) return
    run.retryTimer = setTimeout(() => {
      run.retryTimer = null
      this.requestRefresh(run)
    }, RESCUE_SESSION_POLL_INTERVAL_MS)
  }

  private clearRefreshRetry(run: RealtimeRun): void {
    if (!run.retryTimer) return
    clearTimeout(run.retryTimer)
    run.retryTimer = null
  }

  private async executeRefresh(run: RealtimeRun): Promise<void> {
    run.refreshScheduled = false
    if (!this.isActive(run)) return
    run.refreshing = true
    try {
      await this.options.onPollRefresh(run.helpId)
      this.clearRefreshRetry(run)
    } catch (error) {
      if (this.isActive(run)) {
        logRealtimeError('http-catchup', error)
        this.scheduleRefreshRetry(run)
      }
    } finally {
      run.refreshing = false
      if (this.isActive(run) && run.refreshPending) {
        run.refreshPending = false
        this.requestRefresh(run)
      }
    }
  }
}

function logRealtimeError(scope: string, error: unknown): void {
  const apiError = error as { readonly statusCode?: number; readonly code?: number }
  console.warn(
    `[SessionRealtime] ${scope} status=${apiError?.statusCode ?? 'network'} code=${apiError?.code ?? 'none'}`,
  )
}

export function useSessionRealtime(options: SessionRealtimeOptions) {
  const usingSocket = ref(false)
  const controller = new SessionRealtimeController(options, usingSocket)
  const start = () => controller.start()
  const stop = () => controller.stop()
  onUnmounted(stop)
  return { usingSocket, start, stop }
}
