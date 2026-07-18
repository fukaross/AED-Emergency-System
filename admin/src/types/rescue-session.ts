// 急救会话管理端监控类型（BackEnd.md §4.12 /api/admin/rescue-sessions）
import type { HelpRequestStatus, HelpRequestType } from '@/types/help'

export type RescueSignal = 'ok' | 'lost' | 'gone'

export type RescueMessageKind = 'text' | 'status_event'

export type RescueEventType = 'joined' | 'taken_aed' | 'arrived'

export type RescueEventAction = 'mark' | 'revoke'

export interface RescueSos {
  readonly lat: number
  readonly lng: number
  readonly address: string
}

export interface RescueLocation {
  readonly lat: number
  readonly lng: number
  readonly reportedAt: string
}

export interface AdminRescueSessionListItem {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: HelpRequestStatus
  readonly type: HelpRequestType
  readonly address: string
  readonly createdAt: string
  readonly participantsCount: number
  readonly messagesCount: number
  readonly lastActivityAt: string | null
}

export interface AdminRescueSessionListResponse {
  readonly items: AdminRescueSessionListItem[]
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
}

export interface AdminRescueRequester {
  readonly openId: string | null
  readonly displayName: string
}

export interface AdminRescueParticipant {
  readonly openId: string
  readonly displayName: string
  readonly joinedAt: string
  readonly takenAed: boolean
  readonly arrived: boolean
  readonly signal: RescueSignal
  readonly location: RescueLocation | null
}

interface AdminRescueMessageBase {
  readonly id: string
  readonly seq: number
  readonly senderOpenId: string
  readonly senderName: string
  readonly createdAt: string
}

export interface AdminRescueTextMessage extends AdminRescueMessageBase {
  readonly kind: 'text'
  readonly text: string
  readonly eventType: null
  readonly action: null
}

export interface AdminRescueStatusEventMessage extends AdminRescueMessageBase {
  readonly kind: 'status_event'
  readonly text: null
  readonly eventType: RescueEventType
  readonly action: RescueEventAction
}

export type AdminRescueMessage = AdminRescueTextMessage | AdminRescueStatusEventMessage

export interface AdminRescueSessionDetail {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: HelpRequestStatus
  readonly sos: RescueSos
  readonly requester: AdminRescueRequester
  readonly closedAt: string | null
  readonly closeNote: string | null
  readonly participants: AdminRescueParticipant[]
  readonly messages: AdminRescueMessage[]
  readonly serverTime: string
}
