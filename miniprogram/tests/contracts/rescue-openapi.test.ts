import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const openApi = readFileSync(
  new URL('../../contracts/openapi.yaml', import.meta.url),
  'utf8',
).replace(/\r\n/g, '\n')

/** Extracts one top-level OpenAPI block so assertions cannot pass on a neighboring path or schema. */
function readBlock(header: string, nextHeader: RegExp): string {
  const start = openApi.indexOf(header)
  if (start < 0) {
    throw new Error(`OpenAPI block not found: ${header.trim()}`)
  }

  const contentStart = start + header.length
  const relativeEnd = openApi.slice(contentStart).search(nextHeader)
  return relativeEnd < 0 ? openApi.slice(start) : openApi.slice(start, contentStart + relativeEnd)
}

/** Reads one HTTP path item. */
function pathBlock(path: string): string {
  return readBlock(`  ${path}:\n`, /^ {2}\/\S+:\n/m)
}

/** Reads one component schema. */
function schemaBlock(name: string): string {
  return readBlock(`    ${name}:\n`, /^ {4}[A-Za-z][A-Za-z0-9]+:\n/m)
}

/** Reads one reusable response component. */
function responseBlock(name: string): string {
  return readBlock(`    ${name}:\n`, /^ {4}[A-Za-z][A-Za-z0-9]+:\n/m)
}

const authenticatedRescuePaths = [
  '/api/help/{id}/session/join',
  '/api/help/{id}/session',
  '/api/help/{id}/session/messages',
  '/api/help/{id}/session/status-events',
  '/api/help/{id}/session/location',
  '/api/help/{id}/rescue-view',
  '/api/rescue-sessions/mine',
] as const

const closedSessionPaths = authenticatedRescuePaths.slice(0, 6)
const resourceWritePaths = authenticatedRescuePaths.slice(2, 5)

const accountDisabledPaths = [
  '/api/rescuers/nearby',
  '/api/rescuers/{id}',
  '/api/help/request',
  '/api/help/list',
  '/api/user/profile',
  '/api/user/archives',
  '/api/user/archives/{id}',
  '/api/user/archives/{id}/phone',
  '/api/user/volunteer-applications',
  '/api/user/school-bindings',
  '/api/user/responder-claims',
  '/api/auth/wechat-login',
  '/api/auth/wechat-phone',
  '/api/auth/refresh',
  '/api/wechat/subscriptions',
] as const

const requiredFields = [
  ['RescueSos', 'required: [lat, lng, address]'],
  ['RescueLocation', 'required: [lat, lng, reportedAt]'],
  [
    'RescueTiming',
    'required: [signalLostAfterSeconds, locationGoneAfterSeconds, locationReportIntervalSeconds]',
  ],
  [
    'RescueParticipant',
    'required: [openId, displayName, joinedAt, takenAed, arrived, signal, location]',
  ],
  ['RescueMe', 'required: [openId, takenAed, arrived, lastReadSeq]'],
  [
    'RescueMessage',
    'required: [id, seq, kind, senderOpenId, senderName, text, eventType, action, createdAt]',
  ],
  [
    'RescueSessionSnapshot',
    'required: [helpId, requestNo, helpStatus, sos, me, participants, messages, latestSeq, serverTime, timing]',
  ],
  ['SendRescueMessagePayload', 'required: [text]'],
  ['SendRescueStatusEventPayload', 'required: [eventType, action]'],
  ['RescueMyState', 'required: [openId, takenAed, arrived]'],
  ['RescueStatusEventResult', 'required: [appended, event, me]'],
  ['ReportRescueLocationPayload', 'required: [lat, lng]'],
  ['ReportRescueLocationResult', 'required: [reportedAt]'],
  [
    'RescueViewParticipant',
    'required: [participantId, displayName, takenAed, arrived, signal, location]',
  ],
  ['RescueViewEvent', 'required: [seq, eventType, action, actorName, createdAt]'],
  [
    'RescueView',
    'required: [helpId, helpStatus, sos, respondersCount, participants, events, serverTime, timing]',
  ],
  [
    'RescueMineItem',
    'required: [helpId, requestNo, helpStatus, type, address, joined, joinedAt, unreadCount, lastActivityAt]',
  ],
  ['RescueMineData', 'required: [items]'],
  [
    'AdminRescueSessionListItem',
    'required: [helpId, requestNo, helpStatus, type, address, createdAt, participantsCount, messagesCount, lastActivityAt]',
  ],
  ['AdminRescueSessionListData', 'required: [items, total, page, pageSize, totalPages]'],
  ['AdminRescueSessionRequester', 'required: [openId, displayName]'],
  [
    'AdminRescueSessionDetail',
    'required: [helpId, requestNo, helpStatus, sos, requester, closedAt, closeNote, participants, messages, serverTime]',
  ],
] as const

const dataResponses = [
  'RescueSessionSnapshotResponse',
  'RescueMessageResponse',
  'RescueStatusEventResultResponse',
  'ReportRescueLocationResultResponse',
  'RescueViewResponse',
  'RescueMineResponse',
  'AdminRescueSessionListResponse',
  'AdminRescueSessionDetailResponse',
] as const

const coordinateSchemas = ['RescueSos', 'RescueLocation', 'ReportRescueLocationPayload'] as const

