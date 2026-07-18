import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const authMocks = vi.hoisted(() => ({ getToken: vi.fn() }))

vi.mock('@/services/auth-session', () => authMocks)
vi.mock('@/services/request-config', () => ({ BASE_URL: 'https://api.example.test' }))

import {
  closeAllRescueSockets,
  connectRescueSocket,
} from '@/services/rescue-socket'

interface SocketCallbacks {
  open?: () => void
  close?: () => void
  error?: (error: unknown) => void
  message?: (result: { data: string | ArrayBuffer }) => void
}

function installSocket() {
  const callbacks: SocketCallbacks = {}
  const task = {
    onOpen: vi.fn((callback: () => void) => { callbacks.open = callback }),
    onClose: vi.fn((callback: () => void) => { callbacks.close = callback }),
    onError: vi.fn((callback: (error: unknown) => void) => { callbacks.error = callback }),
    onMessage: vi.fn((callback: (result: { data: string | ArrayBuffer }) => void) => {
      callbacks.message = callback
    }),
    close: vi.fn(),
  }
  const connectSocket = vi.fn().mockReturnValue(task)
  vi.stubGlobal('uni', { connectSocket })
  return { callbacks, connectSocket, task }
}

describe('connectRescueSocket', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    authMocks.getToken.mockReset().mockReturnValue('opaque-token')
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
  })

  afterEach(() => {
    closeAllRescueSockets()
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('passes the token in Authorization header instead of the URL', () => {
    const { connectSocket } = installSocket()

    connectRescueSocket('help/1', { onEnvelope: vi.fn() })

    expect(connectSocket).toHaveBeenCalledWith({
      url: 'wss://api.example.test/ws/help/help%2F1/session',
      header: { Authorization: 'Bearer opaque-token' },
    })
  })

  it('reports asynchronous socket errors exactly once', () => {
    const { callbacks } = installSocket()
    const onUnavailable = vi.fn()
    connectRescueSocket('help-1', { onEnvelope: vi.fn(), onUnavailable })

    callbacks.error?.(new Error('handshake failed with secret-token'))
    callbacks.close?.()

    expect(onUnavailable).toHaveBeenCalledTimes(1)
    expect(console.warn).toHaveBeenCalledWith('[RescueSocket] 连接错误', {
      event: 'socket_error',
      wasConnected: false,
    })
    expect(JSON.stringify(vi.mocked(console.warn).mock.calls)).not.toContain('secret-token')
  })

  it('reports invalid frames for HTTP catch-up without exposing raw contents', () => {
    const { callbacks } = installSocket()
    const onEnvelope = vi.fn()
    const onInvalidFrame = vi.fn()
    connectRescueSocket('help-1', { onEnvelope, onInvalidFrame })

    callbacks.message?.({ data: 'private rescue payload' })

    expect(onEnvelope).not.toHaveBeenCalled()
    expect(onInvalidFrame).toHaveBeenCalledWith('malformed_json')
    expect(console.warn).toHaveBeenCalledWith('[RescueSocket] 推送帧校验失败', {
      event: 'frame_validation_failed',
      reason: 'malformed_json',
      dataType: 'string',
      dataLength: 22,
    })
    expect(JSON.stringify(vi.mocked(console.warn).mock.calls)).not.toContain(
      'private rescue payload',
    )
  })

  it('rejects a parseable frame whose payload is incomplete', () => {
    const { callbacks } = installSocket()
    const onEnvelope = vi.fn()
    const onInvalidFrame = vi.fn()
    connectRescueSocket('help-1', { onEnvelope, onInvalidFrame })

    callbacks.message?.({ data: JSON.stringify({ type: 'participant', data: {} }) })

    expect(onEnvelope).not.toHaveBeenCalled()
    expect(onInvalidFrame).toHaveBeenCalledWith('invalid_envelope')
  })

  it('times out a handshake that never opens', () => {
    const { task } = installSocket()
    const onUnavailable = vi.fn()
    connectRescueSocket('help-1', { onEnvelope: vi.fn(), onUnavailable })

    vi.advanceTimersByTime(10_000)

    expect(task.close).toHaveBeenCalledTimes(1)
    expect(onUnavailable).toHaveBeenCalledTimes(1)
  })

  it('does not report an intentional close as unavailable', () => {
    const { callbacks } = installSocket()
    const onUnavailable = vi.fn()
    const handle = connectRescueSocket('help-1', { onEnvelope: vi.fn(), onUnavailable })

    handle.close()
    callbacks.close?.()

    expect(onUnavailable).not.toHaveBeenCalled()
  })

  it('closes every healthy connection when authentication ends', () => {
    const first = installSocket()
    connectRescueSocket('help-1', { onEnvelope: vi.fn() })
    first.callbacks.open?.()
    const second = installSocket()
    connectRescueSocket('help-2', { onEnvelope: vi.fn() })
    second.callbacks.open?.()

    closeAllRescueSockets()

    expect(first.task.close).toHaveBeenCalledTimes(1)
    expect(second.task.close).toHaveBeenCalledTimes(1)
  })
})
