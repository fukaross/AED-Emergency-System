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
    logout: vi.fn(),
    resetSessions: vi.fn(),
  }
}

describe('map home polling', () => {
  it('logs out and clears cached sessions when discovery is forbidden', async () => {
    const dependencies = buildDependencies()
    dependencies.loadSessions.mockRejectedValueOnce(
      Object.assign(new Error('forbidden'), { statusCode: 403 }),
    )
    const polling = createMapHomePolling(dependencies)

    await polling.refreshSessions()

    expect(dependencies.logout).toHaveBeenCalledTimes(1)
    expect(dependencies.resetSessions).toHaveBeenCalledTimes(1)
  })

  it('clears cached sessions after a refreshed profile loses certification', async () => {
    const dependencies = buildDependencies()
    dependencies.isCertified.mockReturnValue(false)
    const polling = createMapHomePolling(dependencies)

    await polling.refreshSessions()

    expect(dependencies.loadSessions).not.toHaveBeenCalled()
    expect(dependencies.resetSessions).toHaveBeenCalledTimes(1)
  })
})
