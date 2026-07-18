// token 存取这一层自己待着，避免 login 服务和 request 层互相 import 循环依赖
import type { LoginResponse } from '@/types/common'
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  TOKEN_STORAGE_KEY,
} from '@/utils/constants'

export interface AuthSessionContext {
  readonly epoch: number
  readonly token: string
  readonly userId: string
}

export const AUTH_SESSION_CHANGED_MESSAGE = '登录账号已变更，请重试'

let authEpoch = 0

export function getToken(): string {
  try {
    return (uni.getStorageSync(TOKEN_STORAGE_KEY) as string) || ''
  } catch {
    return ''
  }
}

export function getTokenExpireAt(): number {
  return (uni.getStorageSync(TOKEN_EXPIRE_KEY) as number) || 0
}

export function persistAuthSession(result: LoginResponse): void {
  authEpoch += 1
  writeAuthSession(result)
}

export function commitLoginSession(
  result: LoginResponse,
  expected: AuthSessionContext,
): boolean {
  if (!isAuthEpochCurrent(expected)) return false
  persistAuthSession(result)
  return true
}

export function replaceAuthSession(
  result: LoginResponse,
  expected: AuthSessionContext,
): boolean {
  if (!isAuthSessionCurrent(expected)) return false
  if (expected.userId && result.userInfo.id !== expected.userId) return false

  writeAuthSession(result)
  return true
}

export function captureAuthSession(): AuthSessionContext {
  return {
    epoch: authEpoch,
    token: getToken(),
    userId: (uni.getStorageSync(CURRENT_USER_ID_STORAGE_KEY) as string) || '',
  }
}

export function isAuthEpochCurrent(context: AuthSessionContext): boolean {
  return context.epoch === authEpoch
}

export function isAuthSessionCurrent(context: AuthSessionContext): boolean {
  return isAuthEpochCurrent(context) && context.token === getToken()
}

export function createAuthSessionChangedError(): Error {
  return new Error(AUTH_SESSION_CHANGED_MESSAGE)
}

function writeAuthSession(result: LoginResponse): void {
  uni.setStorageSync(TOKEN_STORAGE_KEY, result.token)
  uni.setStorageSync(TOKEN_EXPIRE_KEY, Date.now() + result.expiresIn * 1000)
  uni.setStorageSync(CURRENT_USER_ID_STORAGE_KEY, result.userInfo.id)
  uni.setStorageSync(AUTH_USER_INFO_STORAGE_KEY, result.userInfo)
}

export function clearAuthSession(): void {
  authEpoch += 1
  uni.removeStorageSync(TOKEN_STORAGE_KEY)
  uni.removeStorageSync(TOKEN_EXPIRE_KEY)
  uni.removeStorageSync(CURRENT_USER_ID_STORAGE_KEY)
  uni.removeStorageSync(AUTH_USER_INFO_STORAGE_KEY)
}
