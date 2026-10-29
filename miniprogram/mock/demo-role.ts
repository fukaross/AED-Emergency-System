// 演示角色注册表消费层 —— 登录 code、角色切换与未知 code 的显式失败。
//
// 角色数据来自 demo/roles.json 生成产物；真实微信档不使用这里的任何 code。
import { DEMO_ROLES, DEMO_USERS } from './generated/demo-roles.generated'
import type { DemoRole, DemoUser } from './demo-types'

export const DEMO_ROLE_STORAGE_KEY = 'aed_demo_role_code'
export const DEFAULT_DEMO_LOGIN_CODE = 'demo-requester'

export class DemoRoleError extends Error {
  constructor(message: string) {
    super(`[demo-role] ${message}`)
    this.name = 'DemoRoleError'
  }
}

/** 可切换的演示角色（顺序即 demo/roles.json 顺序）。 */
export function listDemoRoles(): readonly DemoRole[] {
  return DEMO_ROLES
}

export function findDemoRoleByLoginCode(loginCode: string): DemoRole | null {
  return DEMO_ROLES.find((role) => role.loginCode === loginCode) ?? null
}

export function findDemoRoleByOpenId(openId: string): DemoRole | null {
  return DEMO_ROLES.find((role) => role.openId === openId) ?? null
}

export function findDemoUser(openId: string): DemoUser | null {
  return DEMO_USERS.find((user) => user.openId === openId) ?? null
}

/** 未知角色 code 必须显式报错，不能静默回落到默认角色。 */
export function resolveDemoRole(loginCode: string): DemoRole {
  const role = findDemoRoleByLoginCode(loginCode)
  if (!role) {
    throw new DemoRoleError(
      `未知演示角色 code "${loginCode}"，可选值：${DEMO_ROLES.map((item) => item.loginCode).join(', ')}`,
    )
  }
  return role
}

function readStoredLoginCode(): string {
  try {
    const stored = uni.getStorageSync(DEMO_ROLE_STORAGE_KEY) as string
    return typeof stored === 'string' && stored.trim()
      ? stored.trim()
      : DEFAULT_DEMO_LOGIN_CODE
  } catch {
    return DEFAULT_DEMO_LOGIN_CODE
  }
}

/** 当前生效的演示角色（mock 的当前身份 / local-demo 的登录 code 都取这里）。 */
export function getActiveDemoRole(): DemoRole {
  return resolveDemoRole(readStoredLoginCode())
}

/** 切换演示角色；非法 code 抛错且不写入存储。 */
export function setActiveDemoRole(loginCode: string): DemoRole {
  const role = resolveDemoRole(loginCode)
  try {
    uni.setStorageSync(DEMO_ROLE_STORAGE_KEY, role.loginCode)
  } catch {
    console.warn('[demo-role] 保存演示角色失败')
  }
  return role
}

export function resetActiveDemoRole(): void {
  try {
    uni.removeStorageSync(DEMO_ROLE_STORAGE_KEY)
  } catch {
    console.warn('[demo-role] 清理演示角色失败')
  }
}

export function resolveCurrentDemoRole(): DemoRole {
  return getActiveDemoRole()
}

export function resolveCurrentDemoUser(): DemoUser {
  const role = getActiveDemoRole()
  const user = findDemoUser(role.openId)
  if (!user) {
    throw new DemoRoleError(`demo 数据缺少角色 ${role.role} 对应的用户 ${role.openId}`)
  }
  return user
}
