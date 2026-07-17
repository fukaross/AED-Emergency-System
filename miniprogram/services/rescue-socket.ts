// 急救会话 WS 推送通道（ADR 0001：仅扇出，权威数据以 HTTP 为准；断线由页面降级轮询）
import { getToken } from '@/services/auth-session'
import { BASE_URL } from '@/services/request-config'
import type { RescueSocketEnvelope } from '@/types/rescue-session'

export interface RescueSocketHandlers {
  readonly onEnvelope: (envelope: RescueSocketEnvelope) => void
  readonly onClose?: () => void
}

export interface RescueSocketHandle {
  close(): void
  isConnected(): boolean
}

function toWebSocketBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/^http/i, 'ws')
}

/** 建立会话推送连接；token 缺失时直接抛错（不静默降级，由调用方决定回退轮询） */
export function connectRescueSocket(
  helpId: string,
  handlers: RescueSocketHandlers,
): RescueSocketHandle {
  const token = getToken()
  if (!token) {
    throw new Error('缺少登录凭证，无法建立会话推送通道')
  }

  const url = `${toWebSocketBaseUrl(BASE_URL)}/ws/help/${helpId}/session?token=${encodeURIComponent(token)}`
  let connected = false
  // promisify 补丁把返回类型声明为 Promise，运行时实际同步返回 SocketTask
  const task = uni.connectSocket({ url }) as unknown as UniNamespace.SocketTask

  task.onOpen(() => {
    connected = true
  })
  task.onMessage((result) => {
    if (typeof result.data !== 'string' || !result.data) return
    try {
      handlers.onEnvelope(JSON.parse(result.data) as RescueSocketEnvelope)
    } catch {
      console.warn('[RescueSocket] 无法解析的推送帧', result.data)
    }
  })
  task.onClose(() => {
    connected = false
    handlers.onClose?.()
  })
  task.onError((error) => {
    connected = false
    console.warn('[RescueSocket] 连接错误', error)
  })

  return {
    close() {
      connected = false
      task.close({})
    },
    isConnected: () => connected,
  }
}
