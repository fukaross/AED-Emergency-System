import type { GeoPoint } from '@/types/common'
import type { HelpStatus } from '@/types/help'
import type { RescueTiming, RescueViewParticipant } from '@/types/rescue-session'
import { ERROR_CODES } from '@/utils/constants'
import { computeSignal } from '@/utils/rescue-signal'

const ACTIVE_HELP_STATUSES: readonly HelpStatus[] = [
  'published',
  'responding',
  'pending_review',
]

interface RescueViewApplyContext {
  readonly requestRevision: number
  readonly currentRevision: number
  readonly requestedHelpId: string
  readonly currentHelpId: string | undefined
  readonly helpStatus: HelpStatus | undefined
}

interface RescueMapPointSources {
  readonly userLocation: GeoPoint | null
  readonly sos: GeoPoint | null
  readonly participants: readonly RescueViewParticipant[]
}

interface RescueViewProjectionContext {
  readonly participants: readonly RescueViewParticipant[]
  readonly timing: RescueTiming
  readonly serverClockOffsetMs: number
  readonly localNow: number
}

export function isActiveHelpStatus(status: string | undefined): status is HelpStatus {
  return status !== undefined && ACTIVE_HELP_STATUSES.includes(status as HelpStatus)
}

export function canShowReportAction(
  detail: { readonly canSubmitReport: boolean } | null,
): boolean {
  return detail?.canSubmitReport === true
}

export function canApplyRescueView(context: RescueViewApplyContext): boolean {
  return context.requestRevision === context.currentRevision
    && context.requestedHelpId === context.currentHelpId
    && isActiveHelpStatus(context.helpStatus)
}

export function shouldClearRescueViewForError(error: unknown): boolean {
  if (typeof error !== 'object' || error === null) return false
  const apiError = error as {
    readonly code?: number
    readonly statusCode?: number
  }
  return apiError.code === ERROR_CODES.RESCUE_NOT_PARTICIPANT
    || apiError.code === ERROR_CODES.UNAUTHORIZED
    || apiError.code === ERROR_CODES.ACCOUNT_DISABLED
    || apiError.statusCode === 401
    || apiError.statusCode === 403
    || apiError.statusCode === 409
}

/** 使用服务端校准时钟推进只读视图信号，并在 gone 时立即清除精确坐标。 */
export function projectRescueViewParticipants(
  context: RescueViewProjectionContext,
): RescueViewParticipant[] {
  const calibratedNow = context.localNow + context.serverClockOffsetMs
  return context.participants.map((participant) => {
    if (!participant.location) return participant
    const signal = computeSignal(
      participant.location.reportedAt,
      calibratedNow,
      context.timing.signalLostAfterSeconds,
      context.timing.locationGoneAfterSeconds,
    )
    return signal === 'gone'
      ? { ...participant, signal, location: null }
      : { ...participant, signal }
  })
}

export function collectRescueMapPoints(
  sources: RescueMapPointSources,
): Array<{ latitude: number; longitude: number }> {
  const points: Array<{ latitude: number; longitude: number }> = []
  appendPoint(points, sources.userLocation)
  appendPoint(points, sources.sos)
  for (const participant of sources.participants) {
    appendPoint(points, participant.location)
  }
  return points
}

function appendPoint(
  points: Array<{ latitude: number; longitude: number }>,
  point: GeoPoint | null,
): void {
  if (!point) return
  points.push({ latitude: point.lat, longitude: point.lng })
}
