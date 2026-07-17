import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createSessionMarkerAnimation } from '@/pages-sub/rescue/components/session-marker-animation'

describe('session marker animation', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('cancels an older target before animating the same participant again', () => {
    const positions: Array<{ lat: number; lng: number }> = []
    const animation = createSessionMarkerAnimation({
      durationMs: 900,
      steps: 9,
      onPosition: (_openId, point) => positions.push(point),
    })

    animation.move('rescuer-a', { lat: 0, lng: 0 }, { lat: 9, lng: 9 })
    vi.advanceTimersByTime(300)
    animation.move('rescuer-a', positions.at(-1)!, { lat: 20, lng: 20 })
    vi.advanceTimersByTime(1_000)

    expect(positions.at(-1)).toEqual({ lat: 20, lng: 20 })
    const countAtTarget = positions.length
    vi.advanceTimersByTime(1_000)
    expect(positions).toHaveLength(countAtTarget)
  })

  it('never writes a coordinate after that participant is removed', () => {
    const onPosition = vi.fn()
    const animation = createSessionMarkerAnimation({
      durationMs: 900,
      steps: 9,
      onPosition,
    })

    animation.move('rescuer-a', { lat: 0, lng: 0 }, { lat: 9, lng: 9 })
    vi.advanceTimersByTime(200)
    animation.remove('rescuer-a')
    const writesBeforeRemoval = onPosition.mock.calls.length
    vi.advanceTimersByTime(1_000)

    expect(onPosition).toHaveBeenCalledTimes(writesBeforeRemoval)
  })

  it('disposes every participant timer on unmount', () => {
    const onPosition = vi.fn()
    const animation = createSessionMarkerAnimation({
      durationMs: 900,
      steps: 9,
      onPosition,
    })
    animation.move('rescuer-a', { lat: 0, lng: 0 }, { lat: 9, lng: 9 })
    animation.move('rescuer-b', { lat: 1, lng: 1 }, { lat: 10, lng: 10 })

    animation.dispose()
    vi.advanceTimersByTime(1_000)

    expect(onPosition).not.toHaveBeenCalled()
  })
})
