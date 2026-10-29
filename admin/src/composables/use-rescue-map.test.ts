import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AMapModule, AmapLngLat, AmapMap, AmapMarker } from '@/types/amap'
import type { AdminRescueParticipant, RescueSos } from '@/types/rescue-session'
import { siteConfig } from '@/config/site-config'
import { useRescueMap } from './use-rescue-map'
import type { ParticipantColor } from '@/utils/rescue-session-color'

const noColors = new Map<string, ParticipantColor>()

vi.mock('@/utils/amap-loader', () => ({ loadAmap: vi.fn() }))

class FakeLngLat implements AmapLngLat {
  constructor(private readonly lng: number, private readonly lat: number) {}
  getLng(): number { return this.lng }
  getLat(): number { return this.lat }
}

class FakeMarker implements AmapMarker {
  readonly setPosition = vi.fn()
  readonly setContent = vi.fn()
  readonly on = vi.fn()
  readonly remove = vi.fn()

  constructor(readonly options: { position: [number, number]; content?: string }) {}
}

class FakeMap implements AmapMap {
  options: { center?: [number, number]; zoom?: number } = {}
  readonly on = vi.fn()
  readonly setCenter = vi.fn()
  readonly setFitView = vi.fn()
  readonly destroy = vi.fn()
  readonly add = vi.fn()
  readonly remove = vi.fn()
}

// 合成 SOS / 参与者坐标，不沿用任何旧站点点位。
const sos: RescueSos = { lat: 31.246, lng: 121.503, address: '演示教学楼' }

function participant(overrides: Partial<AdminRescueParticipant> = {}): AdminRescueParticipant {
  return {
    openId: 'responder-1',
    displayName: '张急救',
    avatarUrl: '',
    joinedAt: '2026-07-17T10:00:00Z',
    takenAed: false,
    arrived: false,
    signal: 'ok',
    location: { lat: 31.2465, lng: 121.504, reportedAt: '2026-07-17T10:00:00Z' },
    ...overrides,
  }
}

function createFakeAmap(map: FakeMap, createdMarkers: FakeMarker[]): AMapModule {
  return {
    Map: class {
      constructor(_container: string, options?: { center?: [number, number]; zoom?: number }) {
        map.options = options ?? {}
        return map
      }
    },
    Marker: class extends FakeMarker {
      constructor(options: { position: [number, number]; content?: string }) {
        super(options)
        createdMarkers.push(this)
      }
    },
    LngLat: FakeLngLat,
  } as unknown as AMapModule
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

let map: FakeMap
let markers: FakeMarker[]

beforeEach(() => {
  map = new FakeMap()
  markers = []
})

describe('useRescueMap marker updates', () => {
  it('updates marker content when AED or signal state changes and removes gone participants', async () => {
    const loadMap = vi.fn(async () => createFakeAmap(map, markers))
    const rescueMap = useRescueMap({ loadMap })
    await rescueMap.initMap('map')

    rescueMap.syncMarkers(sos, [participant()], noColors)
    const participantMarker = markers[1]
    expect(participantMarker.options.content).toContain('#2196f3')

    rescueMap.syncMarkers(sos, [participant({ takenAed: true })], noColors)
    expect(participantMarker.setContent).toHaveBeenLastCalledWith(expect.stringContaining('#4caf50'))

    rescueMap.syncMarkers(sos, [participant({ takenAed: true, signal: 'lost' })], noColors)
    expect(participantMarker.setContent).toHaveBeenLastCalledWith(expect.stringContaining('#909399'))

    rescueMap.syncMarkers(sos, [participant({ signal: 'gone' })], noColors)
    expect(map.remove).toHaveBeenCalledWith(participantMarker)
  })
})

describe('useRescueMap viewport fitting', () => {
  it('fits the viewport when the first visible participant appears', async () => {
    const rescueMap = useRescueMap({ loadMap: async () => createFakeAmap(map, markers) })
    await rescueMap.initMap('map')

    rescueMap.syncMarkers(sos, [], noColors)
    expect(map.setFitView).not.toHaveBeenCalled()

    rescueMap.syncMarkers(sos, [participant()], noColors)
    expect(map.setFitView).toHaveBeenCalledTimes(1)

    rescueMap.syncMarkers(sos, [participant({ takenAed: true })], noColors)
    expect(map.setFitView).toHaveBeenCalledTimes(1)
  })
})

describe('useRescueMap 站点配置', () => {
  it('未传入 SOS 中心时使用 config/site.json 的 map.center', async () => {
    const rescueMap = useRescueMap({ loadMap: async () => createFakeAmap(map, markers) })
    await rescueMap.initMap('map')

    expect(map.options.center).toEqual([
      siteConfig.mapCenter.longitude,
      siteConfig.mapCenter.latitude,
    ])
  })
})

describe('useRescueMap lifecycle', () => {
  it('does not create a map when loading finishes after destruction', async () => {
    const pendingModule = deferred<AMapModule>()
    const rescueMap = useRescueMap({ loadMap: () => pendingModule.promise })

    const initialization = rescueMap.initMap('map')
    rescueMap.destroyMap()
    pendingModule.resolve(createFakeAmap(map, markers))
    await initialization
    rescueMap.syncMarkers(sos, [participant()], noColors)

    expect(map.add).not.toHaveBeenCalled()
  })
})
