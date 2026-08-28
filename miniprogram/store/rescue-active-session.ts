import { ref } from 'vue'
import type { GeoPoint } from '@/types/common'
import type {
  RescueEventAction,
  RescueMessage,
  RescueMutableEventType,
  RescueSessionSnapshot,
  RescueSocketEnvelope,
  RescueStatusEventResult,
} from '@/types/rescue-session'
import { computeSignal } from '@/utils/rescue-signal'
import { computeServerClockOffset, mergeSessionSnapshot } from '@/utils/rescue-session-state'

interface SessionFetchOptions {
  readonly afterSeq?: number
  readonly silent?: boolean
}

export interface ActiveRescueSessionAdapter {
  join(helpId: string): Promise<RescueSessionSnapshot>
  fetch(helpId: string, options: SessionFetchOptions): Promise<RescueSessionSnapshot>
  postMessage(helpId: string, text: string): Promise<RescueMessage>
  postImage(helpId: string, imageUrl: string): Promise<RescueMessage>
  postStatus(
    helpId: string,
    eventType: RescueMutableEventType,
    action: RescueEventAction,
  ): Promise<RescueStatusEventResult>
  reportLocation(helpId: string, point: GeoPoint): Promise<unknown>
}

interface SessionContext {
  readonly helpId: string
  readonly generation: number
}

const CLOSED_STATUSES = new Set(['closed', 'false_alarm'])

/**
 * Owns one active rescue aggregate. HTTP responses are authoritative; socket frames are hints.
 */
export class ActiveRescueSession {
  readonly snapshot = ref<RescueSessionSnapshot | null>(null)
  readonly closed = ref(false)
  readonly terminalHinted = ref(false)

  private activeHelpId = ''
  private generation = 0
  private latestJoinRequest = 0
  private latestRefreshRequest = 0
  private confirmedSeq = 0
  private serverClockOffsetMs = 0

  constructor(private readonly adapter: ActiveRescueSessionAdapter) {}

  async join(helpId: string): Promise<boolean> {
    if (this.activeHelpId === helpId && this.closed.value) return false
    const generation = this.activate(helpId)
    const request = ++this.latestJoinRequest
    const joined = await this.adapter.join(helpId)
    if (!this.owns(helpId, generation) || request !== this.latestJoinRequest) return false
    this.applyHttpSnapshot(joined, joined.latestSeq)
    return true
  }

  /** Fetches from the last complete HTTP prefix, never from optimistic or socket data. */
  async refresh(helpId: string): Promise<boolean> {
    if (this.activeHelpId === helpId && this.closed.value) return false
    if (!this.snapshot.value || this.snapshot.value.helpId !== helpId) return this.join(helpId)
    const generation = this.generation
    const request = ++this.latestRefreshRequest
    const incremental = await this.adapter.fetch(helpId, {
      afterSeq: this.confirmedSeq,
      silent: true,
    })
    if (!this.owns(helpId, generation) || request !== this.latestRefreshRequest) return false
    const next = mergeSessionSnapshot(this.snapshot.value, incremental)
    this.applyHttpSnapshot(next, Math.max(this.confirmedSeq, incremental.latestSeq))
    return true
  }

  async sendMessage(text: string): Promise<boolean> {
    const context = this.currentContext()
    if (!context || this.closed.value) return false
    const message = await this.adapter.postMessage(context.helpId, text)
    if (!this.ownsContext(context) || this.closed.value) return false
    this.appendDisplayMessage(message)
    return true
  }

  async sendImage(imageUrl: string): Promise<boolean> {
    const context = this.currentContext()
    if (!context || this.closed.value) return false
    const message = await this.adapter.postImage(context.helpId, imageUrl)
    if (!this.ownsContext(context) || this.closed.value) return false
    this.appendDisplayMessage(message)
    return true
  }

  markStatus(eventType: RescueMutableEventType): Promise<boolean> {
    return this.sendStatusEvent(eventType, 'mark')
  }

  revokeStatus(eventType: RescueMutableEventType): Promise<boolean> {
    return this.sendStatusEvent(eventType, 'revoke')
  }

  async reportLocation(point: GeoPoint): Promise<boolean> {
    const context = this.currentContext()
    if (!context || this.closed.value || this.terminalHinted.value) return false
    await this.adapter.reportLocation(context.helpId, point)
    return this.ownsContext(context) && !this.closed.value && !this.terminalHinted.value
  }

  /** Commits the server's final terminal frame; other socket envelopes remain change hints only. */
  applySocketHint(helpId: string, envelope: RescueSocketEnvelope): boolean {
    if (!this.isActiveHelp(helpId)) return false
    if (this.closed.value && envelope.type !== 'help_status') return false
    if (envelope.type !== 'help_status') return true
    const status = envelope.data.helpStatus
    if (!isClosed(status)) return true
    this.applyTerminalStatus(status)
    return true
  }

