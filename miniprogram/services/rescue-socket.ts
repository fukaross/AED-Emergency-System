// 急救会话 WS 推送通道（ADR 0001：仅扇出，权威数据以 HTTP 为准；断线由页面降级轮询）
import { getToken } from '@/services/auth-session'
import { BASE_URL } from '@/services/request-config'
import type { RescueSocketEnvelope } from '@/types/rescue-session'

export interface RescueSocketHandlers {
  readonly onEnvelope: (envelope: RescueSocketEnvelope) => void
  readonly onOpen?: () => void
  readonly onUnavailable?: (reason?: unknown) => void
}

export interface RescueSocketHandle {
  close(): void
  isConnected(): boolean
}

function toWebSocketBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/^http/i, 'ws')
}

const SOCKET_OPEN_TIMEOUT_MS = 8000

/** 建立会话推送连接；token 缺失时直接抛错（不静默降级，由调用方决定回退轮询） */
export function connectRescueSocket(
  helpId: string,
  handlers: RescueSocketHandlers,
): RescueSocketHandle {
  const token = getToken()
  if (!token) throw new Error('缺少登录凭证，无法建立会话推送通道')
  return new RescueSocketConnection(helpId, token, handlers)
}

class RescueSocketConnection implements RescueSocketHandle {
  private connected = false
  private closedByClient = false
  private unavailableNotified = false
  private taskClosed = false
  private readonly task: UniNamespace.SocketTask
  private readonly openTimer: ReturnType<typeof setTimeout>

  constructor(
    helpId: string,
    token: string,
    private readonly handlers: RescueSocketHandlers,
  ) {
    const url = `${toWebSocketBaseUrl(BASE_URL)}/ws/help/${encodeURIComponent(helpId)}/session`
    // promisify 补丁把返回类型声明为 Promise，运行时实际同步返回 SocketTask
    this.task = uni.connectSocket({
      url,
      header: { Authorization: `Bearer ${token}` },
    }) as unknown as UniNamespace.SocketTask
    this.openTimer = setTimeout(
      () => this.notifyUnavailable(new Error('WebSocket 握手超时')),
      SOCKET_OPEN_TIMEOUT_MS,
    )
    this.bindHandlers()
  }

  close(): void {
    if (this.closedByClient) return
    this.closedByClient = true
    this.connected = false
    clearTimeout(this.openTimer)
    this.closeTask()
  }

  isConnected(): boolean {
    return this.connected
  }

  private bindHandlers(): void {
    this.task.onOpen(() => this.handleOpen())
    this.task.onMessage((result) => this.handleMessage(result.data))
    this.task.onClose(() => {
      this.taskClosed = true
      this.connected = false
      this.notifyUnavailable()
    })
    this.task.onError((error) => {
      console.warn('[RescueSocket] 连接错误', {
        event: 'socket_error',
        wasConnected: this.connected,
      })
      this.notifyUnavailable(error)
    })
  }

  private handleOpen(): void {
    if (this.closedByClient || this.unavailableNotified) return
    clearTimeout(this.openTimer)
    this.connected = true
    this.handlers.onOpen?.()
  }

  private handleMessage(data: string | ArrayBuffer): void {
    if (typeof data !== 'string' || !data) return
    try {
      this.handlers.onEnvelope(JSON.parse(data) as RescueSocketEnvelope)
    } catch {
      console.warn('[RescueSocket] 推送帧解析失败', {
        event: 'frame_parse_failed',
        dataType: typeof data,
        dataLength: data.length,
      })
    }
  }

  private notifyUnavailable(reason?: unknown): void {
    if (this.closedByClient || this.unavailableNotified) return
    this.unavailableNotified = true
    this.connected = false
    clearTimeout(this.openTimer)
    this.closeTask()
    this.handlers.onUnavailable?.(reason)
  }

  private closeTask(): void {
    if (this.taskClosed) return
    this.taskClosed = true
    this.task.close({})
  }
}
