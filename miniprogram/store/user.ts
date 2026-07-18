// 用户资料、学校档案摘要（不管 token/登录，那归 auth store 管）
import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { UpdateUserProfilePayload, UserProfile } from '@/types/user'
import { fetchUserProfile, updateUserProfile } from '@/api/user'
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
  ERROR_CODES,
} from '@/utils/constants'
import { USER_BACKEND_READY, USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'
import {
  buildWeChatDisplayName,
  shouldUseWeChatDisplayName,
} from '@/utils/wechat-display'
import {
  captureAuthSession,
  isAuthEpochCurrent,
  type AuthSessionContext,
} from '@/services/auth-session'
import { terminateAuthentication } from '@/services/auth-termination'

class UserStoreController {
  readonly userInfo = ref<UserProfile | null>(null)
  private profileGeneration = 0
  private latestProfileRequest = 0

  async loadProfile(): Promise<void> {
    const context = captureAuthSession()
    const generation = this.profileGeneration
    const request = ++this.latestProfileRequest
    try {
      const profile = await resolveUserProfile()
      if (!this.isCurrentProfileRequest(context, generation, request)) return
      this.userInfo.value = profile
    } catch (error) {
      if (this.isCurrentProfileRequest(context, generation, request)
        && isAccountDisabled(error)) terminateAuthentication()
      throw error
    }
  }

  setLoginProfile(profile: UserProfile): void {
    this.invalidateProfileRequests()
    this.userInfo.value = profile
  }

  async saveProfile(payload: UpdateUserProfilePayload): Promise<void> {
    const context = captureAuthSession()
    const generation = this.profileGeneration
    await updateUserProfile(payload)
    if (!this.isCurrentProfileOwner(context, generation)) return
    await this.loadProfile()
  }

  clearUser(): void {
    this.invalidateProfileRequests()
    this.userInfo.value = null
  }

  private invalidateProfileRequests(): void {
    this.profileGeneration += 1
    this.latestProfileRequest += 1
  }

  private isCurrentProfileOwner(
    context: AuthSessionContext,
    generation: number,
  ): boolean {
    return isAuthEpochCurrent(context) && generation === this.profileGeneration
  }

  private isCurrentProfileRequest(
    context: AuthSessionContext,
    generation: number,
    request: number,
  ): boolean {
    return this.isCurrentProfileOwner(context, generation)
      && request === this.latestProfileRequest
  }
}

export const useUserStore = defineStore('user', () => {
  const controller = new UserStoreController()
  const userInfo = controller.userInfo
  const displayName = computed(() => userInfo.value?.name || '游客')
  const isCertified = computed(() => userInfo.value?.certified ?? false)
  const rescuerLevel = computed(() => userInfo.value?.level ?? null)
  const rescuerSummary = computed(() => userInfo.value?.rescuerSummary ?? null)
  const primaryRole = computed(() => userInfo.value?.primaryRole ?? 'student')

  return {
    userInfo,
    displayName,
    isCertified,
    rescuerLevel,
    rescuerSummary,
    primaryRole,
    loadProfile: () => controller.loadProfile(),
    setLoginProfile: (profile: UserProfile) => controller.setLoginProfile(profile),
    saveProfile: (payload: UpdateUserProfilePayload) => controller.saveProfile(payload),
    clearUser: () => controller.clearUser(),
  }
})

async function resolveUserProfile(): Promise<UserProfile> {
  if (USER_BACKEND_READY) return fetchUserProfile()

  const authProfile = loadAuthUserProfile()
  if (USE_REAL_WECHAT_FLOW) {
    return authProfile ?? buildStoredLoginFallbackProfile()
  }
  return fetchUserProfile()
}

function isAccountDisabled(error: unknown): boolean {
  return typeof error === 'object'
    && error !== null
    && (error as { readonly code?: number }).code === ERROR_CODES.ACCOUNT_DISABLED
}

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
