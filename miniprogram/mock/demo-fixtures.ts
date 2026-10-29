// demo 合成数据的跨模块投影 —— 只做查找与 ID 映射，不再维护第二份数据。
import { requireCampus, type CampusCode } from '@/config/site-config'
import { DEMO_RESCUER_PROFILES } from './generated/demo-rescuer-profiles.generated'
import { DEMO_DATA_VERSION } from './generated/demo-metadata.generated'
import { DEMO_ROLES } from './generated/demo-roles.generated'
import type { DemoRescuerProfile, DemoRole, DemoSchoolBinding, DemoUser } from './demo-types'
import {
  findDemoRoleByOpenId,
  findDemoUser,
  resolveCurrentDemoRole,
} from './demo-role'

export { DEMO_DATA_VERSION }

/** demo 数据的参考“当前时间”，只用于稳定 Mock 的在线/到期判定。 */
export const DEMO_REFERENCE_NOW = '2026-10-26T10:00:00.000Z'
export const DEMO_REFERENCE_DATE = '2026-10-26'

export const DEMO_AVATAR_FALLBACK = '/static/images/avatar-default.png'

/** 求助演示角色在 roles.json 中登记的合成联系电话（仅本地演示，禁止外发）。 */
export const DEMO_REQUESTER_PHONE =
  DEMO_ROLES.find((role) => role.role === 'requester')?.schoolBinding.phone ?? ''

export function findDemoRescuerProfile(profileId: string): DemoRescuerProfile | null {
  return DEMO_RESCUER_PROFILES.find((profile) => profile.id === profileId) ?? null
}

export function resolveCurrentRescuerProfile(): DemoRescuerProfile | null {
  const role = resolveCurrentDemoRole()
  if (!role.responderProfileId) return null
  return findDemoRescuerProfile(role.responderProfileId)
}

export function resolveDemoSchoolBinding(openId: string): DemoSchoolBinding | null {
  return findDemoRoleByOpenId(openId)?.schoolBinding ?? null
}

export function resolveDemoCampusName(campusCode: CampusCode): string {
  return requireCampus(campusCode).name
}

export function toMockRescuerId(profileId: string): string {
  return `rescuer-${profileId}`
}

export function toMockArchiveId(profileId: string): string {
  return `archive-${profileId.toLowerCase()}`
}

export function toMockCertificateId(profileId: string): string {
  return `cert-${profileId.toLowerCase()}`
}

export function toMockResponderId(profileId: string): string {
  return `responder-${profileId}`
}

/** 档案列表/详情使用的“当前登录演示用户”。 */
export function resolveCurrentDemoUserOrNull(): DemoUser | null {
  const role = resolveCurrentDemoRole()
  return findDemoUser(role.openId)
}

export function resolveDemoRoleOrNull(roleCode: string): DemoRole | null {
  return DEMO_ROLES.find((role) => role.role === roleCode) ?? null
}
