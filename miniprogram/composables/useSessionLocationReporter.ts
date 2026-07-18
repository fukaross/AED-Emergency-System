import { onUnmounted, ref } from 'vue'
import { getCurrentLocation, type LocationResult } from '@/services/geo-service'
import type { GeoPoint } from '@/types/common'

const LOCATION_TIMEOUT_MS = 3000
const MS_PER_SECOND = 1000

export type SessionLocationHealthStatus =
  | 'idle'
  | 'healthy'
  | 'permission_denied'
  | 'location_unavailable'
  | 'network_error'

export interface SessionLocationHealth {
  readonly status: SessionLocationHealthStatus
  readonly message: string
}

interface SessionLocationReporterOptions {
  readonly report: (point: GeoPoint) => Promise<void>
  readonly onPermissionDenied?: () => void
}

interface ReporterRun {
  readonly generation: number
  reporting: boolean
  timer: ReturnType<typeof setInterval> | null
}

const IDLE_HEALTH: SessionLocationHealth = { status: 'idle', message: '' }
const HEALTHY_HEALTH: SessionLocationHealth = { status: 'healthy', message: '' }

/** 管理单个页面世代的位置采集、上报节奏与可恢复健康状态。 */
export function useSessionLocationReporter(options: SessionLocationReporterOptions) {
  const controller = new SessionLocationReporter(options)
  onUnmounted(() => controller.stop())
  return {
    health: controller.health,
    retry: () => controller.retry(),
    start: (intervalSeconds: number) => controller.start(intervalSeconds),
    stop: () => controller.stop(),
  }
}

class SessionLocationReporter {
  readonly health = ref<SessionLocationHealth>(IDLE_HEALTH)
  private currentRun: ReporterRun | null = null
  private generation = 0
  private permissionNoticeShown = false

  constructor(private readonly options: SessionLocationReporterOptions) {}

  start(intervalSeconds: number): void {
    const intervalMs = toIntervalMs(intervalSeconds)
    this.stop()
    const run: ReporterRun = {
      generation: this.generation,
      reporting: false,
      timer: null,
    }
    this.currentRun = run
    void this.tick(run)
    run.timer = setInterval(() => void this.tick(run), intervalMs)
  }

  stop(): void {
    this.generation += 1
    const run = this.currentRun
    this.currentRun = null
    if (run?.timer) clearInterval(run.timer)
    this.health.value = IDLE_HEALTH
  }

  retry(): void {
    const run = this.currentRun
    if (run) void this.tick(run)
  }

  private async tick(run: ReporterRun): Promise<void> {
    if (!this.isActive(run) || run.reporting) return
    run.reporting = true
    try {
      const location = await getCurrentLocation({ timeoutMs: LOCATION_TIMEOUT_MS })
      if (!this.isActive(run)) return
      if (!location.success || location.isDefault) {
        this.applyLocationFailure(location)
        return
      }
      await this.options.report(location.point)
      if (this.isActive(run)) this.health.value = HEALTHY_HEALTH
    } catch {
      if (this.isActive(run)) {
        this.health.value = { status: 'network_error', message: '位置共享暂时中断' }
      }
    } finally {
      run.reporting = false
    }
  }

  private applyLocationFailure(location: LocationResult): void {
    if (location.failureReason === 'permission_denied') {
      this.health.value = { status: 'permission_denied', message: '定位权限未开启' }
      if (this.permissionNoticeShown) return
      this.permissionNoticeShown = true
      this.options.onPermissionDenied?.()
      return
    }
    this.health.value = { status: 'location_unavailable', message: '暂时无法获取位置' }
  }

  private isActive(run: ReporterRun): boolean {
    return this.currentRun === run && run.generation === this.generation
  }
}

function toIntervalMs(intervalSeconds: number): number {
  if (!Number.isFinite(intervalSeconds) || intervalSeconds <= 0) {
    throw new Error('locationReportIntervalSeconds 必须为正数')
  }
  return intervalSeconds * MS_PER_SECOND
}
