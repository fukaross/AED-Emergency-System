/**
 * 求助相关类型定义
 *
 * 来源：CLAUDE.md §8.1 (help 系列接口)
 * 来源：BackEnd.md §4.5
 */

/** 求助类型 */
export type HelpRequestType = 'help_hand' | 'call_120' | 'call_school' | 'general'

/** 求助状态 */
export type HelpStatus = 'published' | 'closed' | 'false_alarm'

/** 求助渠道 */
export type RequestChannel = 'user' | 'guest'

/** 发布求助请求 */
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

/** 发布求助响应 */
export interface PublishHelpResponse {
  readonly id: string
  readonly requestNo: string
  readonly status: HelpStatus
  readonly createdAt: string
  readonly followupCode?: string
  readonly closeToken?: string
}

/** 求助列表项 */
export interface HelpListItem {
  readonly id: string
  readonly content: string
  readonly type: HelpRequestType
  readonly status: HelpStatus
  readonly address: string
  readonly createdAt: string
  readonly respondersCount: number
  readonly latestEta: string
}

/** 求助响应者 */
export interface HelpResponder {
  readonly name: string
  readonly role: string
  readonly distance: number
  readonly estimatedArrivalTime: string
}

/** 响应求助请求 */
export interface RespondToHelpPayload {
  readonly lat: number
  readonly lng: number
  readonly eta?: number
}

/** 关闭求助请求 */
export interface CloseHelpPayload {
  readonly status: 'closed' | 'false_alarm'
  readonly note?: string
}

/** 求助详情 */
export interface HelpRequestDetail {
  readonly id: string
  readonly content: string
  readonly type: HelpRequestType
  readonly status: HelpStatus
  readonly address: string
  readonly createdAt: string
  readonly images: readonly string[]
  readonly responders: readonly HelpResponder[]
  readonly estimatedArrivalTime: string
  readonly timeline: readonly HelpTimelineEntry[]
}

/** 时间线条目 */
export interface HelpTimelineEntry {
  readonly action: string
  readonly timestamp: string
  readonly note?: string
}

/** 游客查询请求 */
export interface GuestLookupPayload {
  readonly requestNo: string
  readonly followupCode: string
}

/** 游客查询响应 */
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

/** 游客关闭请求 */
export interface GuestClosePayload {
  readonly requestNo: string
  readonly closeToken: string
  readonly note?: string
}

/** 游客关闭响应 */
export interface GuestCloseResult {
  readonly requestNo: string
  readonly status: 'false_alarm'
  readonly closedAt: string
}

/** 游客会话状态（前端本地维护） */
export interface GuestHelpSession {
  readonly requestNo: string
  readonly followupCode: string
  readonly closeToken: string
}
