import client from './client'
import type { AdminAuditLogPage } from '@/types/audit-log'
import type { ApiResponse } from '@/types/api'

export interface AuditLogParams {
  page?: number
  pageSize?: number
  eventType?: string
  eventName?: string
  actor?: string
  keyword?: string
}

export function getAuditLogs(params: AuditLogParams = {}) {
  return client.get<ApiResponse<AdminAuditLogPage>>('/audit-logs', { params })
}
