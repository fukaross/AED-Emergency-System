// 请求层的通用配置 —— base URL、认证路径
import { API_BASE_URL } from '@/utils/runtime-env'

export const BASE_URL = API_BASE_URL
export const IS_DEV = import.meta.env.VITE_APP_ENV === 'development'
export const AUTH_LOGIN_PATH = '/api/auth/wechat-login'
export const AUTH_REFRESH_PATH = '/api/auth/refresh'

