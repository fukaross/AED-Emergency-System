interface SessionResumeDependencies {
  readonly refreshSignals: (localNow: number) => void
  readonly refreshSession: (helpId: string) => Promise<boolean>
  readonly canResume: (helpId: string) => boolean
  readonly startRuntime: () => void
  readonly handleTerminalError: (error: unknown, helpId: string) => boolean
  readonly handleRecoverableError: (error: unknown) => void
}

/** 保证回前台先执行本地隐私 TTL，再以当前世代的 HTTP 结果恢复实时任务。 */
export class SessionResumeController {
  private generation = 0

  constructor(private readonly dependencies: SessionResumeDependencies) {}

  async resume(helpId: string): Promise<void> {
    if (!this.dependencies.canResume(helpId)) return
    const generation = this.generation
    this.dependencies.refreshSignals(Date.now())
    try {
      const refreshed = await this.dependencies.refreshSession(helpId)
      if (!refreshed || !this.owns(generation, helpId)) return
      this.dependencies.startRuntime()
    } catch (error) {
      if (!this.owns(generation, helpId)) return
      if (this.dependencies.handleTerminalError(error, helpId)) return
      this.dependencies.handleRecoverableError(error)
      this.dependencies.startRuntime()
    }
  }

  invalidate(): void {
    this.generation += 1
  }

  private owns(generation: number, helpId: string): boolean {
    return generation === this.generation && this.dependencies.canResume(helpId)
  }
}
