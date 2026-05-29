// 求助相关的接口 —— 发布、列表、详情、响应、报告、游客查询
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
  SubmitHelpReportPayload,
  SubmitHelpReportResponse,
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
  submitMockHelpReport,
} from '@/mock/help'
import { cachePublishedHelpRecord } from '@/mock/help-storage'
import {
  USE_LOCAL_HELP_READ_MODEL,
  USE_MOCK,
  USE_REAL_WECHAT_FLOW,
} from '@/utils/runtime-env'

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
  ).then((result) => {
    if (USE_LOCAL_HELP_READ_MODEL) {
      cachePublishedHelpRecord(payload, result)
    }
    return result
  })
}

export interface FetchHelpOptions {
  readonly silent?: boolean
}

/** 获取已登录用户求助列表 */
export function fetchHelpList(
  page = 1,
  pageSize = 20,
  opts: FetchHelpOptions = {},
): Promise<PaginatedData<HelpListItem>> {
  if (USE_LOCAL_HELP_READ_MODEL) {
    return Promise.resolve(listMockHelpRequests(page, pageSize))
  }

  return get<PaginatedData<HelpListItem>>(
    '/api/help/list',
    { page, pageSize },
    { skipErrorHandler: opts.silent },
  )
}

/** 获取求助详情 */
export function fetchHelpDetail(
  id: string,
  opts: FetchHelpOptions = {},
): Promise<HelpRequestDetail> {
  if (USE_LOCAL_HELP_READ_MODEL) {
    return Promise.resolve(getMockHelpDetail(id))
  }

  return get<HelpRequestDetail>(`/api/help/${id}`, undefined, { skipErrorHandler: opts.silent })
}

/** 急救员提交救助报告 */
export function submitHelpReport(
  id: string,
  payload: SubmitHelpReportPayload,
): Promise<SubmitHelpReportResponse> {
  if (USE_LOCAL_HELP_READ_MODEL) {
    return Promise.resolve(submitMockHelpReport(id, payload))
  }

  return post<SubmitHelpReportResponse>(
    `/api/help/${id}/report`,
    payload as unknown as Record<string, unknown>,
  )
}

/**
 * 响应求助 —— 只有值班老师或已激活的急救员才能调
 */
export function respondToHelp(
  id: string,
  payload: RespondToHelpPayload,
): Promise<void> {
  if (USE_LOCAL_HELP_READ_MODEL) {
    respondMockHelpRequest(id, payload)
    return Promise.resolve()
  }

  return post(`/api/help/${id}/respond`, payload as unknown as Record<string, unknown>)
}

/** 关闭求助 */
export function closeHelp(id: string, payload: CloseHelpPayload): Promise<void> {
  if (USE_LOCAL_HELP_READ_MODEL) {
    closeMockHelpRequest(id, payload)
    return Promise.resolve()
  }

  return put(`/api/help/${id}/close`, payload as unknown as Record<string, unknown>)
}

/** 游客查询求助状态 */
export function guestQueryHelp(
  payload: GuestLookupPayload,
): Promise<GuestHelpLookupResult> {
  if (USE_LOCAL_HELP_READ_MODEL) {
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
  if (USE_LOCAL_HELP_READ_MODEL) {
    return Promise.resolve(closeMockGuestHelpRequest(payload))
  }

  return post<GuestCloseResult>(
    '/api/help/guest/close',
    payload as unknown as Record<string, unknown>,
  )
}
