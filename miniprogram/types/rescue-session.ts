// 急救会话相关的类型 —— 会话快照、消息、状态事件、只读视图、我的会话（BackEnd.md §4.5A）

export type RescueSignal = 'ok' | 'lost' | 'gone'

export type RescueMessageKind = 'text' | 'status_event'

export type RescueEventType = 'joined' | 'taken_aed' | 'arrived'

export type RescueMutableEventType = Exclude<RescueEventType, 'joined'>

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

export interface RescueTiming {
  readonly signalLostAfterSeconds: number
  readonly locationGoneAfterSeconds: number
  readonly locationReportIntervalSeconds: number
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
  readonly timing: RescueTiming
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
  readonly participantId: string
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
  readonly timing: RescueTiming
}

export interface RescueMineItem {
  readonly helpId: string
  readonly requestNo: string
  readonly helpStatus: string
  readonly type: string
  readonly address: string
  readonly joined: boolean
  readonly joinedAt: string | null
  readonly unreadCount: number
  readonly lastActivityAt: string | null
}

export interface RescueLocationReportResult {
  readonly reportedAt: string
}

// WS 扇出信封（ADR 0001）：非终态只作提示；关闭门后的最终 help_status 控制客户端立即停止实时任务
export interface RescueHelpStatusPayload {
  readonly helpStatus: 'published' | 'responding' | 'pending_review' | 'closed' | 'false_alarm'
}

export type RescueSocketEnvelope =
  | { readonly type: 'message'; readonly data: RescueMessage }
  | { readonly type: 'participant'; readonly data: RescueParticipant }
  | { readonly type: 'help_status'; readonly data: RescueHelpStatusPayload }
