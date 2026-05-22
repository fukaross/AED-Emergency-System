export interface AdminDashboardRecentLogItem {
  id: string
  eventType: string
  eventName: string
  actorName: string | null
  summary: string | null
  createdAt: string
}

export interface AdminDashboardResponse {
  usersTotal: number
  helpRequestsTotal: number
  pendingReviewTotal: number
  todayNewHelpRequests: number
  recentLogs: AdminDashboardRecentLogItem[]
}
