// 急救会话相关的类型 —— 会话快照、消息、状态事件、只读视图、我的会话（BackEnd.md §4.5A）

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

export interface RescueParticipant {
  readonly openId: string
  readonly displayName: string
  readonly joinedAt: string
  readonly takenAed: boolean
  readonly arrived: boolean
  readonly signal: RescueSignal
  readonly location: RescueLocation | null
}

export interface RescueMe {
  readonly openId: string
  readonly takenAed: boolean
  readonly arrived: boolean
  readonly lastReadSeq: number
}

export interface RescueMessage {
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

export interface RescueSessionSnapshot {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: string
  readonly sos: RescueSos
  readonly me: RescueMe
  readonly participants: readonly RescueParticipant[]
  readonly messages: readonly RescueMessage[]
  readonly latestSeq: number
  readonly serverTime: string
}

export interface RescueMyState {
  readonly openId: string
  readonly takenAed: boolean
  readonly arrived: boolean
}

export interface RescueStatusEventResult {
  readonly appended: boolean
  readonly event: RescueMessage | null
  readonly me: RescueMyState
}

export interface RescueViewParticipant {
  readonly displayName: string
  readonly takenAed: boolean
  readonly arrived: boolean
  readonly signal: RescueSignal
  readonly location: RescueLocation | null
}

export interface RescueViewEvent {
  readonly seq: number
  readonly eventType: RescueEventType
  readonly action: RescueEventAction
  readonly actorName: string
  readonly createdAt: string
}

export interface RescueView {
  readonly helpId: string
  readonly helpStatus: string
  readonly sos: RescueSos
  readonly respondersCount: number
  readonly participants: readonly RescueViewParticipant[]
  readonly events: readonly RescueViewEvent[]
  readonly serverTime: string
}

export interface RescueMineItem {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: string
  readonly type: string
  readonly address: string
  readonly joinedAt: string
  readonly unreadCount: number
  readonly lastActivityAt: string | null
}

export interface RescueLocationReportResult {
  readonly reportedAt: string
}

// WS 扇出信封（ADR 0001）：仅作推送提示，权威数据以 HTTP 快照为准
export type RescueSocketEnvelopeType = 'message' | 'participant' | 'help_status'

export interface RescueSocketEnvelope {
  readonly type: RescueSocketEnvelopeType
  readonly data: unknown
}

export interface RescueHelpStatusPayload {
  readonly helpStatus: string
}
