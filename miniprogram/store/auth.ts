/**
 * 登录鉴权状态 Store
 *
 * 职责：管理 token、登录状态、登录方法
 * 不管用户资料（由 user store 管理）
 *
 * 扩展保留（D-03）
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  wxLogin,
  isAuthenticated,
  clearAuth,
} from '@/services/auth-service'
import { useUserStore } from './user'

export const useAuthStore = defineStore('auth', () => {
  // ===== State =====
  const isLoggedIn = ref(isAuthenticated())

  // ===== Actions =====
  async function login(): Promise<void> {
    const result = await wxLogin()
    isLoggedIn.value = true

    // 登录成功后加载用户资料
    const userStore = useUserStore()
    if (result.userInfo) {
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
