// 审计日志相关的接口 —— 操作上报，失败不打断主流程
import { getToken } from '@/services/auth-service'
import { post } from '@/services/request'
import { CURRENT_USER_ID_STORAGE_KEY } from '@/utils/constants'
import { USE_MOCK } from '@/utils/runtime-env'

export interface AuditEvent {
  readonly eventType: string
  readonly eventName: string
  readonly targetId?: string
  readonly metadata?: Record<string, unknown>
}

interface AuditClientInfo {
  readonly platform: string
  readonly version: string
  readonly deviceModel: string
}

interface AuditLogEntry extends AuditEvent {
  readonly userId: string
  readonly timestamp: string
  readonly clientInfo: AuditClientInfo
}

interface JwtPayload {
  readonly userId?: string
}

function readStorageString(key: string): string {
  try {
    return (uni.getStorageSync(key) as string) || ''
  } catch {
    return ''
  }
}

function decodeBase64UrlSegment(segment: string): string {
  const normalized = segment
    .replace(/-/g, '+')
    .replace(/_/g, '/')
    .padEnd(Math.ceil(segment.length / 4) * 4, '=')

  const runtime = uni as typeof uni & {
    readonly base64ToArrayBuffer?: (base64: string) => ArrayBuffer
  }

  if (runtime.base64ToArrayBuffer) {
    const bytes = new Uint8Array(runtime.base64ToArrayBuffer(normalized))
    return Array.from(bytes, (byte) => String.fromCharCode(byte)).join('')
  }

  if (typeof atob === 'function') {
    return atob(normalized)
  }

  throw new Error('当前运行环境不支持解码 JWT')
}

function resolveUserIdFromToken(): string {
  const token = getToken()
  const parts = token.split('.')
  if (parts.length < 2) {
    return ''
  }

  try {
    const payload = JSON.parse(decodeBase64UrlSegment(parts[1])) as JwtPayload
    return typeof payload.userId === 'string' ? payload.userId : ''
  } catch {
    return ''
  }
}

function resolveAuditUserId(): string {
  const storageUserId = readStorageString(CURRENT_USER_ID_STORAGE_KEY)
  if (storageUserId) {
    return storageUserId
  }

  return resolveUserIdFromToken()
}

function buildClientInfo(): AuditClientInfo {
  const runtime = uni as typeof uni & {
    readonly getAccountInfoSync?: () => {
      readonly miniProgram?: {
        readonly version?: string
      }
    }
  }
  const system = uni.getSystemInfoSync()
  const accountInfo = runtime.getAccountInfoSync?.()

  return {
    platform: 'mp-weixin',
    version: accountInfo?.miniProgram?.version || '0.1.0',
    deviceModel: system.model || 'unknown',
  }
}

function buildAuditEntries(events: readonly AuditEvent[]): readonly AuditLogEntry[] {
  const userId = resolveAuditUserId()
  if (!userId) {
    console.warn('[Audit] 缺少 userId，已跳过上报', events)
    return []
  }

  const timestamp = new Date().toISOString()
  const clientInfo = buildClientInfo()

  return events.map((event) => ({
    ...event,
    userId,
    timestamp,
    clientInfo,
  }))
}

/** 批量提交审计事件 */
export async function submitAuditBatch(events: readonly AuditEvent[]): Promise<void> {
  if (USE_MOCK) {
    console.log('[Mock Audit]', events)
    return
  }

  const payloadEvents = buildAuditEntries(events)
  if (payloadEvents.length === 0) {
    return
  }

  try {
    await post('/api/audit/batch', { events: payloadEvents }, { skipErrorHandler: true })
  } catch (err) {
    console.warn('[Audit] 提交失败', err)
  }
}

/** 便捷方法：记录查看急救员联系方式 */
export function auditViewContact(rescuerId: string): void {
  submitAuditBatch([
    {
      eventType: 'rescuer',
      eventName: 'rescuer.view_contact',
      targetId: rescuerId,
    },
  ])
}

/** 便捷方法：记录查看急救档案完整号码 */
export function auditViewArchivePhone(archiveId: string, purpose: string): void {
  submitAuditBatch([
    {
      eventType: 'archive',
      eventName: 'archive.view_phone',
      targetId: archiveId,
      metadata: { purpose },
    },
  ])
}

/** 便捷方法：记录拨打急救员电话 */
export function auditCallResponder(rescuerId: string): void {
  submitAuditBatch([
    {
      eventType: 'phone',
      eventName: 'phone.call_responder',
      targetId: rescuerId,
    },
  ])
}
