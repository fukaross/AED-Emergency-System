import { describe, it, expect } from 'vitest'
import {
  ADMIN_MAP_CENTER,
  lngLatToPoint,
  pointToLngLat,
  isValidCoordinate,
  getDisplayCenter,
} from './map-coordinate'

describe('map-coordinate', () => {
  describe('lngLatToPoint', () => {
    it('converts [lng, lat] to { lat, lng }', () => {
      expect(lngLatToPoint([110.513, 19.977])).toEqual({ lat: 19.977, lng: 110.513 })
    })
  })

  describe('pointToLngLat', () => {
    it('converts { lat, lng } to [lng, lat]', () => {
      expect(pointToLngLat(19.977, 110.513)).toEqual([110.513, 19.977])
    })
  })

  describe('isValidCoordinate', () => {
    it('returns false for 0/0', () => {
      expect(isValidCoordinate(0, 0)).toBe(false)
    })

    it('returns false for near-zero', () => {
      expect(isValidCoordinate(0.001, 0.001)).toBe(false)
    })

    it('returns false for out-of-range lat', () => {
      expect(isValidCoordinate(91, 110)).toBe(false)
      expect(isValidCoordinate(-91, 110)).toBe(false)
    })

    it('returns false for out-of-range lng', () => {
      expect(isValidCoordinate(20, 181)).toBe(false)
      expect(isValidCoordinate(20, -181)).toBe(false)
    })

    it('returns true for valid coordinates', () => {
      expect(isValidCoordinate(19.977, 110.513)).toBe(true)
    })
  })

  describe('getDisplayCenter', () => {
    it('returns actual coordinates when valid', () => {
      expect(getDisplayCenter(19.977, 110.513)).toEqual([110.513, 19.977])
    })

    it('returns ADMIN_MAP_CENTER for 0/0', () => {
      expect(getDisplayCenter(0, 0)).toEqual([
        ADMIN_MAP_CENTER.lng,
        ADMIN_MAP_CENTER.lat,
      ])
    })
  })
})
