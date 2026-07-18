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
  readonly resetSessions: () => void
}

class MapHomePollingController {
  private helpTimer: ReturnType<typeof setInterval> | null = null
  private aedTimer: ReturnType<typeof setInterval> | null = null
  private sessionTimer: ReturnType<typeof setInterval> | null = null
  private isHelpPolling = false
  private isAedPolling = false
  private isSessionPolling = false
  private generation = 0

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
    this.generation += 1
    this.clearTimers()
    this.isHelpPolling = false
    this.isAedPolling = false
    this.isSessionPolling = false
  }

  async refreshSessions(): Promise<void> {
    if (this.isSessionPolling || !this.dependencies.isLoggedIn()) return
    const generation = this.generation
    this.isSessionPolling = true
    try {
      if (!await this.refreshProfile(generation)) return
      if (!this.isCurrentRun(generation)) return
      await this.refreshSessionList()
    } finally {
      if (generation === this.generation) this.isSessionPolling = false
    }
  }

  private async refreshProfile(generation: number): Promise<boolean> {
    try {
      await this.dependencies.refreshProfile()
      return this.isCurrentRun(generation)
    } catch (error) {
      logPollingError('profile', error)
      return false
    }
  }

  private async refreshSessionList(): Promise<void> {
    if (!this.dependencies.isCertified()) {
      this.dependencies.resetSessions()
      return
    }
    try {
      await this.dependencies.loadSessions()
    } catch (error) {
      if (isForbidden(error)) this.dependencies.resetSessions()
      else logPollingError('session-discovery', error)
    }
  }

  private async refreshAed(): Promise<void> {
    if (this.isAedPolling) return
    const generation = this.generation
    this.isAedPolling = true
    try {
      await this.dependencies.refreshAed()
    } catch (error) {
      logPollingError('aed-list', error)
    } finally {
      if (generation === this.generation) this.isAedPolling = false
    }
  }

  private async refreshHelp(silent: boolean): Promise<void> {
    if (this.isHelpPolling) return
    const generation = this.generation
    this.isHelpPolling = true
    try {
      await this.dependencies.loadHelp(silent)
    } catch (error) {
      logPollingError('help-list', error)
    } finally {
      if (generation === this.generation) this.isHelpPolling = false
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

  private isCurrentRun(generation: number): boolean {
    return generation === this.generation && this.dependencies.isLoggedIn()
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
