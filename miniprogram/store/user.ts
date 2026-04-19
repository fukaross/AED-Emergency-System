/**
 * 用户信息 Store
 *
 * 管理用户资料、学校档案摘要
 * 不管 token/登录状态（由 auth store 管理）
 * Mock 与真实接口分流统一在 API 层处理
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserProfile, UpdateUserProfilePayload } from '@/types/user'
import { fetchUserProfile, updateUserProfile } from '@/api/user'

export const useUserStore = defineStore('user', () => {
  // ===== State =====
  const userInfo = ref<UserProfile | null>(null)

  // ===== Getters =====
  const displayName = computed(() => userInfo.value?.name || '游客')
  const isCertified = computed(() => userInfo.value?.certified ?? false)
  const rescuerLevel = computed(() => userInfo.value?.level ?? null)

  // ===== Actions =====
  async function loadProfile(): Promise<void> {
    userInfo.value = await fetchUserProfile()
  }

  async function saveProfile(payload: UpdateUserProfilePayload): Promise<void> {
    await updateUserProfile(payload)
    await loadProfile()
  }

  function clearUser(): void {
    userInfo.value = null
  }

  return {
    userInfo,
    displayName,
    isCertified,
    rescuerLevel,
    loadProfile,
    saveProfile,
    clearUser,
  }
})
