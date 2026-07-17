import type { GeoPoint } from '@/types/common'
import { interpolateGeo } from '@/utils/rescue-markers'

export interface SessionMarkerAnimationOptions {
  readonly durationMs: number
  readonly steps: number
  readonly onPosition: (openId: string, point: GeoPoint) => void
}

export interface SessionMarkerAnimation {
  move(openId: string, from: GeoPoint, to: GeoPoint): void
  remove(openId: string): void
  dispose(): void
}

class SessionMarkerAnimationController implements SessionMarkerAnimation {
  private readonly timers = new Map<string, ReturnType<typeof setInterval>>()

  constructor(private readonly options: SessionMarkerAnimationOptions) {}

  move(openId: string, from: GeoPoint, to: GeoPoint): void {
    this.remove(openId)
    let step = 0
    const timer = setInterval(() => {
      if (this.timers.get(openId) !== timer) return
      step += 1
      this.options.onPosition(
        openId,
        interpolateGeo(from, to, step / this.options.steps),
      )
      if (step >= this.options.steps) this.remove(openId)
    }, this.options.durationMs / this.options.steps)
    this.timers.set(openId, timer)
  }

  remove(openId: string): void {
    const timer = this.timers.get(openId)
    if (!timer) return
    clearInterval(timer)
    this.timers.delete(openId)
  }

  dispose(): void {
    for (const timer of this.timers.values()) clearInterval(timer)
    this.timers.clear()
  }
}

export function createSessionMarkerAnimation(
  options: SessionMarkerAnimationOptions,
): SessionMarkerAnimation {
  return new SessionMarkerAnimationController(options)
}
