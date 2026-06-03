// 用户、档案、急救员和课程相关的类型定义

export type Gender = 'male' | 'female' | 'unknown'

export type PrimaryRole = 'student' | 'teacher' | 'staff' | 'admin'

export type RescuerLevel = 'honor' | 'trainee' | 'official' | 'core'

export type ResponderType = 'rescuer' | 'duty_teacher'

export type ResponderStatus = 'active' | 'inactive'

export type RescuerSourceType = 'school_import'

export type BindingStatus =
  | 'pending'
  | 'active'
  | 'rejected'
  | 'replaced'
  | 'unbound'

export interface SchoolProfile {
  readonly profileId?: number
  readonly personType?: 'student' | 'teacher' | 'staff'
  readonly name?: string
  readonly college: string
  readonly className?: string | null
  readonly studentId?: string
  readonly staffId?: string
  readonly phone?: string | null
  readonly bindingStatus: BindingStatus
  readonly boundAt?: string
}

export interface RescuerSummary {
  readonly archiveProfileId: string
  readonly responderProfileId: string
  readonly responderType: ResponderType
  readonly responderStatus: ResponderStatus
  readonly level: RescuerLevel | null
  readonly certified: boolean
  readonly certExpiry?: string | null
  readonly currentCertificateRecordId?: string | null
  readonly currentCertificateNo?: string | null
  readonly workUnit?: string | null
}

export interface UserProfile {
  readonly id: string
  readonly name: string
  readonly avatarUrl: string
  readonly gender: Gender
  readonly phone: string
  readonly primaryRole: PrimaryRole
  readonly certified: boolean
  readonly level: RescuerLevel | null
  readonly schoolProfile: SchoolProfile | null
  readonly rescuerSummary?: RescuerSummary | null
}

export interface UpdateUserProfilePayload {
  readonly name?: string
  readonly avatarUrl?: string
  readonly gender?: Gender
  readonly phone?: string
}

export interface RescuerArchiveListParams {
  readonly keyword?: string
  readonly level?: RescuerLevel
  readonly page?: number
  readonly pageSize?: number
}

export interface RescuerArchiveListItem {
  readonly id: string
  readonly name: string
  readonly phone: string
  readonly level: RescuerLevel | null
  readonly college: string
  readonly certExpiry: string
  readonly certExpired: boolean
  readonly schoolBindingStatus?: BindingStatus
}

export interface RescuerArchiveListResponse {
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
  readonly canViewFullPhone: boolean
  readonly selfArchive: RescuerArchiveListItem | null
  readonly items: readonly RescuerArchiveListItem[]
}

export interface RescuerImportBatch {
  readonly id: string
  readonly sourceYear: number
  readonly sourceFileName?: string | null
  readonly importedAt: string
  readonly operator?: string | null
  readonly note?: string | null
}

export interface RescuerArchiveSourceTrace {
  readonly sourceFile?: string | null
  readonly sourceSheet?: string | null
  readonly sourceRowNo: number
  readonly sourceSeqNo?: string | null
  readonly batch: RescuerImportBatch
}

export interface RescuerCertificateRecord {
  readonly id: string
  readonly profileId: string
  readonly certificateNo?: string | null
  readonly issuingAuthority?: string | null
  readonly issueDate?: string | null
  readonly trainingYear: number
  readonly trainingProject?: string | null
  readonly trainingSessionName?: string | null
  readonly theoryScore: number | null
  readonly traumaAssessmentResult: string | null
  readonly cprAssessmentResult: string | null
  readonly aedAssessmentResult: string | null
  readonly cprAedCombinedResult: string | null
  readonly remark?: string | null
  readonly isCurrent: boolean
  readonly sourceTrace: RescuerArchiveSourceTrace
}

// 普通详情接口里只返回脱敏信息
export interface RescuerArchiveProfileDetail {
  readonly id: string
  readonly name: string
  readonly gender: Gender
  readonly birthDate: string
  readonly phoneMasked: string
  readonly idMasked: string
  readonly workUnit?: string | null
}

