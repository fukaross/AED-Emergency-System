type AuthenticationTerminator = () => void

let activeTerminator: AuthenticationTerminator | null = null

export function registerAuthenticationTerminator(
  terminator: AuthenticationTerminator,
): void {
  activeTerminator = terminator
}

export function terminateAuthentication(): void {
  if (!activeTerminator) {
    throw new Error('认证终止器尚未初始化')
  }
  activeTerminator()
}
