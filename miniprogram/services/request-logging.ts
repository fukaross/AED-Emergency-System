import { IS_DEV } from '@/services/request-config'

function sanitizeLogPath(url: string): string {
  return url.split('?')[0]
}

export function logRequest(method: string, url: string): void {
  if (!IS_DEV) return
  console.log(`[Request] ${method} ${sanitizeLogPath(url)}`)
}

export function logResponse(
  url: string,
  statusCode: number,
  apiCode: number | undefined,
): void {
  if (!IS_DEV) return
  const codeText = apiCode === undefined ? 'invalid' : String(apiCode)
  console.log(
    `[Response] ${sanitizeLogPath(url)} status=${statusCode} code=${codeText}`,
  )
}

export function logNetworkFailure(method: string, url: string): void {
  console.error(`[Request] ${method} ${sanitizeLogPath(url)} network-failure`)
}
