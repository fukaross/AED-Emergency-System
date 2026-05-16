import { describe, expect, it } from 'vitest'
import {
  mergeLocationText,
  pickReverseGeocodeAddress,
} from '@/services/location-address-service'

function formatLocationFallback(lat: number, lng: number): string {
  return `定位坐标 ${lat.toFixed(6)}, ${lng.toFixed(6)}`
}

describe('mergeLocationText', () => {
  it('combines name and address when both are available', () => {
    expect(mergeLocationText('理科楼', '海南省海口市REDACTED')).toBe(
      '理科楼 海南省海口市REDACTED',
    )
  })

  it('avoids duplicating the place name already included in address', () => {
    expect(mergeLocationText('理科楼', 'REDACTED理科楼一层')).toBe(
      'REDACTED理科楼一层',
    )
  })

  it('returns the available field when only one exists', () => {
    expect(mergeLocationText('理科楼', '')).toBe('理科楼')
    expect(mergeLocationText('', 'REDACTED理科楼一层')).toBe(
      'REDACTED理科楼一层',
    )
  })
})

describe('pickReverseGeocodeAddress', () => {
  it('prefers the recommended formatted address', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          address: '海南省海口市美兰区文临路8号',
          formatted_addresses: {
            recommend: 'REDACTED理科楼附近',
          },
        },
      }),
    ).toBe('REDACTED理科楼附近')
  })

  it('falls back to poi title and address when recommend is missing', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          pois: [
            {
              title: '理科楼',
              address: '海南省海口市REDACTED',
            },
          ],
        },
      }),
    ).toBe('理科楼 海南省海口市REDACTED')
  })

  it('falls back to the plain address when no poi exists', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          address: '海南省海口市美兰区文临路8号',
        },
      }),
    ).toBe('海南省海口市美兰区文临路8号')
  })
})

describe('location fallback formatting', () => {
  it('keeps coordinates readable when no address is available', () => {
    expect(formatLocationFallback(19.97654712, 110.51346091)).toBe(
      '定位坐标 19.976547, 110.513461',
    )
  })
})
