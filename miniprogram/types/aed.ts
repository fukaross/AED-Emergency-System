/**
 * AED 设备相关类型定义
 *
 * 来源：CLAUDE.md §8.1 (GET /api/aed/nearby, GET /api/aed/:id, POST /api/aed/:id/report)
 * 来源：BackEnd.md §4.3
 */

/** AED 设备状态 */
export type AedStatus = 'online' | 'offline'

/** AED 设备列表项 (nearby 接口返回) */
export interface AedItem {
  readonly id: string
  readonly name: string
  readonly lat: number
  readonly lng: number
  readonly status: AedStatus
  readonly distance?: number
  readonly image: string
  readonly address: string
  readonly lastMaintenance: string
  readonly nextMaintenance: string
  readonly maintainer: string
}

/** 附近 AED 请求参数 */
export interface NearbyAedParams {
  readonly lat: number
  readonly lng: number
  readonly radius?: number
}

/** AED 问题报告类型 */
export type AedReportType = 'damaged' | 'relocated' | 'unusable' | 'other'

/** AED 问题报告请求 */
export interface AedReportRequest {
  readonly type: AedReportType
  readonly description: string
  readonly images?: readonly string[]
  readonly contactPhone?: string
  readonly contactEmail?: string
}
