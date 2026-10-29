// 急救员 Mock 档案 —— 直接投影 demo/data/rescuer-profiles.json 生成产物。
// 校区、等级、资格、技能、坐标全部来自 demo 数据，不再从培训批次名称猜测校区。
import type { CampusCode } from '@/config/site-config'
import { DEMO_RESCUER_PROFILES } from './generated/demo-rescuer-profiles.generated'
import type { DemoRescuerProfile } from './demo-types'
import { DEMO_REFERENCE_NOW, toMockRescuerId } from './demo-fixtures'

export interface RescuerRecord {
  readonly id: string
  readonly profileId: string
  readonly name: string
  readonly avatarUrl: string
  readonly phone: string
  readonly campus: CampusCode
  readonly level: 'honor' | 'trainee' | 'official' | 'core'
  readonly qualificationStatus: 'active' | 'inactive'
  readonly lat: number
  readonly lng: number
  readonly certificateNoMasked: string
  readonly workUnit: string | null
  readonly skills: {
    readonly cpr: boolean
    readonly aed: boolean
    readonly stb: boolean
  }
}

export interface ManualOfflineState {
  readonly from: string
  readonly until: string
}

export const INITIAL_MOCK_RESCUER_NOW = DEMO_REFERENCE_NOW

export const INITIAL_MANUAL_OFFLINE_STATE: ManualOfflineState | null = null

function toRescuerRecord(profile: DemoRescuerProfile): RescuerRecord {
  return {
    id: toMockRescuerId(profile.id),
    profileId: profile.id,
    name: profile.name,
    avatarUrl: profile.avatarUrl,
    phone: profile.phone,
    campus: profile.campus,
    level: profile.level,
    qualificationStatus: profile.qualificationStatus,
    lat: profile.lat,
    lng: profile.lng,
    certificateNoMasked: profile.certificateNoMasked,
    workUnit: profile.workUnit.trim() || null,
    skills: {
      cpr: profile.skillCpr,
      aed: profile.skillAed,
      stb: profile.skillStb,
    },
  }
}

export const MOCK_RESCUERS: readonly RescuerRecord[] =
  DEMO_RESCUER_PROFILES.map(toRescuerRecord)

/** 当前演示角色对应的急救员 id；非急救员角色返回 null。 */
export function resolveCurrentMockRescuerId(
  currentProfileId: string | null,
): string | null {
  if (!currentProfileId) return null
  return MOCK_RESCUERS.some((record) => record.profileId === currentProfileId)
    ? toMockRescuerId(currentProfileId)
    : null
}
