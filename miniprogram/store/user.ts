/**
 * 用户信息 Store
 *
 * 管理用户资料、学校档案摘要
 * 不管 token/登录状态（由 auth store 管理）
 * 后端未就绪时回退 mock 数据
 *
 * 来源：CLAUDE.md §6.3
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserProfile, UpdateUserProfilePayload } from '@/types/user'
import { fetchUserProfile, updateUserProfile } from '@/api/user'
import { USER_MOCK_DATA } from '@/mock/user'

export const useUserStore = defineStore('user', () => {
  // ===== State =====
  const userInfo = ref<UserProfile | null>(null)

  // ===== Getters =====
  const displayName = computed(() => userInfo.value?.name || '游客')
  const isCertified = computed(() => userInfo.value?.certified ?? false)
  const rescuerLevel = computed(() => userInfo.value?.level ?? null)

  // ===== Actions =====
  async function loadProfile(): Promise<void> {
    try {
      userInfo.value = await fetchUserProfile()
    } catch {
      // 后端未就绪时回退 mock 数据
      if (!userInfo.value) {
        userInfo.value = { ...USER_MOCK_DATA }
      }
    }
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
