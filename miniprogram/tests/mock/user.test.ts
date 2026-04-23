import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  canMockViewFullPhone,
  getMockArchiveDetail,
  loadMockUserProfile,
  queryMockArchives,
  saveMockUserProfile,
} from '@/mock/user'

const storage = new Map<string, string>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
})

describe('mock rescuer archives', () => {
  beforeEach(() => {
    storage.clear()
    vi.clearAllMocks()
  })

  it('keeps multi-certificate history under one archive profile', () => {
    const detail = getMockArchiveDetail('archive-001')

    expect(detail?.certificateRecords).toHaveLength(2)
    expect(detail?.certificateRecords.map((item) => item.certificateNo)).toEqual([
      '460000232004231001138',
      '460000232004231000138',
    ])
  })

  it('lists only active responder qualifications and excludes self', () => {
    const result = queryMockArchives()

    expect(result.total).toBe(2)
    expect(result.selfArchive?.id).toBe('archive-self')
    expect(result.items.map((item) => item.id)).toEqual([
      'archive-001',
      'archive-002',
    ])
  })

  it('keeps self archive outside keyword and level filters', () => {
    const result = queryMockArchives({ keyword: '张三', level: 'core' })

    expect(result.total).toBe(1)
    expect(result.selfArchive?.id).toBe('archive-self')
    expect(result.items.map((item) => item.id)).toEqual(['archive-001'])
  })

  it('retains inactive archive history outside the current list', () => {
    const result = queryMockArchives()
    const inactiveDetail = getMockArchiveDetail('archive-003')

    expect(result.items.some((item) => item.id === 'archive-003')).toBe(false)
    expect(inactiveDetail?.certificateRecords).toHaveLength(1)
    expect(inactiveDetail?.responderQualification?.status).toBe('inactive')
  })

  it('preserves rescuer summary when saving editable profile fields', () => {
    const updated = saveMockUserProfile({ name: '新的昵称' })
    const reloaded = loadMockUserProfile()

    expect(updated.name).toBe('新的昵称')
    expect(reloaded.avatarUrl).toBe(
      'https://loremflickr.com/200/200/landscape,nature?lock=201',
    )
    expect(reloaded.gender).toBe('female')
    expect(reloaded.phone).toBe('REDACTED')
    expect(reloaded.rescuerSummary?.archiveProfileId).toBe('archive-self')
    expect(reloaded.rescuerSummary?.currentCertificateNo).toBe(
      '460000242004231000301',
    )
  })

  it('replaces legacy default avatar path with mock avatar url', () => {
    storage.set(
      'aed_mock_user_profile',
      JSON.stringify({ avatarUrl: '/static/images/avatar-default.png' }),
    )

    const profile = loadMockUserProfile()

    expect(profile.avatarUrl).toBe(
      'https://loremflickr.com/200/200/landscape,nature?lock=201',
    )
  })

  it('does not allow full phone access for the default mock viewer', () => {
    expect(canMockViewFullPhone()).toBe(false)
  })
})
