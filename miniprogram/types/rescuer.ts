// 急救员中心相关的类型
import type { RescuerLevel } from '@/types/user'
import type { CampusCode } from '@/config/site-config'

// 校区 code 由 config/site.json 派生（string），不是源码枚举；新增校区不用改类型。
export type { CampusCode }

export type RescuerAvailabilityStatus = 'online' | 'offline'

export type RescuerSkillTag = 'cpr' | 'aed' | 'stb'

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
