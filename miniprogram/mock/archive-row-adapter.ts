// 急救档案的投影层 —— demo 合成档案 → 档案详情/证书/资格 DTO 形状。
// 全部字段来自 demo/data/rescuer-profiles.json；证书到期按后端规则“签发 + 3 年”计算。
import {
  DEMO_REFERENCE_DATE,
  resolveDemoCampusName,
  toMockArchiveId,
  toMockCertificateId,
  toMockResponderId,
} from './demo-fixtures'
import { DEMO_RESCUER_PROFILES } from './generated/demo-rescuer-profiles.generated'
import type { DemoRescuerProfile } from './demo-types'
import type {
  Gender,
  RescuerArchiveProfileDetail,
  RescuerCertificateRecord,
  RescuerImportBatch,
  ResponderQualification,
} from '@/types/user'
import { maskIdNumber, maskPhone } from '@/utils/format'

const DEMO_IMPORTED_AT = '2026-10-26T00:00:00Z'

export interface RescuerArchiveProfileRecord
  extends RescuerArchiveProfileDetail {
  readonly phoneRaw: string
  readonly idNumberRaw: string
  readonly identityHash: string
}

function toGender(profile: DemoRescuerProfile): Gender {
  return profile.gender === 'male' ? 'male' : 'female'
}

function buildIdentityHash(profile: DemoRescuerProfile): string {
  return `id:${profile.idNumber.trim().toLowerCase()}`
}

function createProfileRecord(
  profile: DemoRescuerProfile,
): RescuerArchiveProfileRecord {
  return {
    id: toMockArchiveId(profile.id),
    name: profile.name,
    gender: toGender(profile),
    birthDate: profile.birthDate,
    workUnit: profile.workUnit.trim() || resolveDemoCampusName(profile.campus),
    phoneMasked: maskPhone(profile.phone),
    idMasked: maskIdNumber(profile.idNumber),
    phoneRaw: profile.phone,
    idNumberRaw: profile.idNumber,
    identityHash: buildIdentityHash(profile),
  }
}

export const MOCK_ARCHIVE_PROFILES: readonly RescuerArchiveProfileRecord[] =
  DEMO_RESCUER_PROFILES.map(createProfileRecord)

export function resolveArchiveProfileId(profileId: string): string {
  return toMockArchiveId(profileId)
}

export function resolveCampusName(profile: DemoRescuerProfile): string {
  return resolveDemoCampusName(profile.campus)
}

/** 证书到期：签发日期 + 3 年（与后端现有规则一致）。 */
export function resolveCertExpiry(issueDate: string): string {
  const year = Number(issueDate.slice(0, 4)) + 3
  return `${year}-${issueDate.slice(5, 7)}`
}

function resolveIsCertExpired(issueDate: string): boolean {
  return resolveCertExpiry(issueDate) < DEMO_REFERENCE_DATE.slice(0, 7)
}

function createBatch(profile: DemoRescuerProfile): RescuerImportBatch {
  return {
    id: `batch-${profile.trainingYear}-${profile.sourceRowNo}`,
    sourceYear: profile.trainingYear,
    sourceFileName: profile.sourceFile,
    importedAt: DEMO_IMPORTED_AT,
    operator: 'demo-seed',
    note: `${profile.trainingSessionName}（demo-v1 合成批次）`,
  }
}

export function createCertificateFromProfile(
  profile: DemoRescuerProfile,
): RescuerCertificateRecord {
  return {
    id: toMockCertificateId(profile.id),
    profileId: toMockArchiveId(profile.id),
    certificateNo: profile.certificateNo,
    issuingAuthority: profile.issuingAuthority,
    issueDate: profile.issueDate,
    trainingYear: profile.trainingYear,
    trainingProject: profile.trainingProject,
    trainingSessionName: profile.trainingSessionName,
    theoryScore: profile.theoryScore,
    traumaAssessmentResult: profile.traumaAssessmentResult || null,
    cprAssessmentResult: profile.cprAssessmentResult || null,
    aedAssessmentResult: profile.aedAssessmentResult || null,
    cprAedCombinedResult: profile.cprAedCombinedResult || null,
    remark: profile.remark || null,
    isCurrent: true,
    sourceTrace: {
      sourceFile: profile.sourceFile,
      sourceSheet: profile.sourceSheet,
      sourceRowNo: profile.sourceRowNo,
      sourceSeqNo: profile.sourceSeqNo,
      batch: createBatch(profile),
    },
  }
}

export function createQualificationFromProfile(
  profile: DemoRescuerProfile,
): ResponderQualification {
  const certified =
    profile.qualificationStatus === 'active'
    && profile.cprAedCombinedResult === '合格'

  return {
    id: toMockResponderId(profile.id),
    userId: profile.boundOpenId,
    rescuerArchiveProfileId: toMockArchiveId(profile.id),
    currentCertificateRecordId: toMockCertificateId(profile.id),
    responderType: 'rescuer',
    status: profile.qualificationStatus,
    level: profile.level,
    certified,
    certExpiry: resolveCertExpiry(profile.issueDate),
    certExpired: resolveIsCertExpired(profile.issueDate),
    sourceType: 'school_import',
    sourceBatchNo: `${profile.trainingYear}-${profile.sourceRowNo}`,
    remark: '合成演示档案（demo-v1）',
  }
}

export const MOCK_CERTIFICATE_RECORDS: readonly RescuerCertificateRecord[] =
  DEMO_RESCUER_PROFILES.map(createCertificateFromProfile)

export const MOCK_RESPONDER_QUALIFICATIONS: readonly ResponderQualification[] =
  DEMO_RESCUER_PROFILES.map(createQualificationFromProfile)
