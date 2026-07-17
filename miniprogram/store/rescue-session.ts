// 急救会话状态 —— 会话快照、消息增量合并、我的进行中会话（定时器由页面生命周期管理）
import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { GeoPoint } from '@/types/common'
import type {
  RescueEventAction,
  RescueEventType,
  RescueHelpStatusPayload,
  RescueMessage,
  RescueMineItem,
  RescueParticipant,
  RescueSessionSnapshot,
  RescueSocketEnvelope,
} from '@/types/rescue-session'
import {
  fetchMineRescueSessions,
  fetchRescueSession,
  joinRescueSession,
  postRescueMessage,
  postRescueStatusEvent,
  reportRescueLocation,
} from '@/api/rescue-session'
import { computeSignal } from '@/utils/rescue-signal'
import {
  computeServerClockOffset,
  mergeSessionSnapshot,
} from '@/utils/rescue-session-state'

export { classifyRescueSessionTerminalError } from '@/utils/rescue-session-state'

const CLOSED_STATUSES = ['closed', 'false_alarm']

export const useRescueSessionStore = defineStore('rescueSession', () => {
  // ===== State =====
  const mineItems = ref<RescueMineItem[]>([])
  const mineLoaded = ref(false)
  const snapshot = ref<RescueSessionSnapshot | null>(null)
  const sessionClosed = ref(false)
  let activeHelpId = ''
  let sessionGeneration = 0
  let latestJoinRequest = 0
  let latestRefreshRequest = 0
  let serverClockOffsetMs = 0
  let mineOwnerId = ''
  let mineGeneration = 0
  let latestMineRequest = 0

  // ===== Getters =====
  const totalUnread = computed(() =>
    mineItems.value.reduce((sum, item) => sum + item.unreadCount, 0))
  const hasActiveSession = computed(() => mineItems.value.length > 0)

  // ===== Actions =====
  async function loadMineSessions(ownerId: string, silent = false): Promise<void> {
    if (!ownerId) {
      resetMineSessions()
      return
    }
    bindMineOwner(ownerId)
    const generation = mineGeneration
    const request = ++latestMineRequest
    const items = await fetchMineRescueSessions({ silent })
    if (mineOwnerId !== ownerId
      || mineGeneration !== generation
      || latestMineRequest !== request) return
    mineItems.value = items
    mineLoaded.value = true
  }

  function resetMineSessions(): void {
    mineOwnerId = ''
    mineGeneration += 1
    latestMineRequest += 1
    clearMineState()
  }

  function bindMineOwner(ownerId: string): void {
    if (mineOwnerId === ownerId) return
    mineOwnerId = ownerId
    mineGeneration += 1
    latestMineRequest += 1
    clearMineState()
  }

  function clearMineState(): void {
    mineItems.value = []
    mineLoaded.value = false
  }

  async function joinSession(helpId: string): Promise<boolean> {
    if (activeHelpId === helpId && sessionClosed.value) return false
    const generation = activateSession(helpId)
    const request = ++latestJoinRequest
    const joined = await joinRescueSession(helpId)
    if (!isCurrentRequest(helpId, generation) || request !== latestJoinRequest) return false
    setSnapshot(joined)
    return true
  }

  async function refreshSession(helpId: string): Promise<boolean> {
    if (activeHelpId === helpId && sessionClosed.value) return false
    if (!snapshot.value || snapshot.value.helpId !== helpId) {
      return joinSession(helpId)
    }

    const generation = sessionGeneration
    const request = ++latestRefreshRequest
    const incremental = await fetchRescueSession(helpId, {
      afterSeq: snapshot.value.latestSeq,
      silent: true,
    })
    if (!isCurrentRequest(helpId, generation) || request !== latestRefreshRequest) return false
    setSnapshot(mergeSessionSnapshot(snapshot.value, incremental))
    return true
  }

  async function sendMessage(text: string): Promise<boolean> {
    const context = currentContext()
    if (!context || sessionClosed.value) return false
    const message = await postRescueMessage(context.helpId, text)
    if (!isCurrentRequest(context.helpId, context.generation) || sessionClosed.value) return false
    appendMessage(message)
    return true
  }

  function markStatus(eventType: RescueEventType): Promise<boolean> {
    return sendStatusEvent(eventType, 'mark')
  }

  function revokeStatus(eventType: RescueEventType): Promise<boolean> {
    return sendStatusEvent(eventType, 'revoke')
  }

  async function reportLocation(point: GeoPoint): Promise<boolean> {
    const context = currentContext()
    if (!context || sessionClosed.value) return false
    await reportRescueLocation(context.helpId, point)
    return isCurrentRequest(context.helpId, context.generation) && !sessionClosed.value
  }

  function applySocketEnvelope(helpId: string, envelope: RescueSocketEnvelope): boolean {
    if (!snapshot.value || snapshot.value.helpId !== helpId || activeHelpId !== helpId) return false
    if (sessionClosed.value && envelope.type !== 'help_status') return false
    if (envelope.type === 'message') {
      appendMessage(envelope.data as RescueMessage)
      return true
    }
    if (envelope.type === 'participant') {
      replaceParticipant(envelope.data as RescueParticipant)
      return true
    }
    if (envelope.type === 'help_status') {
      const status = (envelope.data as RescueHelpStatusPayload).helpStatus
      if (sessionClosed.value && !isClosed(status)) return false
      setSnapshot({ ...snapshot.value, helpStatus: status })
      return true
    }
    return false
  }

  function resetSession(expectedHelpId?: string): void {
    if (expectedHelpId && activeHelpId !== expectedHelpId) return
    activeHelpId = ''
    sessionGeneration += 1
    latestJoinRequest += 1
    latestRefreshRequest += 1
    serverClockOffsetMs = 0
    snapshot.value = null
    sessionClosed.value = false
  }

  async function sendStatusEvent(
    eventType: RescueEventType,
    action: RescueEventAction,
  ): Promise<boolean> {
    const context = currentContext()
    if (!context || sessionClosed.value) return false
    const result = await postRescueStatusEvent(context.helpId, eventType, action)
    if (
      !snapshot.value
      || !isCurrentRequest(context.helpId, context.generation)
      || sessionClosed.value
    ) return false
    snapshot.value = {
      ...snapshot.value,
      me: { ...snapshot.value.me, takenAed: result.me.takenAed, arrived: result.me.arrived },
    }
    if (result.appended && result.event) {
      appendMessage(result.event)
    }
    return true
  }

  function appendMessage(message: RescueMessage): void {
    if (!snapshot.value || snapshot.value.messages.some((item) => item.seq === message.seq)) {
      return
    }
    snapshot.value = {
      ...snapshot.value,
      messages: [...snapshot.value.messages, message].sort((a, b) => a.seq - b.seq),
      latestSeq: Math.max(snapshot.value.latestSeq, message.seq),
    }
  }

  function replaceParticipant(participant: RescueParticipant): void {
    if (!snapshot.value) return
    const exists = snapshot.value.participants.some((item) => item.openId === participant.openId)
    const participants = exists
      ? snapshot.value.participants.map((item) =>
          item.openId === participant.openId ? participant : item)
      : [...snapshot.value.participants, participant]
    snapshot.value = { ...snapshot.value, participants }
  }

  /** 按最后一次位置上报时间推进信号；90 秒后立即丢弃本地精确坐标。 */
  function refreshParticipantSignals(now = Date.now()): void {
    if (!snapshot.value || sessionClosed.value) return
    const calibratedNow = now + serverClockOffsetMs
    const participants = snapshot.value.participants.map((participant) => {
      if (!participant.location) return participant
      const signal = computeSignal(participant.location.reportedAt, calibratedNow)
      return signal === 'gone'
        ? { ...participant, signal, location: null }
        : { ...participant, signal }
    })
    snapshot.value = { ...snapshot.value, participants }
  }

  function clearParticipantLocations(): void {
    if (!snapshot.value) return
    const participants = snapshot.value.participants.map((participant) => ({
      ...participant,
      signal: 'gone' as const,
      location: null,
    }))
    snapshot.value = { ...snapshot.value, participants }
  }

  function activateSession(helpId: string): number {
    if (activeHelpId === helpId) return sessionGeneration
    activeHelpId = helpId
    sessionGeneration += 1
    latestJoinRequest = 0
    latestRefreshRequest = 0
    serverClockOffsetMs = 0
    snapshot.value = null
    sessionClosed.value = false
    return sessionGeneration
  }

  function isCurrentRequest(helpId: string, generation: number): boolean {
    return activeHelpId === helpId && sessionGeneration === generation
  }

  function currentContext(): { helpId: string; generation: number } | null {
    if (!snapshot.value || snapshot.value.helpId !== activeHelpId) return null
    return { helpId: activeHelpId, generation: sessionGeneration }
  }

  function setSnapshot(next: RescueSessionSnapshot): void {
    serverClockOffsetMs = computeServerClockOffset(next.serverTime, Date.now())
    snapshot.value = next
    sessionClosed.value = isClosed(next.helpStatus)
    if (!sessionClosed.value) return
    latestJoinRequest += 1
    latestRefreshRequest += 1
    clearParticipantLocations()
  }

  return {
    mineItems,
    mineLoaded,
    snapshot,
    sessionClosed,
    totalUnread,
    hasActiveSession,
    loadMineSessions,
    resetMineSessions,
    joinSession,
    refreshSession,
    sendMessage,
    markStatus,
    revokeStatus,
    reportLocation,
    applySocketEnvelope,
    refreshParticipantSignals,
    clearParticipantLocations,
    resetSession,
  }
})

function isClosed(status: string): boolean {
  return CLOSED_STATUSES.includes(status)
}
