import { defineStore } from 'pinia'
import {
  fetchMineRescueSessions,
  fetchRescueSession,
  joinRescueSession,
  postRescueMessage,
  postRescueStatusEvent,
  reportRescueLocation,
} from '@/api/rescue-session'
import { ActiveRescueSession } from '@/store/rescue-active-session'
import { RescueSessionDiscovery } from '@/store/rescue-session-discovery'

export { classifyRescueSessionTerminalError } from '@/utils/rescue-session-state'

const discoveryAdapter = { fetchMine: fetchMineRescueSessions }
const activeAdapter = {
  join: joinRescueSession,
  fetch: fetchRescueSession,
  postMessage: postRescueMessage,
  postStatus: postRescueStatusEvent,
  reportLocation: reportRescueLocation,
}

export const useRescueSessionStore = defineStore('rescueSession', () => {
  const discovery = new RescueSessionDiscovery(discoveryAdapter)
  const active = new ActiveRescueSession(activeAdapter)

  return {
    mineItems: discovery.items,
    mineLoaded: discovery.loaded,
    totalUnread: discovery.totalUnread,
    hasActiveSession: discovery.hasActiveSession,
    snapshot: active.snapshot,
    sessionClosed: active.closed,
    terminalHinted: active.terminalHinted,
    loadMineSessions: (ownerId: string, silent = false) => discovery.load(ownerId, silent),
    resetMineSessions: () => discovery.reset(),
    joinSession: (helpId: string) => active.join(helpId),
    refreshSession: (helpId: string) => active.refresh(helpId),
    sendMessage: (text: string) => active.sendMessage(text),
    markStatus: active.markStatus.bind(active),
    revokeStatus: active.revokeStatus.bind(active),
    reportLocation: active.reportLocation.bind(active),
    applySocketEnvelope: active.applySocketHint.bind(active),
    confirmSessionClosed: active.confirmClosed.bind(active),
    refreshParticipantSignals: (now?: number) => active.refreshSignals(now),
    clearParticipantLocations: () => active.clearLocations(),
    resetSession: (expectedHelpId?: string) => active.reset(expectedHelpId),
  }
})
