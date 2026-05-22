export type HelpRequestStatus =
  | 'published'
  | 'responding'
  | 'pending_review'
  | 'closed'
  | 'false_alarm'

export type HelpRequestType =
  | 'help_hand'
  | 'call_120'
  | 'call_school'
  | 'general'

export type RequestChannel = 'user' | 'guest'

export interface AdminHelpListItem {
  id: string
  requestNo: string
  requestChannel: RequestChannel
  content: string
  type: HelpRequestType
  status: HelpRequestStatus
  address: string
  createdAt: string
  closedAt: string | null
  closeNote: string | null
  reportersCount: number
  respondersCount: number
  latestEta: string | null
}

export interface AdminHelpListResponse {
  items: AdminHelpListItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AdminHelpDetailResponse extends AdminHelpListItem {
  images: string[]
  reports: HelpReportRecord[]
  reporters: HelpReporter[]
  timeline: HelpTimelineEntry[]
  responders: HelpResponder[]
}

export interface HelpReportRecord {
  id: string
  reporterOpenId: string
  content: string
  images: string[]
  createdAt: string
}

export interface HelpReporter {
  openId: string
  displayName: string
  reportsCount: number
  lastReportedAt: string
}

export interface HelpTimelineEntry {
  type: string
  description: string
  createdAt: string
}

export interface HelpResponder {
  openId: string
  name: string
  eta: string | null
  arrivedAt: string | null
}
