// 急救会话信号状态计算 —— 与后端 RescueSessionProjection 同一规则（BackEnd.md §4.5A）
import type { RescueSignal } from '@/types/rescue-session'

export const RESCUE_SIGNAL_LOST_AFTER_SECONDS = 45
export const RESCUE_SIGNAL_GONE_AFTER_SECONDS = 90
export const RESCUE_LOCATION_REPORT_INTERVAL_MS = 5000
export const RESCUE_SESSION_POLL_INTERVAL_MS = 3000
export const RESCUE_VIEW_POLL_INTERVAL_MS = 8000
export const RESCUE_MINE_POLL_INTERVAL_MS = 15000

const MS_PER_SECOND = 1000

/** lastSeenAt 距今 ≤45s 为 ok，≤90s 为 lost（灰显），>90s 为 gone（地图移除） */
export function computeSignal(
  lastSeenAt: string,
  now: number,
  lostAfterSeconds = RESCUE_SIGNAL_LOST_AFTER_SECONDS,
  goneAfterSeconds = RESCUE_SIGNAL_GONE_AFTER_SECONDS,
): RescueSignal {
  const elapsedSeconds = (now - Date.parse(lastSeenAt)) / MS_PER_SECOND
  if (elapsedSeconds <= lostAfterSeconds) return 'ok'
  if (elapsedSeconds <= goneAfterSeconds) return 'lost'
  return 'gone'
}
