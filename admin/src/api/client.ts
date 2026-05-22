import axios, { isAxiosError } from 'axios'
import { useAdminStore } from '@/stores/admin'
import router from '@/router'

interface ApiErrorData {
  code?: string | number
  message?: string
}

const client = axios.create({
  baseURL: '/api/admin',
  timeout: 15000,
})

client.interceptors.request.use((config) => {
  const headers = getAuthHeader()
  Object.assign(config.headers, headers)
  return config
})

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const store = useAdminStore()
      store.clearSession()
      const currentRoute = router.currentRoute.value
      if (currentRoute.name !== 'login') {
        router.replace('/login')
      }
    }
    return Promise.reject(error)
  },
)

function getAuthHeader(): Record<string, string> {
  const adminKey = localStorage.getItem('adminKey')
  if (adminKey) {
    return { 'X-Aed-Admin-Key': adminKey }
  }
  return {}
}

export function getApiErrorCode(error: unknown): string | number | undefined {
  if (isAxiosError<ApiErrorData>(error)) {
    return error.response?.data?.code
  }
  return undefined
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorData>(error)) {
    return error.response?.data?.message || fallback
  }
  return fallback
}

export function getApiErrorStatus(error: unknown): number | undefined {
  if (isAxiosError<ApiErrorData>(error)) {
    return error.response?.status
  }
  return undefined
}

export function isNetworkApiError(error: unknown): boolean {
  return isAxiosError(error) && error.message === 'Network Error'
}

export default client
