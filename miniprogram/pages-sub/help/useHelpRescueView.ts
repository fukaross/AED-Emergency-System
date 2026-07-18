import { ref, type Ref } from 'vue'
import { fetchRescueView } from '@/api/rescue-session'
import type { HelpRequestDetail } from '@/types/help'
import type { RescueView } from '@/types/rescue-session'
import { interpolateViewParticipants } from '@/utils/rescue-markers'
import { computeServerClockOffset } from '@/utils/rescue-session-state'
import { RESCUE_VIEW_POLL_INTERVAL_MS } from '@/utils/rescue-signal'
import {
  canApplyRescueView,
  isActiveHelpStatus,
  projectRescueViewParticipants,
  shouldClearRescueViewForError,
} from './rescue-view-state'

const ANIMATION_DURATION_MS = 600
const ANIMATION_FRAME_MS = 60
const SIGNAL_REFRESH_INTERVAL_MS = 1000

export type RescueViewRefreshResult = 'active' | 'skipped' | 'retryable_error' | 'terminal'

class ParticipantMarkerAnimator {
  readonly participants = ref<RescueView['participants']>([])
  private timer: ReturnType<typeof setInterval> | null = null

  apply(target: RescueView['participants']): void {
    this.stop()
    if (this.participants.value.length === 0) {
      this.participants.value = target
      return
    }
    const current = this.participants.value
    const startedAt = Date.now()
    this.timer = setInterval(() => {
      const progress = (Date.now() - startedAt) / ANIMATION_DURATION_MS
      this.participants.value = interpolateViewParticipants(current, target, progress)
      if (progress >= 1) this.stop()
    }, ANIMATION_FRAME_MS)
  }

  replace(target: RescueView['participants']): void {
    this.stop()
    this.participants.value = target
  }

  clear(): void {
    this.replace([])
  }

  stop(): void {
    if (!this.timer) return
    clearInterval(this.timer)
    this.timer = null
  }
}

class RescueViewController {
  readonly rescueView = ref<RescueView | null>(null)
  readonly stale = ref(false)
  readonly animator = new ParticipantMarkerAnimator()
  private pollTimer: ReturnType<typeof setInterval> | null = null
  private signalTimer: ReturnType<typeof setInterval> | null = null
  private sourceParticipants: RescueView['participants'] = []
  private serverClockOffsetMs = 0
  private revision = 0
  private requestSequence = 0
  private activeRequestId: number | null = null
  private terminal = false

  constructor(private readonly detail: Ref<HelpRequestDetail | null>) {}

  async refresh(): Promise<RescueViewRefreshResult> {
    const current = this.detail.value
    if (this.terminal) return 'terminal'
    if (this.activeRequestId !== null || !current) return 'skipped'
    if (!isActiveHelpStatus(current.status)) {
      this.terminate()
      return 'terminal'
    }
    const requestRevision = this.revision
    const requestId = ++this.requestSequence
    this.activeRequestId = requestId
    try {
      const next = await fetchRescueView(current.id, { silent: true })
      if (!this.ownsRequest(requestId)) return 'skipped'
      if (!this.canApply(next, current.id, requestRevision)) {
        return this.terminal ? 'terminal' : 'skipped'
      }
      this.apply(next)
      return 'active'
    } catch (error) {
      if (!this.ownsRequest(requestId)) return 'skipped'
      return this.handleError(error)
    } finally {
      if (this.activeRequestId === requestId) this.activeRequestId = null
    }
  }

  startPolling(): boolean {
    if (this.terminal || !isActiveHelpStatus(this.detail.value?.status)) return false
    this.stopTimers()
    this.advanceSignals()
    this.pollTimer = setInterval(() => void this.refresh(), RESCUE_VIEW_POLL_INTERVAL_MS)
    this.signalTimer = setInterval(() => this.advanceSignals(), SIGNAL_REFRESH_INTERVAL_MS)
    return true
  }

  suspend(): void {
    this.revision += 1
    this.activeRequestId = null
    this.stopTimers()
    this.animator.stop()
  }

  clear(): void {
    this.terminate()
  }

  private apply(next: RescueView): void {
    this.serverClockOffsetMs = computeServerClockOffset(next.serverTime, Date.now())
    this.sourceParticipants = next.participants
    this.rescueView.value = next
    const participants = this.projectParticipants()
    this.sourceParticipants = participants
    this.rescueView.value = { ...next, participants }
    this.animator.apply(participants)
    this.stale.value = false
  }

  private advanceSignals(): void {
    if (!this.rescueView.value) return
    const participants = this.projectParticipants()
    this.sourceParticipants = participants
    this.rescueView.value = { ...this.rescueView.value, participants }
    this.animator.replace(participants)
  }

  private projectParticipants(): RescueView['participants'] {
    const view = this.rescueView.value
    if (!view) return this.sourceParticipants
    return projectRescueViewParticipants({
      participants: this.sourceParticipants,
      timing: view.timing,
      serverClockOffsetMs: this.serverClockOffsetMs,
      localNow: Date.now(),
    })
  }

  private handleError(error: unknown): RescueViewRefreshResult {
    if (shouldClearRescueViewForError(error)) {
      this.terminate()
      return 'terminal'
    }
    this.stale.value = true
    logRescuePollingError(error)
    return 'retryable_error'
  }

  private terminate(): void {
    this.terminal = true
    this.suspend()
    this.sourceParticipants = []
    this.serverClockOffsetMs = 0
    this.rescueView.value = null
    this.stale.value = false
    this.animator.clear()
  }

  private canApply(next: RescueView, helpId: string, requestRevision: number): boolean {
    if (!isActiveHelpStatus(next.helpStatus)) {
      this.terminate()
      return false
    }
    return canApplyRescueView({
      requestRevision,
      currentRevision: this.revision,
      requestedHelpId: helpId,
      currentHelpId: this.detail.value?.id,
      helpStatus: this.detail.value?.status,
    })
  }

  private ownsRequest(requestId: number): boolean {
    return this.activeRequestId === requestId
  }

  private stopTimers(): void {
    if (this.pollTimer) clearInterval(this.pollTimer)
    if (this.signalTimer) clearInterval(this.signalTimer)
    this.pollTimer = null
    this.signalTimer = null
  }
}

function logRescuePollingError(error: unknown): void {
  const apiError = error as { readonly statusCode?: number; readonly code?: number }
  console.warn(
    `[RescueViewPolling] GET /api/help/{id}/rescue-view status=${apiError?.statusCode ?? 'network'} code=${apiError?.code ?? 'none'}`,
  )
}

export function useHelpRescueView(detail: Ref<HelpRequestDetail | null>) {
  const controller = new RescueViewController(detail)
  return {
    rescueView: controller.rescueView,
    rescueViewStale: controller.stale,
    animatedParticipants: controller.animator.participants,
    refreshRescueView: () => controller.refresh(),
    startRescuePolling: () => controller.startPolling(),
    clearRescueView: () => controller.clear(),
    suspendRescueView: () => controller.suspend(),
  }
}
