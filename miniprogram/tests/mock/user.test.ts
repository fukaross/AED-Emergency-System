import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  canMockViewFullPhone,
  getMockArchiveDetail,
  loadMockUserProfile,
  queryMockResponderClaims,
  queryMockSchoolBindings,
  queryMockArchives,
  saveMockResponderClaim,
  saveMockSchoolBinding,
  saveMockUserProfile,
} from '@/mock/user'
import { FEATURED_MOCK_RESCUER_ARCHIVE_ID } from '@/mock/rescuer-qualification-fixtures'
import { SOURCE_RESCUER_ARCHIVE_ROWS } from '@/mock/rescuer-source-rows'
import { MOCK_PROFILE_AVATAR_URL } from '@/mock/avatar-fixtures'

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

  it('derives archive detail and certificate trace from source rows', () => {
    const detail = getMockArchiveDetail('archive-jzr-2023-00762')

    expect(detail?.profile.name).toBe('REDACTED')
    expect(detail?.profile.phoneMasked).toBe('182****7261')
    expect(detail?.profile.idMasked).toBe('460033******326X')
    expect(detail?.certificateRecords).toHaveLength(1)
    expect(detail?.certificateRecords[0]?.certificateNo).toBe(
      '460000232004231001532',
    )
    expect(detail?.certificateRecords[0]?.sourceTrace.sourceRowNo).toBe(39)
  })

  it('lists active source rows and excludes self from items', () => {
    const result = queryMockArchives()

    expect(SOURCE_RESCUER_ARCHIVE_ROWS).toHaveLength(31)
    expect(result.total).toBe(30)
    expect(result.selfArchive?.id).toBe('archive-jzr-2023-00762')
    expect(result.items).toHaveLength(20)
    expect(result.items[0]?.id).toBe(FEATURED_MOCK_RESCUER_ARCHIVE_ID)
    expect(result.items[0]?.name).toBe('REDACTED')
    expect(result.items[0]?.level).toBe('honor')
    expect(result.items.some((item) => item.id === result.selfArchive?.id)).toBe(
      false,
    )
  })

  it('keeps self archive outside keyword and level filters', () => {
    const result = queryMockArchives({ keyword: 'REDACTED', level: 'core' })

    expect(result.total).toBe(0)
    expect(result.selfArchive?.id).toBe('archive-jzr-2023-00762')
    expect(result.items).toHaveLength(0)
  })

  it('filters other archive rows by keyword and level', () => {
    const result = queryMockArchives({ keyword: 'REDACTED', level: 'trainee' })

    expect(result.total).toBe(1)
    expect(result.items.map((item) => item.id)).toEqual([
      'archive-jzr-2023-00169',
    ])
  })

  it('paginates active archive rows derived from source data', () => {
    const result = queryMockArchives({ page: 2, pageSize: 20 })

    expect(result.total).toBe(30)
    expect(result.items).toHaveLength(10)
    expect(result.items.some((item) => item.id === FEATURED_MOCK_RESCUER_ARCHIVE_ID)).toBe(
      false,
    )
  })

  it('preserves rescuer summary when saving editable profile fields', () => {
    const updated = saveMockUserProfile({ name: '新的昵称' })
    const reloaded = loadMockUserProfile()

    expect(updated.name).toBe('新的昵称')
    expect(reloaded.avatarUrl).toBe(MOCK_PROFILE_AVATAR_URL)
    expect(reloaded.gender).toBe('female')
    expect(reloaded.phone).toBe('REDACTED')
    expect(reloaded.rescuerSummary?.archiveProfileId).toBe(
      'archive-jzr-2023-00762',
    )
    expect(reloaded.rescuerSummary?.currentCertificateNo).toBe(
      '460000232004231001532',
    )
  })

  it('replaces legacy default avatar path with mock avatar url', () => {
    storage.set(
      'aed_mock_user_profile',
      JSON.stringify({ avatarUrl: '/static/images/avatar-default.png' }),
    )

    const profile = loadMockUserProfile()

    expect(profile.avatarUrl).toBe(MOCK_PROFILE_AVATAR_URL)
  })

  it('does not allow full phone access for the default mock viewer', () => {
    expect(canMockViewFullPhone()).toBe(false)
  })

  it('stores mock school binding application', () => {
    const response = saveMockSchoolBinding({
      personType: 'student',
      name: '王小明',
      college: '教育学院',
      studentId: '20260101',
    })
    const result = queryMockSchoolBindings()

    expect(response.status).toBe('submitted')
    expect(result.items[0]?.payload.studentId).toBe('20260101')
  })

  it('stores mock responder claim application', () => {
    const response = saveMockResponderClaim({
      name: '王小明',
      idNumberLast6: '10011X',
      phone: 'REDACTED',
      certificateNo: 'CERT20260001',
    })
    const result = queryMockResponderClaims()

    expect(response.status).toBe('submitted')
    expect(result.items[0]?.payload.certificateNo).toBe('CERT20260001')
  })
})
