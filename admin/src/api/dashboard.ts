import client from './client'
import type { AdminDashboardResponse } from '@/types/dashboard'
import type { ApiResponse } from '@/types/api'

export function getDashboard() {
  return client.get<ApiResponse<AdminDashboardResponse>>('/dashboard')
}
