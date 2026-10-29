import { defineStore } from 'pinia'
import type { AdminLoginResponse } from '@/types/auth'

type AdminRole = 'super_admin' | 'admin'

interface AdminState {
  adminKey: string | null
  username: string | null
  role: AdminRole | null
}

function readStoredSession(): AdminState {
  const adminKey = normalizeStoredValue(localStorage.getItem('adminKey'))
  const username = normalizeStoredValue(localStorage.getItem('adminUsername'))
  const role = parseRole(localStorage.getItem('adminRole'))

  if (!adminKey || !username || !role) {
    clearStoredSession()
    return { adminKey: null, username: null, role: null }
  }

  return { adminKey, username, role }
}

function normalizeStoredValue(value: string | null): string | null {
  if (!value || value === 'undefined' || value === 'null') return null
  return value
}

function parseRole(value: string | null): AdminRole | null {
  if (value === 'super_admin' || value === 'admin') return value
  return null
}

function clearStoredSession() {
  localStorage.removeItem('adminKey')
  localStorage.removeItem('adminUsername')
  localStorage.removeItem('adminRole')
}

function assertSession(data: AdminLoginResponse) {
  if (!data.adminKey || !data.username || !parseRole(data.role)) {
    throw new Error('管理端登录响应缺少有效会话字段')
  }
}

export const useAdminStore = defineStore('admin', {
  state: (): AdminState => readStoredSession(),

  getters: {
    isAuthenticated: (state) => !!state.adminKey,
    isSuperAdmin: (state) => state.role === 'super_admin',
  },

  actions: {
    setSession(data: AdminLoginResponse) {
      assertSession(data)
      this.adminKey = data.adminKey
      this.username = data.username
      this.role = data.role
      localStorage.setItem('adminKey', data.adminKey)
      localStorage.setItem('adminUsername', data.username)
      localStorage.setItem('adminRole', data.role)
    },

    clearSession() {
      this.adminKey = null
      this.username = null
      this.role = null
      clearStoredSession()
    },
  },
})
