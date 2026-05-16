import type { ApiResponse } from '@/types/common'

export interface ApiError extends Error {
  readonly code?: number
  readonly statusCode?: number
}

export function createApiError(
  message: string,
  code?: number,
  statusCode?: number,
): ApiError {
  return Object.assign(new Error(message), { code, statusCode })
}

export function extractApiResponse<T>(payload: unknown): ApiResponse<T> | null {
  if (!payload || typeof payload !== 'object') {
    return null
  }

  const candidate = payload as Partial<ApiResponse<T>>
  if (typeof candidate.code !== 'number' || typeof candidate.message !== 'string') {
    return null
  }

  return candidate as ApiResponse<T>
}
