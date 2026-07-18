import { describe, expect, it, vi } from 'vitest'
import { createMapHomePolling } from '@/pages/map-home/useMapHomePolling'

function buildDependencies() {
  return {
    refreshAed: vi.fn().mockResolvedValue(undefined),
    loadHelp: vi.fn().mockResolvedValue(undefined),
    isLoggedIn: vi.fn(() => true),
    refreshProfile: vi.fn().mockResolvedValue(undefined),
    isCertified: vi.fn(() => true),
    loadSessions: vi.fn().mockResolvedValue(undefined),
    resetSessions: vi.fn(),
  }
}

describe('map home polling', () => {
  it('only clears cached sessions when mine discovery is forbidden', async () => {
    const dependencies = buildDependencies()
    dependencies.loadSessions.mockRejectedValueOnce(
      Object.assign(new Error('forbidden'), { statusCode: 403 }),
    )
    const polling = createMapHomePolling(dependencies)

    await polling.refreshSessions()

    expect(dependencies.resetSessions).toHaveBeenCalledTimes(1)
  })

  it('does not continue discovery when profile refresh is forbidden', async () => {
    const dependencies = buildDependencies()
    dependencies.refreshProfile.mockRejectedValueOnce(
      Object.assign(new Error('forbidden'), { statusCode: 403 }),
    )
    const polling = createMapHomePolling(dependencies)

    await polling.refreshSessions()

    expect(dependencies.loadSessions).not.toHaveBeenCalled()
  })

  it('clears cached sessions after a refreshed profile loses certification', async () => {
    const dependencies = buildDependencies()
    dependencies.isCertified.mockReturnValue(false)
    const polling = createMapHomePolling(dependencies)

    await polling.refreshSessions()

    expect(dependencies.loadSessions).not.toHaveBeenCalled()
    expect(dependencies.resetSessions).toHaveBeenCalledTimes(1)
  })

  it('does not continue an old refresh after polling stops', async () => {
    let finishProfile: (() => void) | undefined
    const dependencies = buildDependencies()
    dependencies.refreshProfile.mockReturnValueOnce(new Promise<void>((resolve) => {
      finishProfile = resolve
    }))
    const polling = createMapHomePolling(dependencies)

    const pending = polling.refreshSessions()
    polling.stop()
    finishProfile?.()
    await pending

    expect(dependencies.loadSessions).not.toHaveBeenCalled()
  })
})
