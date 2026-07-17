// 会话实时通道编排：优先 WS 扇出，断线降级 afterSeq 轮询（ADR 0001；BackEnd.md §4.5A 传输约定）
import { onUnmounted, ref } from 'vue'
import { connectRescueSocket, type RescueSocketHandle } from '@/services/rescue-socket'
import type { RescueSocketEnvelope } from '@/types/rescue-session'
import { RESCUE_SESSION_POLL_INTERVAL_MS } from '@/utils/rescue-signal'

export interface SessionRealtimeOptions {
  readonly getHelpId: () => string
  readonly onEnvelope: (envelope: RescueSocketEnvelope) => void
  readonly onPollRefresh: () => Promise<void>
}

export function useSessionRealtime(options: SessionRealtimeOptions) {
  const usingSocket = ref(false)
  let socket: RescueSocketHandle | null = null
  let pollTimer: ReturnType<typeof setInterval> | null = null
  let polling = false
  let stopped = false

  function startPolling(): void {
    if (pollTimer || stopped) return
    pollTimer = setInterval(() => void poll(), RESCUE_SESSION_POLL_INTERVAL_MS)
  }

  function stopPolling(): void {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
    polling = false
  }

  async function poll(): Promise<void> {
    if (polling) return
    polling = true
    try {
      await options.onPollRefresh()
    } catch {
      // 轮询容错：失败等下一拍（同 detail.vue refreshDetail 惯例）
    } finally {
      polling = false
    }
  }

  function fallbackToPolling(): void {
    usingSocket.value = false
    startPolling()
  }

  function connect(): void {
    socket?.close()
    try {
      socket = connectRescueSocket(options.getHelpId(), {
        onEnvelope: options.onEnvelope,
        onClose: fallbackToPolling,
      })
      usingSocket.value = true
    } catch (error) {
      console.warn('[SessionRealtime] WS 建立失败，降级轮询', error)
      fallbackToPolling()
    }
  }

  function start(): void {
    if (socket || pollTimer) return
    stopped = false
    connect()
  }

  function stop(): void {
    stopped = true
    stopPolling()
    socket?.close()
    socket = null
    usingSocket.value = false
  }

  onUnmounted(stop)

  return { usingSocket, start, stop }
}
