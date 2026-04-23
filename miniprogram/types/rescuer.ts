/**
 * 急救员中心相关类型定义
 *
 * 来源：CLAUDE.md §4.2 / §8.1
 * 来源：BackEnd.md §3.2.15 / §4.4
 */
import type { GeoPoint } from '@/types/common'
import type { RescuerLevel } from '@/types/user'

export type CampusCode = 'guilinyang' | 'fucheng'

export type RescuerAvailabilityStatus = 'online' | 'offline'

export type RescuerSkillTag = 'cpr' | 'aed' | 'stb'

export interface CampusCenter extends GeoPoint {
  readonly name: string
}

export interface RescuerSkillFlags {
  readonly cpr: boolean
  readonly aed: boolean
  readonly stb: boolean
}

export interface NearbyRescuerParams {
  readonly lat: number
  readonly lng: number
}

export interface RescuerListItem {
  readonly id: string
  readonly name: string
  readonly avatarUrl: string
  readonly campus: CampusCode
  readonly campusName: string
  readonly level: RescuerLevel | null
  readonly availabilityStatus: RescuerAvailabilityStatus
  readonly qualificationStatus: 'active' | 'inactive'
  readonly distance: number
  readonly phoneMasked: string
  readonly skills: RescuerSkillFlags
}

export interface RescuerListResponse {
  readonly campus: CampusCode
  readonly campusName: string
  readonly viewerCampusDistance: number
  readonly autoOfflineRadius: number
  readonly items: readonly RescuerListItem[]
}

export interface RescuerDetail {
  readonly id: string
  readonly name: string
  readonly avatarUrl: string
  readonly campus: CampusCode
  readonly campusName: string
  readonly level: RescuerLevel | null
  readonly availabilityStatus: RescuerAvailabilityStatus
  readonly qualificationStatus: 'active' | 'inactive'
  readonly phone: string
  readonly phoneMasked: string
  readonly certificateNoMasked: string | null
  readonly workUnit: string | null
  readonly skills: RescuerSkillFlags
}

export interface RescuerAvailabilitySetting {
  readonly campus: CampusCode
  readonly campusName: string
  readonly availabilityStatus: RescuerAvailabilityStatus
  readonly autoOfflineRadius: number
  readonly maxManualOfflineHours: number
  readonly manualOfflineFrom: string | null
  readonly manualOfflineUntil: string | null
  readonly autoOfflineReason: 'out_of_service_radius' | null
}

export interface UpdateRescuerAvailabilityPayload {
  readonly manualOfflineUntil: string | null
}
