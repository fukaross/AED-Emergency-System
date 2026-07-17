import { describe, expect, it } from 'vitest'
import {
  canShowReportAction,
  canApplyRescueView,
  collectRescueMapPoints,
  isActiveHelpStatus,
  shouldClearRescueViewForError,
} from '@/pages-sub/help/rescue-view-state'

describe('help rescue view state', () => {
  it('shows report action only when the backend grants the capability', () => {
    expect(canShowReportAction({ canSubmitReport: true })).toBe(true)
    expect(canShowReportAction({ canSubmitReport: false })).toBe(false)
    expect(canShowReportAction(null)).toBe(false)
  })

  it('rejects an in-flight response after the request closes', () => {
    expect(canApplyRescueView({
      requestRevision: 1,
      currentRevision: 2,
      requestedHelpId: 'help-1',
      currentHelpId: 'help-1',
      helpStatus: 'closed',
    })).toBe(false)
    expect(isActiveHelpStatus('closed')).toBe(false)
    expect(isActiveHelpStatus('responding')).toBe(true)
  })

  it('clears sensitive locations when rescue-view access closes or is revoked', () => {
    expect(shouldClearRescueViewForError({ statusCode: 403 })).toBe(true)
    expect(shouldClearRescueViewForError({ statusCode: 409 })).toBe(true)
    expect(shouldClearRescueViewForError({ code: 1017 })).toBe(true)
    expect(shouldClearRescueViewForError(new Error('network'))).toBe(false)
  })

  it('includes current user, SOS and every visible participant in map bounds', () => {
    expect(collectRescueMapPoints({
      userLocation: { lat: 1, lng: 2 },
      sos: { lat: 3, lng: 4 },
      participants: [
        { participantId: 'p1', displayName: '一', takenAed: false, arrived: false, signal: 'ok', location: { lat: 5, lng: 6, reportedAt: '' } },
        { participantId: 'p2', displayName: '二', takenAed: false, arrived: false, signal: 'gone', location: null },
      ],
    })).toEqual([
      { latitude: 1, longitude: 2 },
      { latitude: 3, longitude: 4 },
      { latitude: 5, longitude: 6 },
    ])
  })
})
