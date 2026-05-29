// 生命体征相关的接口 —— 当前体征、心率趋势、异常告警
import { get, post } from '@/services/request'
import {
  createMockVitalAlert,
  getMockHeartRateTrends,
  getMockVitalSignsCurrent,
} from '@/mock/vitals'
import type {
  CreateVitalAlertPayload,
  HeartRateTrendList,
  HeartRateTrendParams,
  VitalAlertResult,
  VitalSignsCurrent,
} from '@/types/vitals'
import { USE_PUBLIC_DEMO_DATA } from '@/utils/runtime-env'

export function fetchCurrentVitalSigns(): Promise<VitalSignsCurrent> {
  if (USE_PUBLIC_DEMO_DATA) {
    return Promise.resolve(getMockVitalSignsCurrent())
  }
  return get<VitalSignsCurrent>('/api/vitals/current')
}

export function fetchHeartRateTrends(
  params?: HeartRateTrendParams,
): Promise<HeartRateTrendList> {
  if (USE_PUBLIC_DEMO_DATA) {
    return Promise.resolve(getMockHeartRateTrends(params))
  }
  return get<HeartRateTrendList>(
    '/api/vitals/heart-rate/trends',
    params as unknown as Record<string, unknown>,
  )
}

export function createVitalAlert(
  payload: CreateVitalAlertPayload,
): Promise<VitalAlertResult> {
  if (USE_PUBLIC_DEMO_DATA) {
    return Promise.resolve(createMockVitalAlert(payload))
  }
  return post<VitalAlertResult>(
    '/api/vitals/alerts',
    payload as unknown as Record<string, unknown>,
    { skipErrorHandler: true },
  )
}
