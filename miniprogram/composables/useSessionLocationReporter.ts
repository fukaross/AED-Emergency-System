// 会话页前台位置上报：每 5s 取一次定位并上报（BackEnd.md §4.5A 位置采集约定）
import { onUnmounted } from 'vue'
import { getCurrentLocation } from '@/services/geo-service'
import type { GeoPoint } from '@/types/common'
import { RESCUE_LOCATION_REPORT_INTERVAL_MS } from '@/utils/rescue-signal'

const LOCATION_TIMEOUT_MS = 3000

export function useSessionLocationReporter(report: (point: GeoPoint) => Promise<void>) {
  let currentRun: ReporterRun | null = null
  let generation = 0

  async function tick(run: ReporterRun): Promise<void> {
    if (!isActive(run) || run.reporting) return
    run.reporting = true
    try {
      const location = await getCurrentLocation({ timeoutMs: LOCATION_TIMEOUT_MS })
      if (!isActive(run) || !location.success) return
      await report(location.point)
    } catch (error) {
      if (isActive(run)) console.warn('[SessionLocation] 位置上报失败', error)
    } finally {
      run.reporting = false
    }
  }

  function start(): void {
    stop()
    const run: ReporterRun = {
      generation: generation,
      reporting: false,
      timer: null,
    }
    currentRun = run
    void tick(run)
    run.timer = setInterval(() => void tick(run), RESCUE_LOCATION_REPORT_INTERVAL_MS)
  }

  function stop(): void {
    generation += 1
    const run = currentRun
    currentRun = null
    if (run?.timer) clearInterval(run.timer)
  }

  function isActive(run: ReporterRun): boolean {
    return currentRun === run && run.generation === generation
  }

  onUnmounted(stop)

  return { start, stop }
}

interface ReporterRun {
  readonly generation: number
  reporting: boolean
  timer: ReturnType<typeof setInterval> | null
}
