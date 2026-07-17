import { describe, expect, it } from 'vitest'
import {
  getSessionUnreadText,
  getVisibleRescueSessions,
} from '@/pages/map-home/rescue-session-entry'
import type { RescueMineItem } from '@/types/rescue-session'

function buildItem(helpId: string, unreadCount: number, joined: boolean): RescueMineItem {
  return {
    helpId,
    requestNo: `REQ-${helpId}`,
    helpStatus: 'published',
    type: 'help_hand',
    address: `地点-${helpId}`,
    joined,
    joinedAt: joined ? '2026-07-18T10:00:00.000Z' : null,
    unreadCount,
    lastActivityAt: null,
  }
}

describe('map home rescue session entries', () => {
  it('keeps every active session visible for certified responders', () => {
    const items = [buildItem('joined', 3, true), buildItem('unjoined', 0, false)]

    expect(getVisibleRescueSessions(true, true, items)).toEqual(items)
    expect(getVisibleRescueSessions(false, true, items)).toEqual([])
    expect(getVisibleRescueSessions(true, false, items)).toEqual([])
  })

  it('formats unread per target session rather than summing unrelated sessions', () => {
    expect(getSessionUnreadText(buildItem('one', 3, true))).toBe('3')
    expect(getSessionUnreadText(buildItem('two', 120, true))).toBe('99+')
    expect(getSessionUnreadText(buildItem('new', 0, false))).toBe('')
  })
})
