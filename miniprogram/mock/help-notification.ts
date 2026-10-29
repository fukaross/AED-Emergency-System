// 求助订阅消息的 Mock 派发 —— 只生成「演示未发送」记录，绝不调用真实微信订阅消息。
import { CAMPUSES, type CampusCode } from '@/config/site-config'
import { queryMockNearbyRescuers } from '@/mock/rescuer'
import type { PublishHelpPayload } from '@/types/help'
import { maskPhone } from '@/utils/format'

const MOCK_HELP_NOTIFICATION_STORAGE_KEY = 'aed_mock_help_notifications'
const MOCK_CURRENT_USER_ID = 'demo-openid-requester'
const DEMO_TEMPLATE_ID = 'demo-help-subscribe-template'
const DEMO_PHONE_PREFIX = '130000002'

export type MockHelpRecipientType =
  | 'clinic'
  | 'counselor'
  | 'rescuer'
  | 'duty_teacher'

export interface MockHelpNotificationTarget {
  readonly id: string
  readonly type: MockHelpRecipientType
  readonly name: string
  readonly phone: string
  readonly userId?: string
  readonly campus?: CampusCode
  readonly distance?: number
}

export interface MockHelpNotificationRecord {
  readonly id: string
  readonly helpRequestId: string
  readonly requestNo: string
  readonly recipientType: MockHelpRecipientType
  readonly recipientName: string
  readonly recipientUserId?: string
  readonly phoneMasked: string
  readonly templateId: string
  readonly deliveryChannel: 'demo_local'
  readonly deliveryStatus: 'demo_not_sent'
  readonly createdAt: string
}

interface PublishContext {
  readonly helpRequestId: string
  readonly requestNo: string
  readonly payload: PublishHelpPayload
}

function buildDemoPhone(offset: number): string {
  return `${DEMO_PHONE_PREFIX}${String(offset).padStart(2, '0')}`
}

/** 校医室 / 值班老师按 config/site.json 的校区列表生成，不写死校区清单。 */
function buildCampusTargets(
  type: 'clinic' | 'duty_teacher',
): readonly MockHelpNotificationTarget[] {
  return CAMPUSES.map((campus, index) => ({
    id: `${type}-${campus.code}`,
    type,
    name: type === 'clinic' ? `${campus.name}校医室（演示）` : `${campus.name}值班老师（演示）`,
    phone: buildDemoPhone(index + (type === 'clinic' ? 1 : 11)),
    userId: `demo-${type}-${campus.code}`,
    campus: campus.code,
  }))
}

const MOCK_COUNSELORS: readonly MockHelpNotificationTarget[] = [
  {
    id: 'counselor-demo',
    type: 'counselor',
    name: '演示辅导员',
    phone: buildDemoPhone(21),
    userId: 'demo-counselor',
  },
]

export function dispatchMockHelpNotifications(
  context: PublishContext,
): readonly MockHelpNotificationRecord[] {
  const targets = resolveMockHelpNotificationTargets(context.payload)
  const records = targets.map((target, index) =>
    toNotificationRecord(context, target, index),
  )
  saveMockHelpNotifications([...loadAllMockHelpNotifications(), ...records])

  console.info(
    `[Demo Help Notification] 演示未发送：仅本地记录 ${records.length} 个接收目标，未调用微信订阅消息`,
    {
      requestNo: context.requestNo,
      recipientTypes: records.map((item) => item.recipientType),
    },
  )

  return records
}

export function listMockHelpNotifications(
  requestNo: string,
): readonly MockHelpNotificationRecord[] {
  return loadAllMockHelpNotifications().filter(
    (item) => item.requestNo === requestNo,
  )
}

export function clearMockHelpNotifications(): void {
  try {
    uni.removeStorageSync(MOCK_HELP_NOTIFICATION_STORAGE_KEY)
  } catch {
    console.warn('[Demo Help Notification] 清理通知记录失败')
  }
}

export function dedupeMockHelpNotificationTargets(
  targets: readonly MockHelpNotificationTarget[],
): readonly MockHelpNotificationTarget[] {
  const seen = new Set<string>()
  return targets.filter((target) => {
    const key = target.userId ? `user:${target.userId}` : `phone:${target.phone}`
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}

function resolveMockHelpNotificationTargets(
  payload: PublishHelpPayload,
): readonly MockHelpNotificationTarget[] {
  return dedupeMockHelpNotificationTargets([
    ...resolveCampusScopedTargets(payload, buildCampusTargets('clinic')),
    ...resolveCounselorTargets(payload),
    ...resolveNearbyRescuerTargets(payload),
    ...resolveCampusScopedTargets(payload, buildCampusTargets('duty_teacher')),
  ])
}

function resolveHelpCampus(payload: PublishHelpPayload): CampusCode {
  return queryMockNearbyRescuers({ lat: payload.lat, lng: payload.lng }).campus
}

function resolveCampusScopedTargets(
  payload: PublishHelpPayload,
  targets: readonly MockHelpNotificationTarget[],
): readonly MockHelpNotificationTarget[] {
  const campus = resolveHelpCampus(payload)
  return targets.filter((target) => target.campus === campus)
}

function resolveCounselorTargets(
  payload: PublishHelpPayload,
): readonly MockHelpNotificationTarget[] {
  if (payload.requestChannel === 'guest') return []
  return MOCK_COUNSELORS.map((item) => ({
    ...item,
    id: `${item.id}-${MOCK_CURRENT_USER_ID}`,
  }))
}

function resolveNearbyRescuerTargets(
  payload: PublishHelpPayload,
): readonly MockHelpNotificationTarget[] {
  const result = queryMockNearbyRescuers({ lat: payload.lat, lng: payload.lng })
  return result.items.map((item) => ({
    id: item.id,
    type: 'rescuer',
    name: item.name,
    phone: item.phoneMasked,
    userId: `demo-${item.id}`,
    distance: item.distance,
  }))
}

function toNotificationRecord(
  context: PublishContext,
  target: MockHelpNotificationTarget,
  index: number,
): MockHelpNotificationRecord {
  return {
    id: `${context.helpRequestId}-notify-${index + 1}`,
    helpRequestId: context.helpRequestId,
    requestNo: context.requestNo,
    recipientType: target.type,
    recipientName: target.name,
    recipientUserId: target.userId,
    phoneMasked: maskPhone(target.phone),
    templateId: DEMO_TEMPLATE_ID,
    deliveryChannel: 'demo_local',
    deliveryStatus: 'demo_not_sent',
    createdAt: new Date().toISOString(),
  }
}

function loadAllMockHelpNotifications(): MockHelpNotificationRecord[] {
  try {
    const raw = uni.getStorageSync(MOCK_HELP_NOTIFICATION_STORAGE_KEY) as string
    return raw ? (JSON.parse(raw) as MockHelpNotificationRecord[]) : []
  } catch {
    return []
  }
}

function saveMockHelpNotifications(
  records: readonly MockHelpNotificationRecord[],
): void {
  try {
    uni.setStorageSync(
      MOCK_HELP_NOTIFICATION_STORAGE_KEY,
      JSON.stringify(records),
    )
  } catch {
    console.warn('[Demo Help Notification] 保存通知记录失败')
  }
}
