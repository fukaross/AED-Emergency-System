import {
  request,
  type RequestOptions,
} from '@/services/request-core'

export { request, type RequestOptions }

export function get<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'GET', data, ...options })
}

export function post<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'POST', data, ...options })
}

export function put<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'PUT', data, ...options })
}

export function del<T = unknown>(
  url: string,
  data?: Record<string, unknown>,
  options?: Partial<RequestOptions>,
): Promise<T> {
  return request<T>({ url, method: 'DELETE', data, ...options })
}
