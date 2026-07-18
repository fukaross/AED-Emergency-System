// 登录鉴权状态：token、登录态、登录/登出方法（不管用户资料）。
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  wxLogin,
  isAuthenticated,
  clearAuth,
  initializeAuthState,
} from '@/services/auth-service'
import { useUserStore } from './user'
import { useHelpStore } from './help'
import { useRescueSessionStore } from './rescue-session'
import type { UserProfile } from '@/types/user'
import { USE_MOCK, USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'
import { registerAuthenticationTerminator } from '@/services/auth-termination'
import { closeAllRescueSockets } from '@/services/rescue-socket'
import {
  captureAuthSession,
  commitLoginSession,
} from '@/services/auth-session'
import { resetAuthenticationRejectionState } from '@/services/request-errors'

export const useAuthStore = defineStore('auth', () => {
  // ===== State =====
  const isLoggedIn = ref(initializeAuthState())
  let authOperation = 0

  // ===== Actions =====
  async function login(): Promise<void> {
    const context = captureAuthSession()
    const operation = ++authOperation
    const result = await wxLogin()
    if (operation !== authOperation) return
    if (!commitLoginSession(result, context)) return
    resetAuthenticationRejectionState()

    resetUserScopedState()
    isLoggedIn.value = true

    // 登录成功后加载用户资料
    const userStore = useUserStore()
    if (USE_REAL_WECHAT_FLOW) {
      userStore.setLoginProfile(toUserProfile(result.userInfo))
      return
    }

    if (result.userInfo && USE_MOCK) {
      await userStore.loadProfile()
    }
  }

  function terminate(): void {
    authOperation += 1
    closeAllRescueSockets()
    clearAuth()
    isLoggedIn.value = false
    resetUserScopedState()
  }

  function checkAuth(): boolean {
    isLoggedIn.value = isAuthenticated()
    if (!isLoggedIn.value) terminate()
    return isLoggedIn.value
  }

  function resetUserScopedState(): void {
    useUserStore().clearUser()
    useHelpStore().clearAuthenticatedState()
    const rescueStore = useRescueSessionStore()
    rescueStore.resetMineSessions()
    rescueStore.resetSession()
  }

  registerAuthenticationTerminator(terminate)

  return {
    isLoggedIn,
    login,
    logout: () => terminate(),
    terminate,
    checkAuth,
  }
})

function toUserProfile(userInfo: {
  readonly id: string
  readonly name: string
  readonly avatarUrl: string
}): UserProfile {
  return {
    id: userInfo.id,
    name: userInfo.name || '微信用户',
    avatarUrl: userInfo.avatarUrl,
    gender: 'unknown',
    phone: '',
    primaryRole: 'student',
    certified: false,
    level: null,
    schoolProfile: null,
    rescuerSummary: null,
  }
}
