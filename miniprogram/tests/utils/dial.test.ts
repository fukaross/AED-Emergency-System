import { afterEach, describe, expect, it, vi } from 'vitest'
import { DEMO_DIAL_BLOCKED_TEXT, dialPhone, isRealDialEnabled } from '@/utils/dial'

const phoneCalls = vi.fn()
const toasts = vi.fn()

function stubUni() {
  vi.stubGlobal('uni', {
    makePhoneCall: phoneCalls,
    showToast: toasts,
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
  phoneCalls.mockReset()
  toasts.mockReset()
})

describe('dial guard (demo modes must not dial)', () => {
  it('reports dialing as disabled in the demo build', () => {
    // 测试环境按 mock 档加载，演示档禁止真实拨号
    expect(isRealDialEnabled()).toBe(false)
  })

  it('does not call makePhoneCall in demo mode and shows a visible notice', () => {
    stubUni()

    const dialed = dialPhone('120')

    expect(dialed).toBe(false)
    expect(phoneCalls).not.toHaveBeenCalled()
    expect(toasts).toHaveBeenCalledWith({
      title: DEMO_DIAL_BLOCKED_TEXT,
      icon: 'none',
    })
  })

  it('rejects an empty phone number without dialing', () => {
    stubUni()

    const dialed = dialPhone('   ')

    expect(dialed).toBe(false)
    expect(phoneCalls).not.toHaveBeenCalled()
    expect(toasts).toHaveBeenCalledWith({
      title: '未配置可拨打的电话',
      icon: 'none',
    })
  })
})
