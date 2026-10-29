import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const apiMocks = vi.hoisted(() => ({
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => ({
  fetchMineRescueSessions: apiMocks.fetchMineRescueSessions,
  fetchRescueSession: vi.fn(),
  joinRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueImageMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
}))

import { useRescueSessionStore } from '@/store/rescue-session'

describe('rescue session discovery reset', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    apiMocks.fetchMineRescueSessions.mockReset()
  })

  it('removes cached session addresses and unread state after access is forbidden', async () => {
    apiMocks.fetchMineRescueSessions.mockResolvedValueOnce([{
      helpId: 'help-1',
      requestNo: 'REQ-1',
      helpStatus: 'responding',
      type: 'help_hand',
      address: '敏感求助地址',
      joined: true,
      joinedAt: '2026-07-18T10:00:00.000Z',
      unreadCount: 2,
      lastActivityAt: null,
    }])
    const store = useRescueSessionStore()
    await store.loadMineSessions('user-a')

    store.resetMineSessions()

    expect(store.mineItems).toEqual([])
    expect(store.mineLoaded).toBe(false)
    expect(store.totalUnread).toBe(0)
  })

  it('clears the previous owner before the new owner response and rejects late data', async () => {
    let resolveOld = (_items: unknown[]) => undefined
    let resolveNew = (_items: unknown[]) => undefined
    const oldResponse = new Promise<unknown[]>((resolve) => { resolveOld = resolve })
    const newResponse = new Promise<unknown[]>((resolve) => { resolveNew = resolve })
    apiMocks.fetchMineRescueSessions
      .mockReturnValueOnce(oldResponse)
      .mockReturnValueOnce(newResponse)
    const store = useRescueSessionStore()

    const oldRequest = store.loadMineSessions('user-a')
    const newRequest = store.loadMineSessions('user-b')

    expect(store.mineItems).toEqual([])
    resolveNew([buildMineItem('help-b', '新账号地址')])
    await newRequest
    resolveOld([buildMineItem('help-a', '旧账号敏感地址')])
    await oldRequest

    expect(store.mineItems).toEqual([
      expect.objectContaining({ helpId: 'help-b', address: '新账号地址' }),
    ])
  })
})

function buildMineItem(helpId: string, address: string) {
  return {
    helpId,
    requestNo: `REQ-${helpId}`,
    helpStatus: 'responding',
    type: 'help_hand',
    address,
    joined: true,
    joinedAt: '2026-07-18T10:00:00.000Z',
    unreadCount: 1,
    lastActivityAt: null,
  }
}
