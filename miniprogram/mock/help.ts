// 求助链路的 Mock，没后端的时候也能跑通全流程
import type {
  CloseHelpPayload,
  GuestClosePayload,
  GuestCloseResult,
  GuestHelpLookupResult,
  GuestLookupPayload,
  HelpListItem,
  HelpRequestDetail,
  PublishHelpPayload,
  PublishHelpResponse,
  RespondToHelpPayload,
} from '@/types/help'
import type { PaginatedData } from '@/types/common'
import { dispatchMockHelpNotifications } from './help-notification'
import {
  DEFAULT_MOCK_REQUESTER_PHONE,
  createMockHelpError,
  ensureHelpRecordById,
  ensureHelpRecordByRequestNo,
  isActiveMockHelpStatus,
  loadMockHelpRecords,
  randomCode,
  saveMockHelpRecords,
  withUpdatedHelpRecord,
} from './help-storage'
import type { MockHelpRecord } from './help-storage'

export { submitMockHelpReport } from './help-report'

function formatEtaLabel(eta?: number): string {
  if (!eta || eta <= 0) {
    return '3分钟'
  }
  return `${Math.max(1, Math.round(eta / 60))}分钟`
}

export function publishMockHelpRequest(
  payload: PublishHelpPayload,
): PublishHelpResponse {
  const existingRecords = loadMockHelpRecords()
  if (existingRecords.some((item) => isActiveMockHelpStatus(item.status))) {
    throw createMockHelpError('已有进行中的求助')
  }

  const createdAt = new Date().toISOString()
  const requestChannel = payload.requestChannel === 'guest' ? 'guest' : 'user'
  const nextRecord: MockHelpRecord = {
    id: `help-${Date.now()}`,
    requestNo: randomCode('HELP', 8),
    content: payload.content,
    type: payload.type,
    status: 'published',
    address: payload.address,
    lat: payload.lat,
    lng: payload.lng,
    requesterPhone: DEFAULT_MOCK_REQUESTER_PHONE,
    createdAt,
    images: payload.images ?? [],
    requestChannel,
    followupCode: requestChannel === 'guest' ? randomCode('Q', 6) : undefined,
    closeToken: requestChannel === 'guest' ? randomCode('CLOSE', 8) : undefined,
    responders: [],
    estimatedArrivalTime: '待响应',
    timeline: [{ action: 'published', timestamp: createdAt }],
    reports: [],
  }

  const records = [nextRecord, ...existingRecords]
  saveMockHelpRecords(records)
  dispatchMockHelpNotifications({
    helpRequestId: nextRecord.id,
    requestNo: nextRecord.requestNo,
    payload,
  })

  console.log('[Mock Help API] publish', nextRecord.requestNo)

  return {
    id: nextRecord.id,
    requestNo: nextRecord.requestNo,
    status: nextRecord.status,
    createdAt: nextRecord.createdAt,
    followupCode: nextRecord.followupCode,
    closeToken: nextRecord.closeToken,
  }
}

export function listMockHelpRequests(
  page: number,
  pageSize: number,
): PaginatedData<HelpListItem> {
  const records = loadMockHelpRecords()
  const items = records.map((item) => ({
    id: item.id,
    requestNo: item.requestNo,
    content: item.content,
    type: item.type,
    status: item.status,
    address: item.address,
    createdAt: item.createdAt,
    respondersCount: item.responders.length,
    latestEta: item.estimatedArrivalTime,
    userRole: 'requester' as const,
  }))
  const start = (page - 1) * pageSize
  const pagedItems = items.slice(start, start + pageSize)

  return {
    items: pagedItems,
    total: items.length,
    page,
    pageSize,
    totalPages: Math.max(1, Math.ceil(items.length / pageSize)),
  }
}

export function getMockHelpDetail(id: string): HelpRequestDetail {
  const record = ensureHelpRecordById(id)
  return {
    id: record.id,
    requestNo: record.requestNo,
    content: record.content,
    type: record.type,
    status: record.status,
    address: record.address,
    lat: record.lat,
    lng: record.lng,
    requesterPhone: record.requesterPhone,
    createdAt: record.createdAt,
    images: record.images,
    responders: record.responders,
    estimatedArrivalTime: record.estimatedArrivalTime,
    timeline: record.timeline,
    reports: record.reports,
    viewerRole: 'requester',
    canSubmitReport: false,
  }
}

export function queryMockGuestHelp(
  payload: GuestLookupPayload,
): GuestHelpLookupResult {
  const record = ensureHelpRecordByRequestNo(payload.requestNo)
  if (record.followupCode !== payload.followupCode) {
    throw createMockHelpError('查询码不正确')
  }

  return {
    requestNo: record.requestNo,
    status: record.status,
    content: record.content,
    address: record.address,
    createdAt: record.createdAt,
    respondersCount: record.responders.length,
    latestEta:
      record.estimatedArrivalTime === '待响应' ? undefined : record.estimatedArrivalTime,
    canClose: record.status === 'published' && Boolean(record.closeToken),
    closedAt: record.closedAt,
  }
}

export function closeMockHelpRequest(
  id: string,
  payload: CloseHelpPayload,
): void {
  const records = loadMockHelpRecords()
  const closedAt = new Date().toISOString()
  let found = false

  const nextRecords = records.map((item) => {
    if (item.id !== id) {
      return item
    }

    found = true

    return withUpdatedHelpRecord(item, {
      status: payload.status,
      closedAt,
      timeline: [
        ...item.timeline,
        { action: payload.status, timestamp: closedAt, note: payload.note },
      ],
    })
  })

  if (!found) {
    throw createMockHelpError('求助不存在')
  }

  saveMockHelpRecords(nextRecords)
}

export function closeMockGuestHelpRequest(
  payload: GuestClosePayload,
): GuestCloseResult {
  const records = loadMockHelpRecords()
  const closedAt = new Date().toISOString()
  let targetRequestNo = ''

  const nextRecords = records.map((item) => {
    if (item.requestNo !== payload.requestNo) {
      return item
    }

    if (item.closeToken !== payload.closeToken) {
      throw createMockHelpError('关闭凭证无效')
    }

    targetRequestNo = item.requestNo

    return withUpdatedHelpRecord(item, {
      status: 'false_alarm',
      closedAt,
      timeline: [
        ...item.timeline,
        { action: 'false_alarm', timestamp: closedAt, note: payload.note },
      ],
    })
  })

  if (!targetRequestNo) {
    throw createMockHelpError('求助不存在')
  }

  saveMockHelpRecords(nextRecords)

  return {
    requestNo: targetRequestNo,
    status: 'false_alarm',
    closedAt,
  }
}

export function respondMockHelpRequest(
  id: string,
  payload: RespondToHelpPayload,
): void {
  const records = loadMockHelpRecords()
  const current = ensureHelpRecordById(id)
  const now = new Date().toISOString()
  const responder = {
    name: '模拟急救员',
    role: 'rescuer',
    distance: 120,
    estimatedArrivalTime: formatEtaLabel(payload.eta),
  }

  const nextRecords = records.map((item) => {
    if (item.id !== current.id) {
      return item
    }

    return {
      ...item,
      responders: [...item.responders, responder],
      estimatedArrivalTime: responder.estimatedArrivalTime,
      timeline: [...item.timeline, { action: 'responded', timestamp: now }],
    }
  })

  saveMockHelpRecords(nextRecords)
}
