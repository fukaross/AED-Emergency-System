import { describe, expect, it } from 'vitest'
import { canShowResultReport } from '@/pages-sub/rescue/session-result-report'

describe('canShowResultReport', () => {
  it('shows for active published/responding help while session is open', () => {
    expect(canShowResultReport('published', false)).toBe(true)
    expect(canShowResultReport('responding', false)).toBe(true)
  })

  it('hides after report submitted or help closed', () => {
    expect(canShowResultReport('pending_review', false)).toBe(false)
    expect(canShowResultReport('closed', false)).toBe(false)
    expect(canShowResultReport('false_alarm', false)).toBe(false)
  })

  it('hides when session is closed or status is unknown', () => {
    expect(canShowResultReport('published', true)).toBe(false)
    expect(canShowResultReport(undefined, false)).toBe(false)
  })
})
