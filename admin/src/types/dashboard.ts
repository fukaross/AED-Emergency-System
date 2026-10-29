export interface AdminDashboardRecentLogItem {
  id: string
  eventType: string
  eventName: string
  actorName: string | null
  summary: string | null
  createdAt: string
}

/** 累计趋势单点：date 为 yyyy-MM-dd，total 为截至当日累计值 */
export interface AdminDashboardTrendPoint {
  date: string
  total: number
}

/** 求助处理单日点：processed=当日新增且已闭环，pending=当日新增仍在处理中 */
export interface AdminDashboardProcessingPoint {
  date: string
  processed: number
  pending: number
}

/** 求助状态分布切片 */
export interface AdminDashboardStatusSlice {
  status: string
  count: number
}

/** 仪表盘四组图表数据 */
export interface AdminDashboardCharts {
  userGrowth: AdminDashboardTrendPoint[]
  responderGrowth: AdminDashboardTrendPoint[]
  helpProcessing: AdminDashboardProcessingPoint[]
  statusDistribution: AdminDashboardStatusSlice[]
}

export interface AdminDashboardResponse {
  usersTotal: number
  helpRequestsTotal: number
  pendingReviewTotal: number
  todayNewHelpRequests: number
  recentLogs: AdminDashboardRecentLogItem[]
  charts: AdminDashboardCharts
}
