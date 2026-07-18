// 请求层的全局错误处理 —— 401 自动登出 + 统一 toast
import { terminateAuthentication } from '@/services/auth-termination'
import {
  captureAuthSession,
  isAuthEpochCurrent,
  type AuthSessionContext,
} from '@/services/auth-session'
import { ERROR_CODES, ERROR_MESSAGES } from '@/utils/constants'

export { terminateAuthentication } from '@/services/auth-termination'

export interface AuthenticationRejectionCommand {
  readonly code: number
  readonly message: string
  readonly showPrompt: boolean
  readonly session: AuthSessionContext
}

interface AuthenticationTerminationState {
  readonly epoch: number
  promptShown: boolean
}

let terminationState: AuthenticationTerminationState | null = null

/** A successful login starts a new prompt-ownership lifecycle. */
export function resetAuthenticationRejectionState(): void {
  terminationState = null
}

/** 合并同一认证代际的终止动作，同时允许可见请求补上被 silent 请求跳过的提示。 */
export function handleSessionAuthenticationRejection(
  command: AuthenticationRejectionCommand,
): void {
  const ownsExistingTermination = terminationState?.epoch === command.session.epoch
  if (!isAuthEpochCurrent(command.session) && !ownsExistingTermination) return

  if (isAuthEpochCurrent(command.session)) {
    terminationState = { epoch: command.session.epoch, promptShown: false }
    terminateAuthentication()
  }
  if (!command.showPrompt || terminationState?.promptShown) return

  terminationState!.promptShown = true
  showAuthenticationRejection(command.code, command.message)
}

/** Owns both authentication teardown and the optional user-facing terminal prompt. */
export function handleAuthenticationRejection(
  code: number,
  message: string,
  showPrompt = true,
): void {
  handleSessionAuthenticationRejection({
    code,
    message,
    showPrompt,
    session: captureAuthSession(),
  })
}

function showAuthenticationRejection(code: number, message: string): void {
  if (code === ERROR_CODES.UNAUTHORIZED) {
    uni.showModal({ title: '登录过期', content: '请重新登录', showCancel: false })
    return
  }
  const userMessage = message || ERROR_MESSAGES[code] || '认证失败'
  uni.showModal({ title: '提示', content: userMessage, showCancel: false })
}

export function handleErrorCode(code: number, message: string): void {
  if (code === ERROR_CODES.UNAUTHORIZED || code === ERROR_CODES.ACCOUNT_DISABLED) {
    handleAuthenticationRejection(code, message)
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
