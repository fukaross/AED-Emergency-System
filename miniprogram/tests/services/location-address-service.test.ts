import { describe, expect, it } from 'vitest'
import {
  mergeLocationText,
  pickReverseGeocodeAddress,
} from '@/services/location-address-service'

describe('mergeLocationText', () => {
  it('combines name and address when both are available', () => {
    expect(mergeLocationText('理科楼', '示范园区')).toBe(
      '理科楼 示范园区',
    )
  })

  it('avoids duplicating the place name already included in address', () => {
    expect(mergeLocationText('演示楼', '示范园区演示楼一层')).toBe(
      '示范园区演示楼一层',
    )
  })

  it('returns the available field when only one exists', () => {
    expect(mergeLocationText('理科楼', '')).toBe('理科楼')
    expect(mergeLocationText('', '示范园区演示楼一层')).toBe(
      '示范园区演示楼一层',
    )
  })
})

describe('pickReverseGeocodeAddress', () => {
  it('prefers the recommended formatted address', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          address: '示范园区演示路 8 号',
          formatted_addresses: {
            recommend: '示范园区演示楼附近',
          },
        },
      }),
    ).toBe('示范园区演示楼附近')
  })

  it('falls back to poi title and address when recommend is missing', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          pois: [
            {
              title: '理科楼',
              address: '示范园区',
            },
          ],
        },
      }),
    ).toBe('理科楼 示范园区')
  })

  it('falls back to the plain address when no poi exists', () => {
    expect(
      pickReverseGeocodeAddress({
        status: 0,
        result: {
          address: '示范园区演示路 8 号',
        },
      }),
    ).toBe('示范园区演示路 8 号')
  })
})
