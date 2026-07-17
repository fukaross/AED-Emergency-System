// 急救会话管理端监控类型（BackEnd.md §4.12 /api/admin/rescue-sessions）

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
  readonly helpStatus: string
  readonly type: string
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

export interface AdminRescueMessage {
  readonly id: string
  readonly seq: number
  readonly kind: RescueMessageKind
  readonly senderOpenId: string
  readonly senderName: string
  readonly text: string | null
  readonly eventType: RescueEventType | null
  readonly action: RescueEventAction | null
  readonly createdAt: string
}

export interface AdminRescueSessionDetail {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: string
  readonly sos: RescueSos
  readonly requester: AdminRescueRequester
  readonly closedAt: string | null
  readonly closeNote: string | null
  readonly participants: AdminRescueParticipant[]
  readonly messages: AdminRescueMessage[]
  readonly serverTime: string
}