const int64Schemas = [
  'RescueMe',
  'RescueMessage',
  'RescueSessionSnapshot',
  'RescueViewEvent',
] as const

const nullableCounts = [
  ['RescueParticipant', 1],
  ['RescueMessage', 3],
  ['RescueStatusEventResult', 1],
  ['RescueViewParticipant', 1],
  ['RescueMineItem', 2],
  ['AdminRescueSessionListItem', 1],
  ['AdminRescueSessionRequester', 1],
  ['AdminRescueSessionDetail', 2],
] as const

describe('issue #1 OpenAPI contract', () => {
  it('uses the current production server', () => {
    expect(openApi).toContain('url: REDACTED')
    expect(openApi).not.toContain('url: https://api.aed.qtnu.edu.cn')
  })

  it('documents authentication and closed-session failures per rescue path', () => {
    authenticatedRescuePaths.forEach((path) => {
      expect(pathBlock(path)).toMatch(/'401':\n\s+\$ref: '#\/components\/responses\/Unauthorized'/)
    })
    closedSessionPaths.forEach((path) => {
      expect(pathBlock(path)).toMatch(
        /'409':\n\s+\$ref: '#\/components\/responses\/RescueSessionClosed'/,
      )
    })
    resourceWritePaths.forEach((path) => {
      expect(pathBlock(path)).toMatch(/'404':\n\s+\$ref: '#\/components\/responses\/NotFound'/)
    })
  })

  it('documents cursor and admin query validation without silent clamping', () => {
    const snapshotPath = pathBlock('/api/help/{id}/session')
    const adminPath = pathBlock('/api/admin/rescue-sessions')

    expect(snapshotPath).toMatch(/name: afterSeq[\s\S]*?format: int64[\s\S]*?minimum: 0/)
    expect(adminPath).toContain('enum: [active, history]')
    expect(adminPath).toMatch(/name: page\n[\s\S]*?minimum: 1/)
    expect(adminPath).toMatch(/name: pageSize\n[\s\S]*?minimum: 1[\s\S]*?maximum: 100/)
    expect(adminPath).toMatch(/'400':\n\s+\$ref: '#\/components\/responses\/BadRequest'/)
  })

  it('keeps all issue #1 response fields explicit', () => {
    requiredFields.forEach(([schema, required]) => {
      expect(schemaBlock(schema)).toContain(required)
    })
    dataResponses.forEach((schema) => {
      expect(schemaBlock(schema)).toContain('required: [data]')
    })
  })

  it('publishes timing to both live projections with positive values', () => {
    const timing = schemaBlock('RescueTiming')

    expect(timing.match(/minimum: 1/g)).toHaveLength(3)
    expect(schemaBlock('RescueSessionSnapshot')).toContain(
      "$ref: '#/components/schemas/RescueTiming'",
    )
    expect(schemaBlock('RescueView')).toContain("$ref: '#/components/schemas/RescueTiming'")
  })

  it('bounds every rescue coordinate pair', () => {
    coordinateSchemas.forEach((schema) => {
      const block = schemaBlock(schema)
      expect(block).toMatch(/lat:[\s\S]*?minimum: -90[\s\S]*?maximum: 90/)
      expect(block).toMatch(/lng:[\s\S]*?minimum: -180[\s\S]*?maximum: 180/)
    })
  })

  it('uses int64 sequence fields and preserves nullable response keys', () => {
    int64Schemas.forEach((schema) => {
      expect(schemaBlock(schema)).toContain('format: int64')
    })
    nullableCounts.forEach(([schema, count]) => {
      expect(schemaBlock(schema).match(/nullable: true/g)).toHaveLength(count)
    })
  })

  it('defines code 1016 as a read-and-write closed-session conflict', () => {
    const response = responseBlock('RescueSessionClosed')

    expect(response).toContain('HTTP 409 / code 1016')
    expect(response).toContain('不允许继续读取或写入')
  })

  it('documents account-disabled failures on every current-user surface', () => {
    accountDisabledPaths.forEach((path) => {
      expect(pathBlock(path)).toMatch(
        /'403':\n\s+\$ref: '#\/components\/responses\/AccountDisabled'/,
      )
    })
    expect(responseBlock('AccountDisabled')).toContain('enum: [1018]')
  })

  it('keeps business denial and account disablement distinct on mixed 403 paths', () => {
    authenticatedRescuePaths.forEach((path) => {
      expect(pathBlock(path)).toMatch(
        /'403':\n\s+\$ref: '#\/components\/responses\/SmallProgramForbidden'/,
      )
    })
    expect(responseBlock('SmallProgramForbidden')).toContain('enum: [1003, 1017, 1018]')
  })

  it('documents admin image scenes and the bootstrap conflict code', () => {
    const imageUpload = pathBlock('/api/upload/image')
    expect(imageUpload).toContain('- adminSessionKey: []')
    expect(imageUpload).toContain(
      'enum: [avatar, report, help, volunteer, device, rescuer, course, quick-action]',
    )
    expect(pathBlock('/api/admin/auth/bootstrap')).toContain(
      "$ref: '#/components/responses/AdminAlreadyInitialized'",
    )
    expect(responseBlock('AdminAlreadyInitialized')).toContain('enum: [2019]')
  })
})
