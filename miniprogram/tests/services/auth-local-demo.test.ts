import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestMocks = vi.hoisted(() => ({ post: vi.fn() }))

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'http://127.0.0.1:5150',
  IS_LOCAL_DEMO: true,
  USE_MOCK: false,
  USE_REAL_WECHAT_FLOW: false,
  RESTORE_AUTH_SESSION: false,
}))

vi.mock('@/services/request', () => requestMocks)

import { wxLogin } from '@/services/auth-service'
import { setActiveDemoRole } from '@/mock/demo-role'

const storage = new Map<string, string>()
const wechatLogin = vi.fn()
const showModal = vi.fn()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key) ?? ''),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
  login: wechatLogin,
  showModal,
})

function mockLoginResponse(openId: string) {
  requestMocks.post.mockResolvedValueOnce({
    token: 'real-bearer-token',
    openId,
    isNewUser: false,
    expiresIn: 2592000,
  })
}

describe('local-demo login (fixed demo role codes through the real endpoint)', () => {
  beforeEach(() => {
    storage.clear()
    requestMocks.post.mockReset()
    wechatLogin.mockReset()
    showModal.mockReset()
  })

  it('logs in with the default requester code and does not call wx.login', async () => {
    mockLoginResponse('demo-openid-requester')

    const result = await wxLogin()

    expect(requestMocks.post).toHaveBeenCalledWith('/api/auth/wechat-login', {
      code: 'demo-requester',
    })
    expect(wechatLogin).not.toHaveBeenCalled()
    expect(result.token).toBe('real-bearer-token')
    expect(result.userInfo.id).toBe('demo-openid-requester')
    expect(result.userInfo.name).toBe('演示甲（求助人）')
  })

  it('uses the selected demo role code and its display name', async () => {
    setActiveDemoRole('demo-rescuer')
    mockLoginResponse('demo-openid-rescuer-east')

    const result = await wxLogin()

    expect(requestMocks.post).toHaveBeenCalledWith('/api/auth/wechat-login', {
      code: 'demo-rescuer',
    })
    expect(result.userInfo.name).toBe('演示乙（急救员·东区）')
  })

  it('rejects an unknown stored role code with a visible error and no request', async () => {
    storage.set('aed_demo_role_code', 'demo-does-not-exist')

    await expect(wxLogin()).rejects.toThrow('未知演示角色 code')
    expect(requestMocks.post).not.toHaveBeenCalled()
    expect(wechatLogin).not.toHaveBeenCalled()
    expect(showModal).toHaveBeenCalledWith(
      expect.objectContaining({ title: '演示角色无效' }),
    )
  })
})
