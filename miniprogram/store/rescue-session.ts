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

const CLOSED_STATUSES = ['closed', 'false_alarm']

export const useRescueSessionStore = defineStore('rescueSession', () => {
  // ===== State =====
  const mineItems = ref<RescueMineItem[]>([])
  const mineLoaded = ref(false)
  const snapshot = ref<RescueSessionSnapshot | null>(null)
  const sessionClosed = ref(false)

  // ===== Getters =====
  const totalUnread = computed(() =>
    mineItems.value.reduce((sum, item) => sum + item.unreadCount, 0))
  const hasActiveSession = computed(() => mineItems.value.length > 0)

  // ===== Actions =====
  async function loadMineSessions(silent = false): Promise<void> {
    mineItems.value = await fetchMineRescueSessions({ silent })
    mineLoaded.value = true
  }

  async function joinSession(helpId: string): Promise<void> {
    snapshot.value = await joinRescueSession(helpId)
    sessionClosed.value = isClosed(snapshot.value.helpStatus)
  }

  async function refreshSession(helpId: string): Promise<void> {
    if (!snapshot.value || snapshot.value.helpId !== helpId) {
      await joinSession(helpId)
      return
    }

    const incremental = await fetchRescueSession(helpId, {
      afterSeq: snapshot.value.latestSeq,
      silent: true,
    })
    snapshot.value = mergeSnapshot(snapshot.value, incremental)
    sessionClosed.value = isClosed(incremental.helpStatus)
  }

  async function sendMessage(text: string): Promise<void> {
    if (!snapshot.value) return
    const message = await postRescueMessage(snapshot.value.helpId, text)
    appendMessage(message)
  }

  async function markStatus(eventType: RescueEventType): Promise<void> {
    await sendStatusEvent(eventType, 'mark')
  }

  async function revokeStatus(eventType: RescueEventType): Promise<void> {
    await sendStatusEvent(eventType, 'revoke')
  }

  async function reportLocation(point: GeoPoint): Promise<void> {
    if (!snapshot.value) return
    await reportRescueLocation(snapshot.value.helpId, point)
  }

  function applySocketEnvelope(envelope: RescueSocketEnvelope): void {
    if (!snapshot.value) return
    if (envelope.type === 'message') {
      appendMessage(envelope.data as RescueMessage)
      return
    }
    if (envelope.type === 'participant') {
      replaceParticipant(envelope.data as RescueParticipant)
      return
    }
    if (envelope.type === 'help_status') {
      const status = (envelope.data as RescueHelpStatusPayload).helpStatus
      snapshot.value = { ...snapshot.value, helpStatus: status }
      sessionClosed.value = isClosed(status)
    }
  }

  function resetSession(): void {
    snapshot.value = null
    sessionClosed.value = false
  }

  async function sendStatusEvent(
    eventType: RescueEventType,
    action: RescueEventAction,
  ): Promise<void> {
    if (!snapshot.value) return
    const result = await postRescueStatusEvent(snapshot.value.helpId, eventType, action)
    snapshot.value = {
      ...snapshot.value,
      me: { ...snapshot.value.me, takenAed: result.me.takenAed, arrived: result.me.arrived },
    }
    if (result.appended && result.event) {
      appendMessage(result.event)
    }
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

  return {
    mineItems,
    mineLoaded,
    snapshot,
    sessionClosed,
    totalUnread,
    hasActiveSession,
    loadMineSessions,
    joinSession,
    refreshSession,
    sendMessage,
    markStatus,
    revokeStatus,
    reportLocation,
    applySocketEnvelope,
    resetSession,
  }
})

function isClosed(status: string): boolean {
  return CLOSED_STATUSES.includes(status)
}

function mergeSnapshot(
  current: RescueSessionSnapshot,
  incremental: RescueSessionSnapshot,
): RescueSessionSnapshot {
  const knownSeqs = new Set(current.messages.map((message) => message.seq))
  const newMessages = incremental.messages.filter((message) => !knownSeqs.has(message.seq))
  return {
    ...incremental,
    messages: [...current.messages, ...newMessages].sort((a, b) => a.seq - b.seq),
  }
}
