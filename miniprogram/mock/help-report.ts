import type {
  SubmitHelpReportPayload,
  SubmitHelpReportResponse,
} from '@/types/help'
import {
  createMockHelpError,
  loadMockHelpRecords,
  saveMockHelpRecords,
  withUpdatedHelpRecord,
} from './help-storage'

export function submitMockHelpReport(
  id: string,
  payload: SubmitHelpReportPayload,
): SubmitHelpReportResponse {
  validateReportPayload(payload)

  const records = loadMockHelpRecords()
  const submittedAt = new Date().toISOString()
  let reportId = ''
  let nextStatus: SubmitHelpReportResponse['status'] = 'pending_review'

  const nextRecords = records.map((item) => {
    if (item.id !== id && item.requestNo !== id) return item
    if (item.status === 'closed' || item.status === 'false_alarm') {
      throw createMockHelpError('求助已结束')
    }

    reportId = `report-${Date.now()}`
    nextStatus = payload.isFalseAlarm ? 'false_alarm' : 'pending_review'

    return withUpdatedHelpRecord(item, {
      status: nextStatus,
      closedAt: payload.isFalseAlarm ? submittedAt : item.closedAt,
      reports: [...item.reports, { id: reportId, submittedAt, ...payload }],
      timeline: [
        ...item.timeline,
        {
          action: nextStatus,
          timestamp: submittedAt,
          note: payload.recordText,
        },
      ],
    })
  })

  if (!reportId) throw createMockHelpError('求助不存在')

  saveMockHelpRecords(nextRecords)
  return { id: reportId, status: nextStatus, submittedAt }
}

function validateReportPayload(payload: SubmitHelpReportPayload): void {
  if (!payload.images.length) {
    throw createMockHelpError('请至少上传一张现场图片')
  }
  if (!payload.recordText.trim()) {
    throw createMockHelpError('请填写救助记录')
  }
}
