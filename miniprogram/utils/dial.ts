// 拨号统一入口 —— 演示档（mock / local-demo）不得发起真实拨号。
import { IS_DEMO_MODE } from '@/utils/runtime-env'

export const DEMO_DIAL_BLOCKED_TEXT = '演示模式不发起真实拨号'

export function isRealDialEnabled(): boolean {
  return !IS_DEMO_MODE
}

export interface DialOptions {
  readonly onFail?: () => void
}

/**
 * 统一的拨号守卫；返回是否真正发起拨号。
 * 演示档或空号码时只给可见提示，不调用 uni.makePhoneCall。
 */
export function dialPhone(phoneNumber: string, options: DialOptions = {}): boolean {
  const phone = phoneNumber.trim()

  if (!phone) {
    uni.showToast({ title: '未配置可拨打的电话', icon: 'none' })
    return false
  }

  if (!isRealDialEnabled()) {
    uni.showToast({ title: DEMO_DIAL_BLOCKED_TEXT, icon: 'none' })
    return false
  }

  uni.makePhoneCall(
    options.onFail
      ? { phoneNumber: phone, fail: options.onFail }
      : { phoneNumber: phone },
  )
  return true
}