  /** Commits a terminal status learned from an authoritative HTTP response or error. */
  confirmClosed(helpId: string, status: 'closed' | 'false_alarm' = 'closed'): boolean {
    if (!this.isActiveHelp(helpId)) return false
    this.applyTerminalStatus(status)
    return true
  }

  reset(expectedHelpId?: string): void {
    if (expectedHelpId && this.activeHelpId !== expectedHelpId) return
    this.activeHelpId = ''
    this.invalidateRequests()
    this.confirmedSeq = 0
    this.serverClockOffsetMs = 0
    this.snapshot.value = null
    this.closed.value = false
    this.terminalHinted.value = false
  }

  /** Advances signal state with server-calibrated time and the snapshot's required timing. */
  refreshSignals(now = Date.now()): void {
    const current = this.snapshot.value
    if (!current || this.closed.value) return
    const calibratedNow = now + this.serverClockOffsetMs
    const { signalLostAfterSeconds, locationGoneAfterSeconds } = current.timing
    let changed = false
    const participants = current.participants.map((participant) => {
      if (!participant.location) return participant
      const signal = computeSignal(
        participant.location.reportedAt,
        calibratedNow,
        signalLostAfterSeconds,
        locationGoneAfterSeconds,
      )
      if (signal === participant.signal && signal !== 'gone') return participant
      changed = true
      return signal === 'gone'
        ? { ...participant, signal, location: null }
        : { ...participant, signal }
    })
    if (!changed) return
    this.snapshot.value = { ...current, participants }
  }

  clearLocations(): void {
    const current = this.snapshot.value
    if (!current) return
    const participants = current.participants.map((participant) => ({
      ...participant,
      signal: 'gone' as const,
      location: null,
    }))
    this.snapshot.value = { ...current, participants }
  }

  private async sendStatusEvent(
    eventType: RescueMutableEventType,
    action: RescueEventAction,
  ): Promise<boolean> {
    const context = this.currentContext()
    if (!context || this.closed.value) return false
    const result = await this.adapter.postStatus(context.helpId, eventType, action)
    const current = this.snapshot.value
    if (!current || !this.ownsContext(context) || this.closed.value) return false
    this.snapshot.value = {
      ...current,
      me: { ...current.me, takenAed: result.me.takenAed, arrived: result.me.arrived },
    }
    if (result.appended && result.event) this.appendDisplayMessage(result.event)
    return true
  }

  private appendDisplayMessage(message: RescueMessage): void {
    const current = this.snapshot.value
    if (!current || current.messages.some((item) => item.seq === message.seq)) return
    this.snapshot.value = {
      ...current,
      messages: [...current.messages, message].sort((a, b) => a.seq - b.seq),
      latestSeq: Math.max(current.latestSeq, message.seq),
    }
  }

  private applyHttpSnapshot(next: RescueSessionSnapshot, confirmedSeq: number): void {
    this.serverClockOffsetMs = computeServerClockOffset(next.serverTime, Date.now())
    this.confirmedSeq = confirmedSeq
    this.snapshot.value = next
    this.closed.value = isClosed(next.helpStatus)
    if (!this.closed.value && !this.terminalHinted.value) return
    this.clearLocations()
    if (!this.closed.value) return
    this.terminalHinted.value = true
    this.latestJoinRequest += 1
    this.latestRefreshRequest += 1
  }

  private applyTerminalStatus(status: string): void {
    const current = this.snapshot.value
    if (!current || (this.closed.value && !isClosed(status))) return
    this.snapshot.value = { ...current, helpStatus: status }
    this.closed.value = true
    this.terminalHinted.value = true
    this.latestJoinRequest += 1
    this.latestRefreshRequest += 1
    this.clearLocations()
  }

  private activate(helpId: string): number {
    if (this.activeHelpId === helpId) return this.generation
    this.activeHelpId = helpId
    this.invalidateRequests()
    this.confirmedSeq = 0
    this.serverClockOffsetMs = 0
    this.snapshot.value = null
    this.closed.value = false
    this.terminalHinted.value = false
    return this.generation
  }

  private invalidateRequests(): void {
    this.generation += 1
    this.latestJoinRequest += 1
    this.latestRefreshRequest += 1
  }

  private currentContext(): SessionContext | null {
    if (!this.snapshot.value || this.snapshot.value.helpId !== this.activeHelpId) return null
    return { helpId: this.activeHelpId, generation: this.generation }
  }

  private ownsContext(context: SessionContext): boolean {
    return this.owns(context.helpId, context.generation)
  }

  private owns(helpId: string, generation: number): boolean {
    return this.activeHelpId === helpId && this.generation === generation
  }

  private isActiveHelp(helpId: string): boolean {
    return this.snapshot.value?.helpId === helpId && this.activeHelpId === helpId
  }
}

function isClosed(status: string): boolean {
  return CLOSED_STATUSES.has(status)
}
