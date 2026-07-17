// 急救会话相关的接口 —— 进入、快照/增量、消息、状态事件、位置、只读视图、我的会话（BackEnd.md §4.5A）
import { get, post, put } from '@/services/request'
import type { GeoPoint } from '@/types/common'
import type {
  RescueEventAction,
  RescueEventType,
  RescueLocationReportResult,
  RescueMessage,
  RescueMineItem,
  RescueSessionSnapshot,
  RescueStatusEventResult,
  RescueView,
} from '@/types/rescue-session'
import {
  fetchMockRescueSession,
  fetchMockRescueView,
  joinMockRescueSession,
  listMockMineRescueSessions,
  postMockRescueMessage,
  postMockRescueStatusEvent,
  reportMockRescueLocation,
} from '@/mock/rescue-session'
import { SESSION_BACKEND_READY, USE_MOCK } from '@/utils/runtime-env'

const USE_SESSION_MOCK = !SESSION_BACKEND_READY && USE_MOCK

export interface FetchRescueSessionOptions {
  readonly afterSeq?: number
  readonly silent?: boolean
}

/** 进入会话（幂等，首次进入自动记录 joined 并可能触发 published → responding） */
export function joinRescueSession(helpId: string): Promise<RescueSessionSnapshot> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(joinMockRescueSession(helpId))
  }
  return post<RescueSessionSnapshot>(`/api/help/${helpId}/session/join`)
}

/** 拉取会话快照 / 增量（afterSeq 之后的消息；参与者与派生状态始终全量） */
export function fetchRescueSession(
  helpId: string,
  options: FetchRescueSessionOptions = {},
): Promise<RescueSessionSnapshot> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(fetchMockRescueSession(helpId, options.afterSeq))
  }
  return get<RescueSessionSnapshot>(
    `/api/help/${helpId}/session`,
    options.afterSeq === undefined ? undefined : { afterSeq: options.afterSeq },
    { skipErrorHandler: options.silent },
  )
}

/** 发送文字消息（1~500 字符） */
export function postRescueMessage(helpId: string, text: string): Promise<RescueMessage> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(postMockRescueMessage(helpId, text))
  }
  return post<RescueMessage>(`/api/help/${helpId}/session/messages`, { text })
}

/** 发送 / 撤销状态事件（taken_aed / arrived；服务端幂等去抖） */
export function postRescueStatusEvent(
  helpId: string,
  eventType: RescueEventType,
  action: RescueEventAction,
): Promise<RescueStatusEventResult> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(postMockRescueStatusEvent(helpId, eventType, action))
  }
  return post<RescueStatusEventResult>(`/api/help/${helpId}/session/status-events`, {
    eventType,
    action,
  })
}

/** 上报当前位置（覆盖式，只保留最新一份） */
export function reportRescueLocation(
  helpId: string,
  point: GeoPoint,
): Promise<RescueLocationReportResult> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(reportMockRescueLocation(helpId, point))
  }
  return put<RescueLocationReportResult>(`/api/help/${helpId}/session/location`, {
    lat: point.lat,
    lng: point.lng,
  })
}

/** 求助人只读实时视图（不含文字消息与参与者 openId） */
export function fetchRescueView(helpId: string, options: { silent?: boolean } = {}): Promise<RescueView> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(fetchMockRescueView(helpId))
  }
  return get<RescueView>(`/api/help/${helpId}/rescue-view`, undefined, {
    skipErrorHandler: options.silent,
  })
}

/** 我参与的进行中会话（主页「进行中会话」入口，含未读数） */
export function fetchMineRescueSessions(options: { silent?: boolean } = {}): Promise<RescueMineItem[]> {
  if (USE_SESSION_MOCK) {
    return Promise.resolve(listMockMineRescueSessions())
  }
  return get<{ items: RescueMineItem[] }>('/api/rescue-sessions/mine', undefined, {
    skipErrorHandler: options.silent,
  }).then((result) => result.items)
}
