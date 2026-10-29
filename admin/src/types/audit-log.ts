export interface AdminAuditLogRecord {
  id: string
  eventType: string
  eventName: string
  targetId: string | null
  actorType: string | null
  actorId: string | null
  actorName: string | null
  actorRole: string | null
  summary: string | null
  metadata: Record<string, unknown> | null
  ipAddress: string | null
  userAgent: string | null
  createdAt: string
}

export interface AdminAuditLogPage {
  items: AdminAuditLogRecord[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}
