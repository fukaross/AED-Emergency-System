// 急救员中心相关的接口 —— 附近列表、详情、运行态配置
import { get, put } from '@/services/request'
import {
  getMockMyRescuerAvailability,
  getMockRescuerDetail,
  queryMockNearbyRescuers,
  updateMockMyRescuerAvailability,
} from '@/mock/rescuer'
import type {
  NearbyRescuerParams,
  RescuerAvailabilitySetting,
  RescuerDetail,
  RescuerListResponse,
  UpdateRescuerAvailabilityPayload,
} from '@/types/rescuer'
import {
  RESCUER_BACKEND_READY,
  USE_MOCK,
  USE_PUBLIC_DEMO_DATA,
} from '@/utils/runtime-env'

/** 获取当前校区急救员列表 */
export function fetchNearbyRescuers(
  params: NearbyRescuerParams,
): Promise<RescuerListResponse> {
  if (!RESCUER_BACKEND_READY && USE_PUBLIC_DEMO_DATA) {
    return Promise.resolve(queryMockNearbyRescuers(params))
  }

  return get<RescuerListResponse>(
    '/api/rescuers/nearby',
    params as unknown as Record<string, unknown>,
  )
}

/** 获取急救员联络详情 */
export function fetchRescuerDetail(rescuerId: string): Promise<RescuerDetail> {
  if (!RESCUER_BACKEND_READY && USE_PUBLIC_DEMO_DATA) {
    const detail = getMockRescuerDetail(rescuerId)
    if (!detail) {
      return Promise.reject(new Error('急救员不存在'))
    }

    return Promise.resolve(detail)
  }

  return get<RescuerDetail>(`/api/rescuers/${rescuerId}`)
}

/** 获取当前急救员自己的运行态配置 */
export function fetchMyRescuerAvailability(): Promise<RescuerAvailabilitySetting> {
  if (USE_MOCK) {
    return Promise.resolve(getMockMyRescuerAvailability())
  }

  return get<RescuerAvailabilitySetting>('/api/rescuers/me/availability')
}

/** 设置当前急救员的限时离线窗口 */
export function updateMyRescuerAvailability(
  payload: UpdateRescuerAvailabilityPayload,
): Promise<RescuerAvailabilitySetting> {
  if (USE_MOCK) {
    return Promise.resolve(updateMockMyRescuerAvailability(payload))
  }

  return put<RescuerAvailabilitySetting>(
    '/api/rescuers/me/availability',
    payload as unknown as Record<string, unknown>,
  )
}
