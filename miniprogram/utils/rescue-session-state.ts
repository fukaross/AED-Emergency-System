import type { ApiError } from '@/services/request-response'
import type { RescueSessionSnapshot } from '@/types/rescue-session'
import { ERROR_CODES } from '@/utils/constants'

export type RescueSessionTerminalState = 'closed' | 'revoked'

export function classifyRescueSessionTerminalError(
  error: unknown,
): RescueSessionTerminalState | null {
  const apiError = error as ApiError
  if (apiError?.code === ERROR_CODES.RESCUE_SESSION_CLOSED) return 'closed'
  if (
    apiError?.statusCode === 401
    || apiError?.statusCode === 403
    || apiError?.code === ERROR_CODES.UNAUTHORIZED
    || apiError?.code === ERROR_CODES.FORBIDDEN
    || apiError?.code === ERROR_CODES.ACCOUNT_DISABLED
    || apiError?.code === ERROR_CODES.RESCUE_NOT_PARTICIPANT
  ) return 'revoked'
  return null
}

export function computeServerClockOffset(serverTime: string, localNow: number): number {
  const serverNow = Date.parse(serverTime)
  return Number.isFinite(serverNow) ? serverNow - localNow : 0
}

export function mergeSessionSnapshot(
  current: RescueSessionSnapshot,
  incremental: RescueSessionSnapshot,
): RescueSessionSnapshot {
  const knownSeqs = new Set(current.messages.map((message) => message.seq))
  const newMessages = incremental.messages.filter((message) => !knownSeqs.has(message.seq))
  return {
    ...incremental,
    messages: [...current.messages, ...newMessages].sort((a, b) => a.seq - b.seq),
    latestSeq: Math.max(current.latestSeq, incremental.latestSeq),
  }
}
