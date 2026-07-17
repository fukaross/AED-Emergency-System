import type { GeoPoint } from '@/types/common'
import type { HelpStatus } from '@/types/help'
import type { RescueViewParticipant } from '@/types/rescue-session'

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
  return apiError.code === 1017
    || apiError.statusCode === 403
    || apiError.statusCode === 409
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
