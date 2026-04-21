/**
 * 急救员中心相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.4
 *
 * GET /api/rescuers/nearby          — 获取当前校区急救员列表 [登录]
 * GET /api/rescuers/:id             — 获取急救员联络详情 [登录]
 * GET /api/rescuers/me/availability — 获取我的运行态配置 [登录 + 急救员]
 * PUT /api/rescuers/me/availability — 设置我的限时离线窗口 [登录 + 急救员]
 */
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
import { USE_MOCK } from '@/utils/runtime-env'

/** 获取当前校区急救员列表 */
export function fetchNearbyRescuers(
  params: NearbyRescuerParams,
): Promise<RescuerListResponse> {
  if (USE_MOCK) {
    return Promise.resolve(queryMockNearbyRescuers(params))
  }

  return get<RescuerListResponse>(
    '/api/rescuers/nearby',
    params as unknown as Record<string, unknown>,
  )
}

/** 获取急救员联络详情 */
export function fetchRescuerDetail(rescuerId: string): Promise<RescuerDetail> {
  if (USE_MOCK) {
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
