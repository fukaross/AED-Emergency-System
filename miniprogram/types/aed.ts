// AED 设备相关的类型

export type AedStatus = 'online' | 'offline'

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

export interface NearbyAedParams {
  readonly lat: number
  readonly lng: number
  readonly radius?: number
}

export type AedReportType = 'damaged' | 'relocated' | 'unusable' | 'other'

export interface AedReportRequest {
  readonly type: AedReportType
  readonly description: string
  readonly images?: readonly string[]
  readonly contactPhone?: string
  readonly contactEmail?: string
}
