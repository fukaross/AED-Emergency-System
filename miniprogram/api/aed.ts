/**
 * AED 设备相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.3
 *
 * GET  /api/aed/nearby — 获取附近 AED 列表 [公开]
 * GET  /api/aed/:id    — 获取单个 AED 详情 [公开]
 * POST /api/aed/:id/report — 提交问题报告 [登录可选]
 */
import { get, post } from '@/services/request'
import type { AedItem, NearbyAedParams, AedReportRequest } from '@/types/aed'
import { AED_MOCK_DATA } from '@/mock/aed'
import { calculateDistance } from '@/services/geo-service'
import {
  USE_MOCK,
  IS_DEV_LIKE,
  shouldWarnPlaceholderApiBaseUrl,
} from '@/utils/runtime-env'

let hasWarnedPlaceholderPublicApi = false

function buildMockNearbyAed(params: NearbyAedParams): AedItem[] {
  const userPoint = { lat: params.lat, lng: params.lng }

  return AED_MOCK_DATA
    .map((item) => ({
      ...item,
      distance: Math.round(
        calculateDistance(userPoint, { lat: item.lat, lng: item.lng }),
      ),
    }))
    .sort((a, b) => {
      if (a.status === 'online' && b.status !== 'online') return -1
      if (a.status !== 'online' && b.status === 'online') return 1
      return a.distance - b.distance
    })
}

function logMockUsage(action: string): void {
  if (shouldWarnPlaceholderApiBaseUrl() && !hasWarnedPlaceholderPublicApi) {
    console.warn('[Mock AED API] 检测到占位接口地址，公开内容已切换为 Mock 数据')
    hasWarnedPlaceholderPublicApi = true
  }
  if (!IS_DEV_LIKE || !USE_MOCK) return
  console.log(`[Mock AED API] ${action}`)
}

/** 获取附近 AED 列表 */
export function fetchNearbyAed(params: NearbyAedParams): Promise<AedItem[]> {
  if (USE_MOCK) {
    logMockUsage('fetchNearbyAed')
    return Promise.resolve(buildMockNearbyAed(params))
  }
  return get<AedItem[]>('/api/aed/nearby', params as unknown as Record<string, unknown>)
}

/** 获取单个 AED 详情 */
export function fetchAedDetail(id: string): Promise<AedItem> {
  if (USE_MOCK) {
    logMockUsage('fetchAedDetail')
    const found = AED_MOCK_DATA.find((item) => item.id === id)
    if (!found) {
      return Promise.reject(new Error('AED 设备不存在'))
    }
    return Promise.resolve({ ...found })
  }
  return get<AedItem>(`/api/aed/${id}`)
}

/** 提交 AED 问题报告 */
export function submitAedReport(
  aedId: string,
  payload: AedReportRequest,
): Promise<void> {
  if (USE_MOCK) {
    logMockUsage(`submitAedReport:${aedId}`)
    console.log('[Mock AED API] report payload', payload)
    return Promise.resolve()
  }
  return post(`/api/aed/${aedId}/report`, payload as unknown as Record<string, unknown>)
}
