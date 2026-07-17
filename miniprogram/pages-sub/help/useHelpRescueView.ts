import { ref, type Ref } from 'vue'
import { fetchRescueView } from '@/api/rescue-session'
import type { HelpRequestDetail } from '@/types/help'
import type { RescueView } from '@/types/rescue-session'
import { interpolateViewParticipants } from '@/utils/rescue-markers'
import { RESCUE_VIEW_POLL_INTERVAL_MS } from '@/utils/rescue-signal'
import {
  canApplyRescueView,
  isActiveHelpStatus,
  shouldClearRescueViewForError,
} from './rescue-view-state'

const ANIMATION_DURATION_MS = 600
const ANIMATION_FRAME_MS = 60

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

  clear(): void {
    this.stop()
    this.participants.value = []
  }

  stop(): void {
    if (!this.timer) return
    clearInterval(this.timer)
    this.timer = null
  }
}

class RescueViewController {
  readonly rescueView = ref<RescueView | null>(null)
  readonly animator = new ParticipantMarkerAnimator()
  private pollTimer: ReturnType<typeof setInterval> | null = null
  private revision = 0
  private requestSequence = 0
  private activeRequestId: number | null = null

  constructor(private readonly detail: Ref<HelpRequestDetail | null>) {}

  async refresh(): Promise<void> {
    const current = this.detail.value
    if (this.activeRequestId !== null || !current) return
    if (!isActiveHelpStatus(current.status)) {
      this.clear()
      return
    }
    const requestRevision = this.revision
    const requestId = ++this.requestSequence
    this.activeRequestId = requestId
    try {
      const next = await fetchRescueView(current.id, { silent: true })
      if (!this.ownsRequest(requestId)) return
      if (!this.canApply(next, current.id, requestRevision)) return
      this.rescueView.value = next
      this.animator.apply(next.participants)
    } catch (error) {
      if (!this.ownsRequest(requestId)) return
      if (shouldClearRescueViewForError(error)) this.clear()
      else logRescuePollingError(error)
    } finally {
      if (this.activeRequestId === requestId) this.activeRequestId = null
    }
  }

  startPolling(): void {
    this.stopPollingTimer()
    this.pollTimer = setInterval(
      () => this.refresh(),
      RESCUE_VIEW_POLL_INTERVAL_MS,
    )
  }

  suspend(): void {
    this.revision += 1
    this.activeRequestId = null
    this.stopPollingTimer()
    this.animator.stop()
  }

  clear(): void {
    this.suspend()
    this.rescueView.value = null
    this.animator.clear()
  }

  private canApply(
    next: RescueView,
    helpId: string,
    requestRevision: number,
  ): boolean {
    if (!isActiveHelpStatus(next.helpStatus)) {
      this.clear()
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

  private stopPollingTimer(): void {
    if (!this.pollTimer) return
    clearInterval(this.pollTimer)
    this.pollTimer = null
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
    animatedParticipants: controller.animator.participants,
    refreshRescueView: () => controller.refresh(),
    startRescuePolling: () => controller.startPolling(),
    clearRescueView: () => controller.clear(),
    suspendRescueView: () => controller.suspend(),
  }
}
