/**
 * 求助相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.5
 *
 * POST /api/help/request       — 发布求助 [登录可选]
 * GET  /api/help/list           — 已登录用户求助列表 [登录]
 * GET  /api/help/:id            — 求助详情 [登录]
 * POST /api/help/:id/respond    — 响应求助 [登录 + 权限]
 * PUT  /api/help/:id/close      — 关闭求助 [登录]
 * POST /api/help/guest/query    — 游客查询 [公开]
 * POST /api/help/guest/close    — 游客关闭误报 [公开]
 *
 * [G-04] POST /api/help/{id}/respond 仅值班老师
 *        或学校导入且 status=active 的急救员可调用。
 *        其他登录用户返回 1003 (FORBIDDEN)。
 */
import { get, post, put } from '@/services/request'
import type {
  CloseHelpPayload,
  PublishHelpPayload,
  PublishHelpResponse,
  HelpListItem,
  HelpRequestDetail,
  GuestLookupPayload,
  GuestHelpLookupResult,
  GuestClosePayload,
  GuestCloseResult,
  RespondToHelpPayload,
} from '@/types/help'
import type { PaginatedData } from '@/types/common'
import {
  closeMockHelpRequest,
  closeMockGuestHelpRequest,
  getMockHelpDetail,
  listMockHelpRequests,
  publishMockHelpRequest,
  queryMockGuestHelp,
  respondMockHelpRequest,
} from '@/mock/help'
import { USE_MOCK, USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'

/** 发布求助 */
export function publishHelp(
  payload: PublishHelpPayload,
): Promise<PublishHelpResponse> {
  if (USE_MOCK && !USE_REAL_WECHAT_FLOW) {
    return Promise.resolve(publishMockHelpRequest(payload))
  }

  return post<PublishHelpResponse>(
    '/api/help/request',
    payload as unknown as Record<string, unknown>,
  )
}

/** 获取已登录用户求助列表 */
export function fetchHelpList(
  page = 1,
  pageSize = 20,
): Promise<PaginatedData<HelpListItem>> {
  if (USE_MOCK) {
    return Promise.resolve(listMockHelpRequests(page, pageSize))
  }

  return get<PaginatedData<HelpListItem>>('/api/help/list', { page, pageSize })
}

/** 获取求助详情 */
export function fetchHelpDetail(id: string): Promise<HelpRequestDetail> {
  if (USE_MOCK) {
    return Promise.resolve(getMockHelpDetail(id))
  }

  return get<HelpRequestDetail>(`/api/help/${id}`)
}

/**
 * 响应求助
 *
 * [G-04] 仅值班老师或学校导入且 status=active 的急救员可调用。
 * 其他登录用户返回错误码 1003 (FORBIDDEN)。
 */
export function respondToHelp(
  id: string,
  payload: RespondToHelpPayload,
): Promise<void> {
  if (USE_MOCK) {
    respondMockHelpRequest(id, payload)
    return Promise.resolve()
  }

  return post(`/api/help/${id}/respond`, payload as unknown as Record<string, unknown>)
}

/** 关闭求助 */
export function closeHelp(id: string, payload: CloseHelpPayload): Promise<void> {
  if (USE_MOCK) {
    closeMockHelpRequest(id, payload)
    return Promise.resolve()
  }

  return put(`/api/help/${id}/close`, payload as unknown as Record<string, unknown>)
}

/** 游客查询求助状态 */
export function guestQueryHelp(
  payload: GuestLookupPayload,
): Promise<GuestHelpLookupResult> {
  if (USE_MOCK) {
    return Promise.resolve(queryMockGuestHelp(payload))
  }

  return post<GuestHelpLookupResult>(
    '/api/help/guest/query',
    payload as unknown as Record<string, unknown>,
  )
}

/** 游客关闭误报 */
export function guestCloseHelp(
  payload: GuestClosePayload,
): Promise<GuestCloseResult> {
  if (USE_MOCK) {
    return Promise.resolve(closeMockGuestHelpRequest(payload))
  }

  return post<GuestCloseResult>(
    '/api/help/guest/close',
    payload as unknown as Record<string, unknown>,
  )
}
