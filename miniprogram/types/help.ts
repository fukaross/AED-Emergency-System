// 求助发布、响应、报告、游客查询相关的类型

export type HelpRequestType = 'help_hand' | 'call_120' | 'call_school' | 'general'

// published → responding → pending_review → closed（或直接 false_alarm）
export type HelpStatus =
  | 'published'
  | 'responding'
  | 'pending_review'
  | 'closed'
  | 'false_alarm'

export type RequestChannel = 'user' | 'guest'

export interface PublishHelpPayload {
  readonly lat: number
  readonly lng: number
  readonly address: string
  readonly content: string
  readonly type: HelpRequestType
  readonly images?: readonly string[]
  readonly requestChannel?: RequestChannel
  readonly guestName?: string
  readonly callbackAllowed?: boolean
  readonly privacyConsentAt?: string
}

export interface PublishHelpResponse {
  readonly id: string
  readonly requestNo: string
  readonly status: HelpStatus
  readonly createdAt: string
  readonly followupCode?: string
  readonly closeToken?: string
}

export interface HelpListItem {
  readonly id: string
  readonly requestNo: string
  readonly content: string
  readonly type: HelpRequestType
  readonly status: HelpStatus
  readonly address: string
  readonly createdAt: string
  readonly respondersCount: number
  readonly latestEta: string
  readonly userRole: 'requester' | 'reporter'
}

export interface HelpResponder {
  readonly name: string
  readonly role: string
  readonly distance: number
  readonly estimatedArrivalTime: string
}

export interface RespondToHelpPayload {
  readonly lat: number
  readonly lng: number
  readonly eta?: number
}

export interface CloseHelpPayload {
  readonly status: 'closed' | 'false_alarm'
  readonly note?: string
}

export interface SubmitHelpReportPayload {
  readonly images: readonly string[]
  readonly recordText: string
  readonly isFalseAlarm: boolean
}

export interface SubmitHelpReportResponse {
  readonly id: string
  readonly status: 'pending_review' | 'false_alarm'
  readonly submittedAt: string
}

export interface HelpReportRecord {
  readonly id: string
  readonly images: readonly string[]
  readonly recordText: string
  readonly isFalseAlarm: boolean
  readonly submittedAt: string
}

export interface HelpRequestDetail {
  readonly id: string
  readonly requestNo: string
  readonly content: string
  readonly type: HelpRequestType
  readonly status: HelpStatus
  readonly address: string
  readonly lat: number
  readonly lng: number
  readonly requesterPhone: string
  readonly createdAt: string
  readonly images: readonly string[]
  readonly responders: readonly HelpResponder[]
  readonly estimatedArrivalTime: string
  readonly timeline: readonly HelpTimelineEntry[]
  readonly reports: readonly HelpReportRecord[]
  readonly viewerRole: 'requester' | 'responder'
  readonly canSubmitReport: boolean
}

export interface HelpTimelineEntry {
  readonly action: string
  readonly timestamp: string
  readonly note?: string
}

export interface GuestLookupPayload {
  readonly requestNo: string
  readonly followupCode: string
}

export interface GuestHelpLookupResult {
  readonly requestNo: string
  readonly status: HelpStatus
  readonly content: string
  readonly address: string
  readonly createdAt: string
  readonly respondersCount: number
  readonly latestEta?: string
  readonly canClose: boolean
  readonly closedAt?: string
}

export interface GuestClosePayload {
  readonly requestNo: string
  readonly closeToken: string
  readonly note?: string
}

export interface GuestCloseResult {
  readonly requestNo: string
  readonly status: 'false_alarm'
  readonly closedAt: string
}

// 游客本地会话状态，用于追踪自己的求助
export interface GuestHelpSession {
  readonly requestNo: string
  readonly followupCode: string
  readonly closeToken: string
}
