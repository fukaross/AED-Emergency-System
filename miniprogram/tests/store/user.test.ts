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

vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => ({
    id: 'openid-local',
    name: '微信用户 open...ocal',
    avatarUrl: '',
  })),
})

import { fetchUserProfile, updateUserProfile } from '@/api/user'
import { useUserStore } from '@/store/user'
import type { UserProfile } from '@/types/user'

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
    vi.clearAllMocks()
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
      Object.assign(new Error('账号已禁用'), { statusCode: 403 }),
    )
    const store = useUserStore()
    store.setLoginProfile({ ...buildProfile('缓存急救员', ''), certified: true })

    await expect(store.loadProfile()).rejects.toMatchObject({ statusCode: 403 })

    expect(store.userInfo).toBeNull()
    expect(store.isCertified).toBe(false)
  })
})
