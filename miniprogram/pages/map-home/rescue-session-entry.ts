import type { RescueMineItem } from '@/types/rescue-session'

export function getVisibleRescueSessions(
  loggedIn: boolean,
  certified: boolean,
  items: readonly RescueMineItem[],
): readonly RescueMineItem[] {
  return loggedIn && certified ? items : []
}

export function getSessionUnreadText(item: RescueMineItem): string {
  if (!item.joined || item.unreadCount <= 0) return ''
  return item.unreadCount > 99 ? '99+' : String(item.unreadCount)
}
