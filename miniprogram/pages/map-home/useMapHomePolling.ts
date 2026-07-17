import { RESCUE_MINE_POLL_INTERVAL_MS } from '@/utils/rescue-signal'

const HELP_POLL_INTERVAL_MS = 30_000
const AED_POLL_INTERVAL_MS = 60_000

export interface MapHomePollingDependencies {
  readonly refreshAed: () => Promise<void>
  readonly loadHelp: (silent: boolean) => Promise<void>
  readonly isLoggedIn: () => boolean
  readonly refreshProfile: () => Promise<void>
  readonly isCertified: () => boolean
  readonly loadSessions: () => Promise<void>
  readonly logout: () => void
  readonly resetSessions: () => void
}

class MapHomePollingController {
  private helpTimer: ReturnType<typeof setInterval> | null = null
  private aedTimer: ReturnType<typeof setInterval> | null = null
  private sessionTimer: ReturnType<typeof setInterval> | null = null
  private isHelpPolling = false
  private isAedPolling = false
  private isSessionPolling = false

  constructor(private readonly dependencies: MapHomePollingDependencies) {}

  start(): void {
    this.stop()
    void this.refreshAed()
    void this.refreshHelp(false)
    void this.refreshSessions()
    this.aedTimer = setInterval(() => this.refreshAed(), AED_POLL_INTERVAL_MS)
    this.helpTimer = setInterval(() => this.refreshHelp(true), HELP_POLL_INTERVAL_MS)
    this.sessionTimer = setInterval(
      () => this.refreshSessions(),
      RESCUE_MINE_POLL_INTERVAL_MS,
    )
  }

  stop(): void {
    this.clearTimers()
    this.isHelpPolling = false
    this.isAedPolling = false
    this.isSessionPolling = false
  }

  async refreshSessions(): Promise<void> {
    if (this.isSessionPolling || !this.dependencies.isLoggedIn()) return
    this.isSessionPolling = true
    try {
      await this.dependencies.refreshProfile()
      if (this.dependencies.isCertified()) {
        await this.dependencies.loadSessions()
      } else {
        this.dependencies.resetSessions()
      }
    } catch (error) {
      if (isForbidden(error)) {
        this.dependencies.logout()
        this.dependencies.resetSessions()
      } else {
        logPollingError('session-discovery', error)
      }
    } finally {
      this.isSessionPolling = false
    }
  }

  private async refreshAed(): Promise<void> {
    if (this.isAedPolling) return
    this.isAedPolling = true
    try {
      await this.dependencies.refreshAed()
    } catch (error) {
      logPollingError('aed-list', error)
    } finally {
      this.isAedPolling = false
    }
  }

  private async refreshHelp(silent: boolean): Promise<void> {
    if (this.isHelpPolling) return
    this.isHelpPolling = true
    try {
      await this.dependencies.loadHelp(silent)
    } catch (error) {
      logPollingError('help-list', error)
    } finally {
      this.isHelpPolling = false
    }
  }

  private clearTimers(): void {
    if (this.helpTimer) clearInterval(this.helpTimer)
    if (this.aedTimer) clearInterval(this.aedTimer)
    if (this.sessionTimer) clearInterval(this.sessionTimer)
    this.helpTimer = null
    this.aedTimer = null
    this.sessionTimer = null
  }
}

export function createMapHomePolling(
  dependencies: MapHomePollingDependencies,
) {
  const controller = new MapHomePollingController(dependencies)
  return {
    start: () => controller.start(),
    stop: () => controller.stop(),
    refreshSessions: () => controller.refreshSessions(),
  }
}

function isForbidden(error: unknown): boolean {
  return typeof error === 'object'
    && error !== null
    && (error as { readonly statusCode?: number }).statusCode === 403
}

function logPollingError(scope: string, error: unknown): void {
  const apiError = error as { readonly statusCode?: number; readonly code?: number }
  console.warn(
    `[MapHome] ${scope} status=${apiError?.statusCode ?? 'network'} code=${apiError?.code ?? 'none'}`,
  )
}
