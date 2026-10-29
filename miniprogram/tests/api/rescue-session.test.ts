import { beforeEach, describe, expect, it, vi } from 'vitest'

const storage = new Map<string, string>()

vi.mock('@/utils/runtime-env', () => ({
  APP_ENV: 'development',
  API_BASE_URL: 'http://localhost:5000',
  SESSION_BACKEND_READY: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  USE_REAL_LOCATION: false,
  USE_REAL_WECHAT_FLOW: false,
}))

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
})

import {
  fetchMineRescueSessions,
  fetchRescueSession,
  fetchRescueView,
  joinRescueSession,
  postRescueMessage,
  postRescueStatusEvent,
  reportRescueLocation,
} from '@/api/rescue-session'

describe('rescue-session api (mock face)', () => {
  beforeEach(() => {
    storage.clear()
  })

  it('joins a fresh session with an automatic joined event and responding transition', async () => {
    const snapshot = await joinRescueSession('test-help-x')

    expect(snapshot.helpStatus).toBe('responding')
    expect(snapshot.me.openId).toBe('demo-openid-rescuer-east')
    expect(snapshot.participants).toHaveLength(1)
    expect(snapshot.messages).toHaveLength(1)
    expect(snapshot.messages[0].eventType).toBe('joined')
    expect(snapshot.timing).toEqual({
      signalLostAfterSeconds: 45,
      locationGoneAfterSeconds: 90,
      locationReportIntervalSeconds: 5,
    })
  })

  it('keeps join idempotent without duplicate joined events', async () => {
    await joinRescueSession('test-help-x')
    const snapshot = await joinRescueSession('test-help-x')

    expect(snapshot.participants).toHaveLength(1)
    expect(snapshot.messages).toHaveLength(1)
  })

  it('posts messages and filters them with afterSeq', async () => {
    await joinRescueSession('test-help-x')
    const first = await postRescueMessage('test-help-x', '我到了楼下')
    const second = await postRescueMessage('test-help-x', '拿到 AED 了')

    const incremental = await fetchRescueSession('test-help-x', { afterSeq: first.seq })
    expect(incremental.messages).toHaveLength(1)
    expect(incremental.messages[0].seq).toBe(second.seq)
    expect(incremental.latestSeq).toBe(second.seq)
  })

  it('rejects invalid message text', async () => {
    await joinRescueSession('test-help-x')
    expect(() => postRescueMessage('test-help-x', '   ')).toThrow('1~500')
  })

  it('marks and revokes status events with debounce', async () => {
    await joinRescueSession('test-help-x')

    const marked = await postRescueStatusEvent('test-help-x', 'taken_aed', 'mark')
    expect(marked.appended).toBe(true)
    expect(marked.me.takenAed).toBe(true)

    const repeated = await postRescueStatusEvent('test-help-x', 'taken_aed', 'mark')
    expect(repeated.appended).toBe(false)
    expect(repeated.event).toBeNull()

    const revoked = await postRescueStatusEvent('test-help-x', 'taken_aed', 'revoke')
    expect(revoked.appended).toBe(true)
    expect(revoked.me.takenAed).toBe(false)
  })

  it('reports the latest location and exposes it in the snapshot', async () => {
    await joinRescueSession('test-help-x')
    await reportRescueLocation('test-help-x', { lat: 31.2315, lng: 121.4785 })

    const snapshot = await fetchRescueSession('test-help-x')
    const me = snapshot.participants.find(
      (item) => item.openId === 'demo-openid-rescuer-east',
    )
    expect(me?.location?.lat).toBeCloseTo(31.2315, 4)
    expect(me?.signal).toBe('ok')
  })

  it('provides an anonymized rescue view with an event timeline', async () => {
    await joinRescueSession('test-help-x')
    await postRescueStatusEvent('test-help-x', 'taken_aed', 'mark')

    const view = await fetchRescueView('test-help-x')
    expect(view.respondersCount).toBe(1)
    expect(view.participants[0]).not.toHaveProperty('openId')
    expect(view.participants[0].participantId).toBe('mock-participant-1')
    expect(view.events.map((item) => item.eventType)).toEqual(['joined', 'taken_aed'])
    expect(view.timing).toEqual({
      signalLostAfterSeconds: 45,
      locationGoneAfterSeconds: 90,
      locationReportIntervalSeconds: 5,
    })
  })

  it('tracks unread counts in mine list and clears them after a full fetch', async () => {
    await joinRescueSession('mock-help-1')

    const before = await fetchMineRescueSessions()
    expect(before).toHaveLength(1)
    expect(before[0].unreadCount).toBe(2)

    await fetchRescueSession('mock-help-1')
    const after = await fetchMineRescueSessions()
    expect(after[0].unreadCount).toBe(0)
  })

  it('lists every active session and distinguishes sessions not yet joined', async () => {
    const beforeJoin = await fetchMineRescueSessions()

    expect(beforeJoin).toHaveLength(1)
    expect(beforeJoin[0]).toMatchObject({
      helpId: 'mock-help-1',
      joined: false,
      joinedAt: null,
      unreadCount: 0,
    })

    await joinRescueSession('mock-help-1')
    const afterJoin = await fetchMineRescueSessions()

    expect(afterJoin[0].joined).toBe(true)
    expect(afterJoin[0].joinedAt).not.toBeNull()
    expect(afterJoin[0].unreadCount).toBe(2)
  })
})
