import { clearAuthSession } from '@/services/auth-session'
import { ERROR_CODES, ERROR_MESSAGES } from '@/utils/constants'

export function handleErrorCode(code: number, message: string): void {
  if (code === ERROR_CODES.UNAUTHORIZED) {
    uni.showModal({
      title: '登录过期',
      content: '请重新登录',
      showCancel: false,
      success: () => {
        clearAuthSession()
      },
    })
    return
  }

  const userMessage = ERROR_MESSAGES[code] || message || '请求失败'

  const modalCodes: readonly number[] = [
    ERROR_CODES.FORBIDDEN,
    ERROR_CODES.INTERNAL_ERROR,
  ]

  if (modalCodes.includes(code)) {
    uni.showModal({ title: '提示', content: userMessage, showCancel: false })
    return
  }

  uni.showToast({ title: userMessage, icon: 'none', duration: 2000 })
}
