// 登录鉴权状态 —— token、登录态、登录/登出方法（不管用户资料）
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  wxLogin,
  isAuthenticated,
  clearAuth,
  initializeAuthState,
} from '@/services/auth-service'
import { useUserStore } from './user'
import type { UserProfile } from '@/types/user'
import { USE_MOCK, USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'

export const useAuthStore = defineStore('auth', () => {
  // ===== State =====
  const isLoggedIn = ref(initializeAuthState())

  // ===== Actions =====
  async function login(): Promise<void> {
    const result = await wxLogin()
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

  function logout(): void {
    clearAuth()
    isLoggedIn.value = false

    const userStore = useUserStore()
    userStore.clearUser()
  }

  function checkAuth(): boolean {
    isLoggedIn.value = isAuthenticated()
    return isLoggedIn.value
  }

  return {
    isLoggedIn,
    login,
    logout,
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
    certified: false,
    level: null,
    schoolProfile: null,
    rescuerSummary: null,
  }
}
