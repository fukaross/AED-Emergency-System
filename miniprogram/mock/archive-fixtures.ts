// 急救档案 Mock 查询层 —— 数据来自 demo 合成档案，当前身份跟随演示角色切换。
import type {
  ArchiveDetail,
  RescuerArchiveDetail,
  RescuerArchiveListItem,
  RescuerCertificateRecord,
  RescuerSummary,
  ResponderQualification,
  SchoolProfile,
} from '@/types/user'
import {
  MOCK_ARCHIVE_PROFILES,
  MOCK_CERTIFICATE_RECORDS,
  MOCK_RESPONDER_QUALIFICATIONS,
  resolveArchiveProfileId,
  type RescuerArchiveProfileRecord,
} from './archive-row-adapter'
import {
  resolveCurrentRescuerProfile,
  resolveDemoSchoolBinding,
  toMockArchiveId,
} from './demo-fixtures'
import { DEMO_RESCUER_PROFILES } from './generated/demo-rescuer-profiles.generated'
import type { DemoRescuerProfile } from './demo-types'

function findDemoProfileByArchiveId(archiveId: string): DemoRescuerProfile | null {
  return (
    DEMO_RESCUER_PROFILES.find(
      (profile) => toMockArchiveId(profile.id) === archiveId,
    ) ?? null
  )
}

/** 当前演示角色的档案 id（求助人角色没有档案）。 */
export function resolveCurrentArchiveProfileId(): string | null {
  const profile = resolveCurrentRescuerProfile()
  return profile ? toMockArchiveId(profile.id) : null
}

export function cloneSchoolProfile(
  schoolProfile: SchoolProfile | null,
): SchoolProfile | null {
  return schoolProfile ? { ...schoolProfile } : null
}

export function getArchiveProfileRecord(
  archiveId: string,
): RescuerArchiveProfileRecord | null {
  return MOCK_ARCHIVE_PROFILES.find((profile) => profile.id === archiveId) ?? null
}

export function getArchiveSchoolProfile(
  archiveId: string,
): SchoolProfile | null {
  const profile = findDemoProfileByArchiveId(archiveId)
  if (!profile?.boundOpenId) return null
  const binding = resolveDemoSchoolBinding(profile.boundOpenId)
  if (!binding) return null

  return {
    personType: binding.personType,
    name: binding.name,
    college: binding.college,
    className: binding.className,
    studentId: binding.studentId ?? undefined,
    staffId: binding.staffId ?? undefined,
    phone: profile.phone,
    bindingStatus: 'active',
  }
}

export function getResponderQualification(
  archiveId: string,
): ResponderQualification | null {
  const qualification = MOCK_RESPONDER_QUALIFICATIONS.find(
    (item) => item.rescuerArchiveProfileId === archiveId,
  )
  return qualification ? { ...qualification } : null
}

function cloneCertificateRecord(
  record: RescuerCertificateRecord,
): RescuerCertificateRecord {
  return {
    ...record,
    sourceTrace: {
      ...record.sourceTrace,
      batch: { ...record.sourceTrace.batch },
    },
  }
}

export function getCertificateRecords(
  archiveId: string,
): readonly RescuerCertificateRecord[] {
  return MOCK_CERTIFICATE_RECORDS
    .filter((record) => record.profileId === archiveId)
    .map(cloneCertificateRecord)
    .sort((left, right) => (right.issueDate ?? '').localeCompare(left.issueDate ?? ''))
}

function getCurrentCertificateRecord(
  archiveId: string,
  currentCertificateRecordId?: string | null,
): RescuerCertificateRecord | null {
  const certificates = getCertificateRecords(archiveId)
  if (!currentCertificateRecordId) {
    return certificates.find((record) => record.isCurrent) ?? null
  }
  return certificates.find((record) => record.id === currentCertificateRecordId) ?? null
}

export function buildRescuerSummary(archiveId: string): RescuerSummary | null {
  const qualification = getResponderQualification(archiveId)
  if (!qualification) return null
  const certificate = getCurrentCertificateRecord(
    archiveId,
    qualification.currentCertificateRecordId,
  )
  const profile = getArchiveProfileRecord(archiveId)

  return {
    archiveProfileId: archiveId,
    responderProfileId: qualification.id,
    responderType: qualification.responderType,
    responderStatus: qualification.status,
    level: qualification.level,
    certified: qualification.certified,
    certExpiry: qualification.certExpiry ?? null,
    currentCertificateRecordId: qualification.currentCertificateRecordId ?? null,
    currentCertificateNo: certificate?.certificateNo ?? null,
    workUnit: profile?.workUnit ?? null,
  }
}

function buildArchiveListItem(
  qualification: ResponderQualification,
): RescuerArchiveListItem | null {
  const archiveId = qualification.rescuerArchiveProfileId
  const profile = getArchiveProfileRecord(archiveId)
  const schoolProfile = getArchiveSchoolProfile(archiveId)
  const certificate = getCurrentCertificateRecord(
    archiveId,
    qualification.currentCertificateRecordId,
  )
  if (!profile || !qualification.level || !certificate) return null

  return {
    id: profile.id,
    name: profile.name,
    phone: profile.phoneMasked,
    level: qualification.level,
    college: schoolProfile?.college ?? profile.workUnit ?? '',
    certExpiry: qualification.certExpiry ?? '',
    certExpired: qualification.certExpired,
    schoolBindingStatus: schoolProfile?.bindingStatus ?? 'unbound',
  }
}

export function buildArchiveListItemById(
  archiveId: string,
): RescuerArchiveListItem | null {
  const qualification = getResponderQualification(archiveId)
  return qualification ? buildArchiveListItem(qualification) : null
}

function resolveDemoOrder(archiveId: string): number {
  const profile = findDemoProfileByArchiveId(archiveId)
  return profile ? DEMO_RESCUER_PROFILES.indexOf(profile) : Number.MAX_SAFE_INTEGER
}

/** 其他人（有效资格）的档案列表；当前演示角色自己的档案单独展示。 */
export function buildActiveArchiveListItems(): RescuerArchiveListItem[] {
  const currentArchiveId = resolveCurrentArchiveProfileId()
  return MOCK_RESPONDER_QUALIFICATIONS
    .filter((item) => item.status === 'active')
    .filter((item) => item.rescuerArchiveProfileId !== currentArchiveId)
    .map(buildArchiveListItem)
    .filter((item): item is RescuerArchiveListItem => item !== null)
    .sort(
      (left, right) => resolveDemoOrder(left.id) - resolveDemoOrder(right.id),
    )
}

export function buildArchiveDetail(archiveId: string): ArchiveDetail | null {
  const profile = getArchiveProfileRecord(archiveId)
  if (!profile) return null
  const schoolProfile = getArchiveSchoolProfile(archiveId)

  return {
    id: profile.id,
    profile: {
      id: profile.id,
      name: profile.name,
      gender: profile.gender,
      birthDate: profile.birthDate,
      phoneMasked: profile.phoneMasked,
      idMasked: profile.idMasked,
      workUnit: profile.workUnit ?? null,
    },
    schoolProfile,
    schoolBindingStatus: schoolProfile?.bindingStatus ?? 'unbound',
    responderQualification: getResponderQualification(archiveId),
    certificateRecords: getCertificateRecords(archiveId),
  } satisfies RescuerArchiveDetail
}

export { resolveArchiveProfileId }