export interface ResponderQualification {
  readonly id: string
  readonly userId?: string | null
  readonly rescuerArchiveProfileId: string
  readonly currentCertificateRecordId?: string | null
  readonly responderType: ResponderType
  readonly status: ResponderStatus
  readonly level: RescuerLevel | null
  readonly certified: boolean
  readonly certExpiry?: string | null
  readonly certExpired: boolean
  readonly sourceType: RescuerSourceType
  readonly sourceBatchNo?: string | null
  readonly remark?: string | null
}

export interface RescuerArchiveDetail {
  readonly id: string
  readonly profile: RescuerArchiveProfileDetail
  readonly schoolProfile: SchoolProfile | null
  readonly schoolBindingStatus?: BindingStatus
  readonly responderQualification: ResponderQualification | null
  readonly certificateRecords: readonly RescuerCertificateRecord[]
}

// 兼容老命名，逐步迁移
export type ArchiveListParams = RescuerArchiveListParams
export type ArchiveItem = RescuerArchiveListItem
export type ArchiveListResponse = RescuerArchiveListResponse
export type ArchiveDetail = RescuerArchiveDetail

export interface CourseSummary {
  readonly id: string
  readonly title: string
  readonly thumbnail: string
  readonly type: 'article' | 'video'
  readonly category: string
  readonly createdAt: string
  readonly pinned?: boolean
  readonly sortOrder?: number
  readonly cacheVersion?: string
  readonly estimatedTime?: string
}

export type SchoolPersonType = 'student' | 'teacher' | 'staff'
export type IdentityApplicationStatus = 'submitted' | 'approved' | 'rejected'

export interface SubmitSchoolBindingPayload {
  readonly personType: SchoolPersonType
  readonly name: string
  readonly college: string
  readonly className?: string
  readonly studentId?: string
  readonly staffId?: string
  readonly phone?: string
  readonly note?: string
}

export interface SubmitResponderClaimPayload {
  readonly name: string
  readonly idNumberLast6: string
  readonly phone: string
  readonly certificateNo?: string
  readonly note?: string
}

export interface IdentityApplicationResponse {
  readonly applicationId: string
  readonly status: IdentityApplicationStatus
  readonly submittedAt: string
}

export interface SchoolBindingPayloadRecord {
  readonly personType: SchoolPersonType
  readonly name: string
  readonly college: string
  readonly className?: string | null
  readonly studentId?: string | null
  readonly staffId?: string | null
  readonly phone?: string | null
  readonly note?: string | null
}

export interface SchoolBindingDetail {
  readonly applicationId: string
  readonly status: IdentityApplicationStatus
  readonly schoolProfileId?: number | null
  readonly submittedAt: string
  readonly updatedAt: string
  readonly reviewNote?: string | null
  readonly reviewedAt?: string | null
  readonly payload: SchoolBindingPayloadRecord
}

export interface ResponderClaimPayloadRecord {
  readonly name: string
  readonly idNumberLast6: string
  readonly phone: string
  readonly certificateNo?: string | null
  readonly note?: string | null
}

export interface ResponderClaimDetail {
  readonly applicationId: string
  readonly status: IdentityApplicationStatus
  readonly responderProfileId?: string | null
  readonly submittedAt: string
  readonly updatedAt: string
  readonly reviewNote?: string | null
  readonly reviewedAt?: string | null
  readonly payload: ResponderClaimPayloadRecord
}

export interface IdentityApplicationListResponse<T> {
  readonly items: readonly T[]
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
}

export interface CourseDetail extends CourseSummary {
  readonly content: string
  readonly videoUrl: string | null
  readonly videos: readonly CourseVideo[]
  readonly disclaimer?: string
  readonly cacheVersion?: string
  readonly estimatedTime?: string
  readonly viewCount?: number
  readonly steps: readonly CourseStep[]
}

export interface CourseVideo {
  readonly order: number
  readonly title: string
  readonly url: string
}

export interface CourseStep {
  readonly order?: number
  readonly title: string
  readonly description: string
  readonly image: string
  readonly keyPoints?: readonly string[]
}

export interface CourseFilter {
  readonly page?: number
  readonly pageSize?: number
  readonly category?: string
}
