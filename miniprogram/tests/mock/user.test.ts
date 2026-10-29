import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  canMockViewFullPhone,
  getMockArchiveDetail,
  loadMockUserProfile,
  queryMockArchives,
  queryMockResponderClaims,
  queryMockSchoolBindings,
  saveMockResponderClaim,
  saveMockSchoolBinding,
  saveMockUserProfile,
} from '@/mock/user'
import {
  DEMO_ROLE_STORAGE_KEY,
  resetActiveDemoRole,
  setActiveDemoRole,
} from '@/mock/demo-role'
import { DEMO_RESCUER_PROFILES } from '@/mock/generated/demo-rescuer-profiles.generated'
import { DEMO_USERS } from '@/mock/generated/demo-roles.generated'

const storage = new Map<string, string>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key) ?? ''),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
})

const ACTIVE_RESCUER_ARCHIVE_ID = 'archive-demo-rsp-001'

describe('mock user identity and rescuer archives (demo-v1)', () => {
  beforeEach(() => {
    storage.clear()
    resetActiveDemoRole()
    vi.clearAllMocks()
  })

  it('builds the requester identity from the demo role registry', () => {
    const profile = loadMockUserProfile()
    const requester = DEMO_USERS.find(
      (user) => user.openId === 'demo-openid-requester',
    )

    expect(profile.id).toBe('demo-openid-requester')
    expect(profile.name).toBe(requester?.name)
    expect(profile.phone).toBe('13000000001')
    expect(profile.gender).toBe('female')
    expect(profile.primaryRole).toBe('student')
    expect(profile.certified).toBe(false)
    expect(profile.level).toBeNull()
    expect(profile.rescuerSummary).toBeNull()
    expect(profile.schoolProfile?.college).toBe('演示学院')
    expect(profile.schoolProfile?.bindingStatus).toBe('active')
  })

  it('switches identity to the active-rescuer demo role', () => {
    setActiveDemoRole('demo-rescuer')
    const profile = loadMockUserProfile()

    expect(profile.name).toBe('演示乙（急救员·东区）')
    expect(profile.phone).toBe('13000000002')
    expect(profile.primaryRole).toBe('teacher')
    expect(profile.certified).toBe(true)
    expect(profile.level).toBe('official')
    expect(profile.rescuerSummary?.archiveProfileId).toBe(ACTIVE_RESCUER_ARCHIVE_ID)
    expect(profile.rescuerSummary?.currentCertificateNo).toBe('DEMO-CERT-2601')
    expect(profile.schoolProfile?.personType).toBe('teacher')
    expect(profile.schoolProfile?.staffId).toBe('DEMO-TEA-0001')
  })

  it('keeps the unqualified demo role non-certified', () => {
    setActiveDemoRole('demo-unqualified')
    const profile = loadMockUserProfile()

    expect(profile.name).toBe('演示丙（待认证）')
    expect(profile.certified).toBe(false)
    expect(profile.level).toBe('trainee')
    expect(profile.rescuerSummary?.responderStatus).toBe('inactive')
  })

  it('rejects unknown demo role codes instead of falling back silently', () => {
    expect(() => setActiveDemoRole('demo-unknown')).toThrow('未知演示角色 code')
  })

  it('fails loudly when the stored demo role code is unknown', () => {
    storage.set(DEMO_ROLE_STORAGE_KEY, 'demo-removed-role')

    expect(() => loadMockUserProfile()).toThrow('未知演示角色 code')
  })

  it('derives archive detail and certificate trace from demo data', () => {
    const detail = getMockArchiveDetail(ACTIVE_RESCUER_ARCHIVE_ID)

    expect(detail?.profile.name).toBe('演示乙（急救员·东区）')
    expect(detail?.profile.phoneMasked).toBe('130****0002')
    expect(detail?.profile.idMasked).toBe('990000******002X')
    expect(detail?.schoolProfile?.college).toBe('演示学院')
    expect(detail?.certificateRecords).toHaveLength(1)
    expect(detail?.certificateRecords[0]?.certificateNo).toBe('DEMO-CERT-2601')
    expect(detail?.certificateRecords[0]?.sourceTrace.sourceRowNo).toBe(1)
    expect(detail?.certificateRecords[0]?.sourceTrace.sourceSheet).toBe('demo-v1')
    expect(detail?.responderQualification?.status).toBe('active')
    expect(detail?.responderQualification?.certExpiry).toBe('2029-04')
  })

  it('lists active demo profiles, excluding inactive ones and self', () => {
    const result = queryMockArchives()

    // 有效资格档案 6 条（DEMO-RSP-002 资格失效不计入），求助人角色没有自己的档案
    expect(result.total).toBe(6)
    expect(result.selfArchive).toBeNull()
    expect(result.items.map((item) => item.id)).toEqual([
      'archive-demo-rsp-001',
      'archive-demo-rsp-003',
      'archive-demo-rsp-004',
      'archive-demo-rsp-005',
      'archive-demo-rsp-006',
      'archive-demo-rsp-007',
    ])
    expect(result.items.some((item) => item.id === 'archive-demo-rsp-002')).toBe(false)
  })

  it('exposes the current role archive as self and removes it from the list', () => {
    setActiveDemoRole('demo-rescuer')
    const result = queryMockArchives()

    expect(result.selfArchive?.id).toBe(ACTIVE_RESCUER_ARCHIVE_ID)
    expect(result.total).toBe(5)
    expect(result.items.some((item) => item.id === ACTIVE_RESCUER_ARCHIVE_ID)).toBe(false)
  })

  it('keeps self archive outside keyword and level filters', () => {
    setActiveDemoRole('demo-rescuer')
    const result = queryMockArchives({ keyword: '演示乙', level: 'official' })

    expect(result.total).toBe(0)
    expect(result.selfArchive?.id).toBe(ACTIVE_RESCUER_ARCHIVE_ID)
    expect(result.items).toHaveLength(0)
  })

  it('filters other archive rows by keyword and level', () => {
    const result = queryMockArchives({ keyword: '演示庚', level: 'official' })

    expect(result.total).toBe(1)
    expect(result.items.map((item) => item.id)).toEqual(['archive-demo-rsp-005'])
  })

  it('paginates active archive rows derived from demo data', () => {
    const result = queryMockArchives({ page: 2, pageSize: 4 })

    expect(result.total).toBe(6)
    expect(result.items.map((item) => item.id)).toEqual([
      'archive-demo-rsp-006',
      'archive-demo-rsp-007',
    ])
  })

  it('preserves rescuer summary when saving editable profile fields', () => {
    setActiveDemoRole('demo-rescuer')
    const updated = saveMockUserProfile({ name: '新的昵称' })
    const reloaded = loadMockUserProfile()

    expect(updated.name).toBe('新的昵称')
    expect(reloaded.name).toBe('新的昵称')
    expect(reloaded.avatarUrl).toBe('/static/images/avatar-default.png')
    expect(reloaded.gender).toBe('male')
    expect(reloaded.phone).toBe('13000000002')
    expect(reloaded.rescuerSummary?.archiveProfileId).toBe(ACTIVE_RESCUER_ARCHIVE_ID)
    expect(reloaded.rescuerSummary?.currentCertificateNo).toBe('DEMO-CERT-2601')
  })

  it('replaces legacy default avatar path with the demo avatar url', () => {
    storage.set(
      'aed_mock_user_profile',
      JSON.stringify({ avatarUrl: '/static/images/avatar-default.png' }),
    )

    const profile = loadMockUserProfile()

    expect(profile.avatarUrl).toBe('/static/images/avatar-default.png')
  })

  it('does not allow full phone access for the default mock viewer', () => {
    expect(canMockViewFullPhone()).toBe(false)
  })

  it('keeps every demo archive linked to a synthetic profile', () => {
    DEMO_RESCUER_PROFILES.forEach((profile) => {
      const detail = getMockArchiveDetail(`archive-${profile.id.toLowerCase()}`)
      expect(detail?.id).toBe(`archive-${profile.id.toLowerCase()}`)
      expect(detail?.profile.name).toBe(profile.name)
    })
  })

  it('stores mock school binding application', () => {
    const response = saveMockSchoolBinding({
      personType: 'student',
      name: '演示申请',
      college: '演示学院',
      studentId: 'DEMO-STU-9999',
    })
    const result = queryMockSchoolBindings()

    expect(response.status).toBe('submitted')
    expect(result.items[0]?.payload.studentId).toBe('DEMO-STU-9999')
  })

  it('stores mock responder claim application', () => {
    const response = saveMockResponderClaim({
      name: '演示申请',
      idNumberLast6: '000001',
      phone: '13000000009',
      certificateNo: 'DEMO-CERT-9999',
    })
    const result = queryMockResponderClaims()

    expect(response.status).toBe('submitted')
    expect(result.items[0]?.payload.certificateNo).toBe('DEMO-CERT-9999')
  })
})
