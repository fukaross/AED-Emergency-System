/**
 * 用户与档案相关类型定义
 *
 * 来源：CLAUDE.md §8.1 (user 系列接口)
 * 来源：BackEnd.md §4.6
 */

/** 性别 */
export type Gender = 'male' | 'female' | 'unknown'

/** 急救员级别 */
export type RescuerLevel = 'honor' | 'trainee' | 'official' | 'core'

/** 响应资格类型 */
export type ResponderType = 'rescuer' | 'duty_teacher'

/** 响应资格状态 */
export type ResponderStatus = 'active' | 'inactive'

/** 档案导入来源 */
export type RescuerSourceType = 'school_import'

/** 用户与学校档案绑定状态 */
export type BindingStatus =
  | 'pending'
  | 'active'
  | 'rejected'
  | 'replaced'
  | 'unbound'

/** 学校档案摘要 (只读) */
export interface SchoolProfile {
  readonly college: string
  readonly studentId?: string
  readonly staffId?: string
  readonly bindingStatus: BindingStatus
}

/** 当前急救员资格摘要 */
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

/** 用户信息 (GET /api/user/profile 响应) */
export interface UserProfile {
  readonly id: string
  readonly name: string
  readonly avatarUrl: string
  readonly gender: Gender
  readonly phone: string
  readonly certified: boolean
  readonly level: RescuerLevel | null
  readonly schoolProfile: SchoolProfile | null
  readonly rescuerSummary?: RescuerSummary | null
}

/** 更新用户信息请求 (PUT /api/user/profile) */
export interface UpdateUserProfilePayload {
  readonly name?: string
  readonly avatarUrl?: string
  readonly gender?: Gender
  readonly phone?: string
}

/** 急救档案列表请求参数 */
export interface RescuerArchiveListParams {
  readonly keyword?: string
  readonly level?: RescuerLevel
  readonly page?: number
  readonly pageSize?: number
}

/** 急救档案列表项 */
export interface RescuerArchiveListItem {
  readonly id: string
  readonly name: string
  readonly phone: string
  readonly level: RescuerLevel
  readonly college: string
  readonly certExpiry: string
  readonly certExpired: boolean
  readonly schoolBindingStatus?: BindingStatus
}

/** 急救档案列表响应 */
export interface RescuerArchiveListResponse {
  readonly total: number
  readonly page: number
  readonly pageSize: number
  readonly totalPages: number
  readonly canViewFullPhone: boolean
  readonly selfArchive: RescuerArchiveListItem | null
  readonly items: readonly RescuerArchiveListItem[]
}

/** 导入批次 */
export interface RescuerImportBatch {
  readonly id: string
  readonly sourceYear: number
  readonly sourceFileName: string
  readonly importedAt: string
  readonly operator?: string | null
  readonly note?: string | null
}

/** 来源追溯摘要 */
export interface RescuerArchiveSourceTrace {
  readonly sourceFile: string
  readonly sourceSheet: string
  readonly sourceRowNo: number
  readonly sourceSeqNo: string
  readonly batch: RescuerImportBatch
}

/** 证书 / 培训 / 考核历史记录 */
export interface RescuerCertificateRecord {
  readonly id: string
  readonly profileId: string
  readonly certificateNo: string
  readonly issuingAuthority: string
  readonly issueDate: string
  readonly trainingYear: number
  readonly trainingProject: string
  readonly trainingSessionName: string
  readonly theoryScore: number | null
  readonly traumaAssessmentResult: string | null
  readonly cprAssessmentResult: string | null
  readonly aedAssessmentResult: string | null
  readonly cprAedCombinedResult: string | null
  readonly remark?: string | null
  readonly isCurrent: boolean
  readonly sourceTrace: RescuerArchiveSourceTrace
}

/** 人员主档（普通详情接口仅返回脱敏信息） */
export interface RescuerArchiveProfileDetail {
  readonly id: string
  readonly name: string
  readonly gender: Gender
  readonly birthDate: string
  readonly phoneMasked: string
  readonly idMasked: string
  readonly workUnit?: string | null
}

/** 当前响应资格摘要 */
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

/** 急救档案详情 */
export interface RescuerArchiveDetail {
  readonly id: string
  readonly profile: RescuerArchiveProfileDetail
  readonly schoolProfile: SchoolProfile | null
  readonly schoolBindingStatus?: BindingStatus
  readonly responderQualification: ResponderQualification | null
  readonly certificateRecords: readonly RescuerCertificateRecord[]
}

/** 兼容现有命名 */
export type ArchiveListParams = RescuerArchiveListParams
export type ArchiveItem = RescuerArchiveListItem
export type ArchiveListResponse = RescuerArchiveListResponse
export type ArchiveDetail = RescuerArchiveDetail

/** 课程摘要 (列表项) */
export interface CourseSummary {
  readonly id: string
  readonly title: string
  readonly thumbnail: string
  readonly type: 'article' | 'video'
  readonly category: string
  readonly createdAt: string
  readonly pinned?: boolean
  readonly sortOrder?: number
  readonly isEmergency?: boolean
  readonly cacheVersion?: string
  readonly estimatedTime?: string
}

/** 课程详情 */
export interface CourseDetail extends CourseSummary {
  readonly content: string
  readonly videoUrl: string | null
  readonly disclaimer?: string
  readonly cacheVersion?: string
  readonly estimatedTime?: string
  readonly viewCount?: number
  readonly steps: readonly CourseStep[]
}

/** 课程步骤数据 */
export interface CourseStep {
  readonly order?: number
  readonly title: string
  readonly description: string
  readonly image: string
  readonly keyPoints?: readonly string[]
}

/** 课程筛选参数 */
export interface CourseFilter {
  readonly page?: number
  readonly pageSize?: number
  readonly category?: string
}

/** 按类型获取课程请求 */
export type CourseTypeKey = 'cpr' | 'aed' | 'heimlich'

/** 按类型获取课程响应 */
export interface CourseByTypeResponse {
  readonly id: string
  readonly title: string
  readonly contentType: 'article' | 'video' | 'steps'
  readonly entryMode: 'detail' | 'steps'
  readonly summary: string
}
