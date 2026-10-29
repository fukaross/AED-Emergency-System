// 急救员中心 Mock —— 运行态和资格状态分开维护；数据来自 demo 合成档案。
import {
  isOutsideServiceRadius,
  requireCampus,
  resolveNearestCampus,
  SERVICE_RADIUS_METERS,
  type CampusCode,
} from '@/config/site-config'
import { calculateDistance } from '@/services/geo-service'
import {
  INITIAL_MANUAL_OFFLINE_STATE,
  INITIAL_MOCK_RESCUER_NOW,
  type ManualOfflineState,
  MOCK_RESCUERS,
  type RescuerRecord,
} from '@/mock/rescuer-fixtures'
import { resolveCurrentRescuerProfile } from '@/mock/demo-fixtures'
import type {
  RescuerAvailabilitySetting,
  RescuerDetail,
  RescuerListItem,
  RescuerListResponse,
  NearbyRescuerParams,
  UpdateRescuerAvailabilityPayload,
} from '@/types/rescuer'
import { maskPhone } from '@/utils/format'
import { RESCUER_MAX_MANUAL_OFFLINE_HOURS, RESCUER_MAX_MANUAL_OFFLINE_MS } from '@/utils/constants'

let mockNow = INITIAL_MOCK_RESCUER_NOW

let manualOfflineState: ManualOfflineState | null = INITIAL_MANUAL_OFFLINE_STATE

function getNow(): number {
  return Date.parse(mockNow)
}

function getCampusName(campus: CampusCode): string {
  return requireCampus(campus).name
}

function getCampusDistance(lat: number, lng: number, campus: CampusCode): number {
  return Math.round(calculateDistance({ lat, lng }, requireCampus(campus).center))
}

function getCurrentRescuer(): RescuerRecord | null {
  const profile = resolveCurrentRescuerProfile()
  if (!profile) return null
  return MOCK_RESCUERS.find((record) => record.profileId === profile.id) ?? null
}

function isManualOfflineActive(state: ManualOfflineState | null, now: number): boolean {
  if (!state) return false
  return Date.parse(state.from) <= now && now < Date.parse(state.until)
}

function getAvailabilityStatus(record: RescuerRecord, now: number): 'online' | 'offline' {
  if (record.qualificationStatus !== 'active') return 'offline'

  const campusDistance = getCampusDistance(record.lat, record.lng, record.campus)
  if (isOutsideServiceRadius(campusDistance)) {
    return 'offline'
  }

  if (
    record.id === getCurrentRescuer()?.id &&
    isManualOfflineActive(manualOfflineState, now)
  ) {
    return 'offline'
  }

  return 'online'
}

function toListItem(
  record: RescuerRecord,
  userLat: number,
  userLng: number,
  now: number,
): RescuerListItem {
  return {
    id: record.id,
    name: record.name,
    avatarUrl: record.avatarUrl,
    campus: record.campus,
    campusName: getCampusName(record.campus),
    level: record.level,
    availabilityStatus: getAvailabilityStatus(record, now),
    qualificationStatus: record.qualificationStatus,
    distance: Math.round(
      calculateDistance(
        { lat: userLat, lng: userLng },
        { lat: record.lat, lng: record.lng },
      ),
    ),
    phoneMasked: maskPhone(record.phone),
    skills: { ...record.skills },
  }
}

function sortList(left: RescuerListItem, right: RescuerListItem): number {
  if (
    left.availabilityStatus === 'online' &&
    right.availabilityStatus !== 'online'
  ) {
    return -1
  }

  if (
    left.availabilityStatus !== 'online' &&
    right.availabilityStatus === 'online'
  ) {
    return 1
  }

  return left.distance - right.distance
}

function getRecordById(rescuerId: string): RescuerRecord | null {
  return MOCK_RESCUERS.find((item) => item.id === rescuerId) ?? null
}

export function queryMockNearbyRescuers(
  params: NearbyRescuerParams,
): RescuerListResponse {
  const now = getNow()
  const nearest = resolveNearestCampus({ lat: params.lat, lng: params.lng })
  const campus = nearest.campus.code
  const items = isOutsideServiceRadius(nearest.distanceMeters)
    ? []
    : MOCK_RESCUERS.filter(
        (item) =>
          item.campus === campus &&
          item.qualificationStatus === 'active',
      )
        .map((item) => toListItem(item, params.lat, params.lng, now))
        .sort(sortList)

  return {
    campus,
    campusName: nearest.campus.name,
    viewerCampusDistance: nearest.distanceMeters,
    autoOfflineRadius: SERVICE_RADIUS_METERS,
    items,
  }
}

export function getMockRescuerDetail(rescuerId: string): RescuerDetail | null {
  const record = getRecordById(rescuerId)
  if (!record) return null

  return {
    id: record.id,
    name: record.name,
    avatarUrl: record.avatarUrl,
    campus: record.campus,
    campusName: getCampusName(record.campus),
    level: record.level,
    availabilityStatus: getAvailabilityStatus(record, getNow()),
    qualificationStatus: record.qualificationStatus,
    phone: record.phone,
    phoneMasked: maskPhone(record.phone),
    certificateNoMasked: record.certificateNoMasked,
    workUnit: record.workUnit,
    skills: { ...record.skills },
  }
}

export function getMockMyRescuerAvailability(): RescuerAvailabilitySetting {
  const record = getCurrentRescuer()
  if (!record) {
    throw new Error('当前演示角色不是急救员，未提供运行态设置')
  }

  const autoOfflineReason = isOutsideServiceRadius(
    getCampusDistance(record.lat, record.lng, record.campus),
  )
    ? 'out_of_service_radius'
    : null

  return {
    campus: record.campus,
    campusName: getCampusName(record.campus),
    availabilityStatus: getAvailabilityStatus(record, getNow()),
    autoOfflineRadius: SERVICE_RADIUS_METERS,
    maxManualOfflineHours: RESCUER_MAX_MANUAL_OFFLINE_HOURS,
    manualOfflineFrom: manualOfflineState?.from ?? null,
    manualOfflineUntil: manualOfflineState?.until ?? null,
    autoOfflineReason,
  }
}

export function updateMockMyRescuerAvailability(
  payload: UpdateRescuerAvailabilityPayload,
): RescuerAvailabilitySetting {
  if (payload.manualOfflineUntil === null) {
    manualOfflineState = null
    return getMockMyRescuerAvailability()
  }

  const now = getNow()
  const targetUntil = Date.parse(payload.manualOfflineUntil)

  if (Number.isNaN(targetUntil)) {
    throw new Error('离线结束时间格式不正确')
  }

  if (targetUntil <= now) {
    throw new Error('离线结束时间必须晚于当前时间')
  }

  if (targetUntil - now > RESCUER_MAX_MANUAL_OFFLINE_MS) {
    throw new Error('单次离线时长不能超过 10 小时')
  }

  manualOfflineState = {
    from: new Date(now).toISOString(),
    until: payload.manualOfflineUntil,
  }

  return getMockMyRescuerAvailability()
}

export function __setMockRescuerNow(isoTime: string): void {
  mockNow = isoTime
}

export function __resetMockRescuerState(): void {
  mockNow = INITIAL_MOCK_RESCUER_NOW
  manualOfflineState = INITIAL_MANUAL_OFFLINE_STATE
}
