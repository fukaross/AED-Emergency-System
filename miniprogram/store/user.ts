// 用户资料、学校档案摘要（不管 token/登录，那归 auth store 管）
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserProfile, UpdateUserProfilePayload } from '@/types/user'
import { fetchUserProfile, updateUserProfile } from '@/api/user'
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
} from '@/utils/constants'
import { USER_BACKEND_READY, USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'
import {
  buildWeChatDisplayName,
  shouldUseWeChatDisplayName,
} from '@/utils/wechat-display'

export const useUserStore = defineStore('user', () => {
  // ===== State =====
  const userInfo = ref<UserProfile | null>(null)

  // ===== Getters =====
  const displayName = computed(() => userInfo.value?.name || '游客')
  const isCertified = computed(() => userInfo.value?.certified ?? false)
  const rescuerLevel = computed(() => userInfo.value?.level ?? null)
  const rescuerSummary = computed(() => userInfo.value?.rescuerSummary ?? null)
  const primaryRole = computed(() => userInfo.value?.primaryRole ?? 'student')

  // ===== Actions =====
  async function loadProfile(): Promise<void> {
    if (USER_BACKEND_READY) {
      userInfo.value = await fetchUserProfile()
      return
    }

    const authProfile = loadAuthUserProfile()
    if (USE_REAL_WECHAT_FLOW) {
      userInfo.value = authProfile ?? buildStoredLoginFallbackProfile()
      return
    }

    userInfo.value = await fetchUserProfile()
  }

  function setLoginProfile(profile: UserProfile): void {
    userInfo.value = profile
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
    rescuerSummary,
    primaryRole,
    loadProfile,
    setLoginProfile,
    saveProfile,
    clearUser,
  }
})

function loadAuthUserProfile(): UserProfile | null {
  const stored = uni.getStorageSync(AUTH_USER_INFO_STORAGE_KEY) as
    | UserProfile
    | undefined
  if (!stored?.id) return null

  return normalizeAuthUserProfile(stored)
}

function buildStoredLoginFallbackProfile(): UserProfile {
  const id = (uni.getStorageSync(CURRENT_USER_ID_STORAGE_KEY) as string) || ''
  return normalizeAuthUserProfile({
    id,
    name: buildWeChatDisplayName(id),
    avatarUrl: '',
    gender: 'unknown',
    phone: '',
    primaryRole: 'student',
    certified: false,
    level: null,
    schoolProfile: null,
    rescuerSummary: null,
  })
}

function normalizeAuthUserProfile(profile: UserProfile): UserProfile {
  return {
    id: profile.id,
    name: shouldUseWeChatDisplayName(profile.name)
      ? buildWeChatDisplayName(profile.id)
      : profile.name,
    avatarUrl: profile.avatarUrl || '',
    gender: profile.gender ?? 'unknown',
    phone: profile.phone ?? '',
    primaryRole: profile.primaryRole ?? 'student',
    certified: profile.certified ?? false,
    level: profile.level ?? null,
    schoolProfile: profile.schoolProfile ?? null,
    rescuerSummary: profile.rescuerSummary ?? null,
  }
}
