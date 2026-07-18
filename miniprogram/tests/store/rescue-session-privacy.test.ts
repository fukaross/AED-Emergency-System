import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RescueMessage, RescueSessionSnapshot } from '@/types/rescue-session'
import { buildMessage, buildSnapshot, deferred } from './rescue-session-test-fixtures'

const apiMocks = vi.hoisted(() => ({
  joinRescueSession: vi.fn(),
  fetchRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => apiMocks)

import { useRescueSessionStore } from '@/store/rescue-session'

describe('rescue-session store location privacy', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(apiMocks).forEach((mock) => mock.mockReset())
  })

  it('derives signal age from reportedAt and removes expired coordinates', async () => {
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-07-17T10:00:01.000Z'))
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:01:00.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('lost')
    expect(store.snapshot?.participants[0].location).not.toBeNull()

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:01:31.000Z'))
    expect(store.snapshot?.participants[0]).toMatchObject({ signal: 'gone', location: null })
  })

  it('calibrates TTL against snapshot.serverTime when the device clock drifts', async () => {
    const localJoinTime = Date.parse('2026-07-17T12:00:01.000Z')
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(localJoinTime)
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()

    store.refreshParticipantSignals(Date.parse('2026-07-17T12:00:31.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('ok')

    store.refreshParticipantSignals(Date.parse('2026-07-17T12:01:01.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('lost')
  })

  it('preserves snapshot and participant identities while signal state is unchanged', async () => {
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-07-17T10:00:01.000Z'))
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()
    const initialSnapshot = store.snapshot
    const initialParticipant = store.snapshot?.participants[0]

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:00:30.000Z'))

    expect(store.snapshot).toBe(initialSnapshot)
    expect(store.snapshot?.participants[0]).toBe(initialParticipant)
  })

  it('uses the required snapshot timing instead of client defaults', async () => {
    const joinedAt = Date.parse('2026-07-17T10:00:01.000Z')
    const nowSpy = vi.spyOn(Date, 'now').mockReturnValue(joinedAt)
    apiMocks.joinRescueSession.mockResolvedValueOnce({
      ...buildSnapshot('help-a'),
      timing: {
        signalLostAfterSeconds: 10,
        locationGoneAfterSeconds: 20,
        locationReportIntervalSeconds: 2,
      },
    })
    const store = useRescueSessionStore()
    await store.joinSession('help-a')
    nowSpy.mockRestore()

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:00:12.000Z'))
    expect(store.snapshot?.participants[0].signal).toBe('lost')

    store.refreshParticipantSignals(Date.parse('2026-07-17T10:00:22.000Z'))
    expect(store.snapshot?.participants[0]).toMatchObject({ signal: 'gone', location: null })
  })

  it('clears every exact coordinate when the final socket terminal frame arrives', async () => {
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })

    expect(store.sessionClosed).toBe(true)
    expect(store.terminalHinted).toBe(true)
    expect(store.snapshot?.participants[0]).toMatchObject({ signal: 'gone', location: null })
  })

  it('does not let an in-flight active refresh reopen a closed session', async () => {
    const refresh = deferred<RescueSessionSnapshot>()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.fetchRescueSession.mockReturnValueOnce(refresh.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingRefresh = store.refreshSession('help-a')
    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })
    refresh.resolve(buildSnapshot('help-a'))
    await pendingRefresh

    expect(store.sessionClosed).toBe(true)
    expect(store.terminalHinted).toBe(true)
    expect(store.snapshot?.participants[0].location).toBeNull()
    expect(store.snapshot?.helpStatus).toBe('closed')
    expect(store.snapshot?.participants[0].location).toBeNull()
  })

  it('ignores late message and participant results after closure', async () => {
    const message = deferred<RescueMessage>()
    apiMocks.joinRescueSession.mockResolvedValueOnce(buildSnapshot('help-a'))
    apiMocks.postRescueMessage.mockReturnValueOnce(message.promise)
    const store = useRescueSessionStore()
    await store.joinSession('help-a')

    const pendingMessage = store.sendMessage('马上到达')
    store.applySocketEnvelope('help-a', {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })
    store.applySocketEnvelope('help-a', {
      type: 'participant',
      data: buildSnapshot('help-a').participants[0],
    })
    message.resolve(buildMessage(2))
    await pendingMessage

    expect(store.snapshot?.messages).toHaveLength(1)
    expect(store.snapshot?.participants[0].location).toBeNull()
  })
})
