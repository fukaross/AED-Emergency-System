import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/utils/runtime-env', () => ({
  USER_BACKEND_READY: true,
  USE_REAL_WECHAT_FLOW: true,
}))

const apiMocks = vi.hoisted(() => ({
  fetchUserProfile: vi.fn(),
  updateUserProfile: vi.fn(),
}))

vi.mock('@/api/user', () => apiMocks)

const storage = new Map<string, unknown>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => storage.delete(key)),
  setStorageSync: vi.fn((key: string, value: unknown) => storage.set(key, value)),
})

import { fetchUserProfile, updateUserProfile } from '@/api/user'
import { clearAuthSession, persistAuthSession } from '@/services/auth-session'
import { registerAuthenticationTerminator } from '@/services/auth-termination'
import { useUserStore } from '@/store/user'
import type { LoginResponse } from '@/types/common'
import type { UserProfile } from '@/types/user'

const terminateAuthentication = vi.fn()

function buildProfile(name: string, avatarUrl: string): UserProfile {
  return {
    id: 'openid-real',
    name,
    avatarUrl,
    gender: 'unknown',
    phone: '',
    primaryRole: 'student',
    certified: false,
    level: null,
    schoolProfile: null,
    rescuerSummary: null,
  }
}

describe('user store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    storage.clear()
    vi.clearAllMocks()
    registerAuthenticationTerminator(terminateAuthentication)
  })

  it('loads backend profile when user backend is ready', async () => {
    vi.mocked(fetchUserProfile).mockResolvedValue(
      buildProfile('后端昵称', 'https://cdn.example/avatar.png'),
    )

    const store = useUserStore()
    await store.loadProfile()

    expect(fetchUserProfile).toHaveBeenCalledTimes(1)
    expect(store.userInfo).toMatchObject({
      name: '后端昵称',
      avatarUrl: 'https://cdn.example/avatar.png',
      primaryRole: 'student',
    })
  })

  it('refreshes backend profile after saving edits', async () => {
    vi.mocked(updateUserProfile).mockResolvedValue()
    vi.mocked(fetchUserProfile).mockResolvedValue(
      buildProfile('保存后昵称', 'https://cdn.example/saved.png'),
    )

    const store = useUserStore()
    await store.saveProfile({ name: '保存后昵称' })

    expect(updateUserProfile).toHaveBeenCalledWith({ name: '保存后昵称' })
    expect(fetchUserProfile).toHaveBeenCalledTimes(1)
    expect(store.userInfo).toMatchObject({
      name: '保存后昵称',
      avatarUrl: 'https://cdn.example/saved.png',
    })
  })

  it('clears a cached certification profile when refresh is forbidden', async () => {
    vi.mocked(fetchUserProfile).mockRejectedValue(
      Object.assign(new Error('账号已禁用'), { statusCode: 403, code: 1018 }),
    )
    const store = useUserStore()
    registerAuthenticationTerminator(() => {
      terminateAuthentication()
      store.clearUser()
    })
    store.setLoginProfile({ ...buildProfile('缓存急救员', ''), certified: true })

    await expect(store.loadProfile()).rejects.toMatchObject({ statusCode: 403, code: 1018 })

    expect(store.userInfo).toBeNull()
    expect(store.isCertified).toBe(false)
    expect(terminateAuthentication).toHaveBeenCalledTimes(1)
  })

  it('does not terminate authentication for a business forbidden profile response', async () => {
    vi.mocked(fetchUserProfile).mockRejectedValue(
      Object.assign(new Error('权限不足'), { statusCode: 403, code: 1003 }),
    )
    const store = useUserStore()

    await expect(store.loadProfile()).rejects.toMatchObject({ statusCode: 403, code: 1003 })

    expect(terminateAuthentication).not.toHaveBeenCalled()
  })

  it('does not let a late profile response replace a newly bound account', async () => {
    let finishOld: ((profile: UserProfile) => void) | undefined
    vi.mocked(fetchUserProfile).mockReturnValueOnce(new Promise((resolve) => {
      finishOld = resolve
    }))
    const store = useUserStore()
    persistAuthSession(buildLogin('token-a', 'openid-a'))
    const pending = store.loadProfile()

    clearAuthSession()
    store.clearUser()
    persistAuthSession(buildLogin('token-b', 'openid-b'))
    store.setLoginProfile({
      ...buildProfile('账号 B', ''),
      id: 'openid-b',
    })
    finishOld?.({
      ...buildProfile('账号 A', ''),
      id: 'openid-a',
    })
    await pending

    expect(store.userInfo?.id).toBe('openid-b')
    expect(store.displayName).toBe('账号 B')
  })
})

function buildLogin(token: string, id: string): LoginResponse {
  return {
    token,
    expiresIn: 2592000,
    userInfo: { id, name: id, avatarUrl: '', isNewUser: false },
  }
}
