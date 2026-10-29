import { describe, expect, it } from 'vitest'
import { createRequestSequencer } from './request-sequencer'

describe('request sequencer', () => {
  it('accepts only the latest request ticket', () => {
    const sequencer = createRequestSequencer()
    const first = sequencer.next()
    const second = sequencer.next()

    expect(sequencer.isCurrent(first)).toBe(false)
    expect(sequencer.isCurrent(second)).toBe(true)
  })

  it('invalidates earlier polling tickets when a manual request starts', () => {
    const sequencer = createRequestSequencer()
    const pollTicket = sequencer.next()
    const manualTicket = sequencer.next()

    expect(sequencer.isCurrent(pollTicket)).toBe(false)
    expect(sequencer.isCurrent(manualTicket)).toBe(true)
  })

  it('explicitly invalidates the current ticket when a view is paused', () => {
    const sequencer = createRequestSequencer()
    const ticket = sequencer.next()

    sequencer.invalidate()

    expect(sequencer.isCurrent(ticket)).toBe(false)
  })
})
