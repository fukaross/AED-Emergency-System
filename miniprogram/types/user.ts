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

/** 学校档案摘要 (只读) */
export interface SchoolProfile {
  readonly college: string
  readonly studentId?: string
  readonly staffId?: string
  readonly bindingStatus: string
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
}

/** 更新用户信息请求 (PUT /api/user/profile) */
export interface UpdateUserProfilePayload {
  readonly name?: string
  readonly avatarUrl?: string
  readonly gender?: Gender
  readonly phone?: string
}

/** 急救档案列表请求参数 */
export interface ArchiveListParams {
  readonly keyword?: string
  readonly level?: RescuerLevel
  readonly page?: number
  readonly pageSize?: number
}

/** 急救档案列表项 */
export interface ArchiveItem {
  readonly id: string
  readonly name: string
  readonly phone: string
  readonly level: RescuerLevel
  readonly college: string
  readonly certExpiry: string
  readonly certExpired: boolean
  readonly bindingStatus?: string
}

/** 急救档案列表响应 */
export interface ArchiveListResponse {
  readonly total: number
  readonly canViewFullPhone: boolean
  readonly items: readonly ArchiveItem[]
}

/** 课程摘要 (列表项) */
export interface CourseSummary {
  readonly id: string
  readonly title: string
  readonly thumbnail: string
  readonly type: 'article' | 'video'
  readonly category: string
  readonly createdAt: string
}

/** 课程详情 */
export interface CourseDetail extends CourseSummary {
  readonly content: string
  readonly videoUrl: string
  readonly steps: readonly CourseStep[]
}

/** 课程步骤数据 */
export interface CourseStep {
  readonly title: string
  readonly description: string
  readonly image: string
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
