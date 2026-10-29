// demo 合成数据的只读类型契约。
//
// 数据真源是仓库根 demo/（demo-v1），由 scripts/sync-demo-data.mjs 生成到
// ./generated/ 下。本文件只声明生成产物的形状，不保存任何数据，也不是第二份档案。
// 生成器会校验引用关系；字段不匹配会在 pnpm typecheck 阶段失败。
import type { CampusCode } from '@/config/site-config'

export type DemoGender = 'male' | 'female'

export type DemoAedStatus = 'online' | 'offline'

export interface DemoAedDevice {
  readonly id: string
  readonly name: string
  readonly lat: number
  readonly lng: number
  readonly status: DemoAedStatus
  readonly statusNote: string | null
  readonly image: string
  readonly address: string
  readonly lastMaintenance: string
  readonly nextMaintenance: string
  readonly maintainer: string
}

export type DemoRescuerLevel = 'honor' | 'trainee' | 'official' | 'core'
export type DemoResponderStatus = 'active' | 'inactive'

export interface DemoRescuerProfile {
  readonly id: string
  readonly recordId: string
  readonly name: string
  readonly gender: DemoGender
  readonly birthDate: string
  readonly phone: string
  readonly idNumber: string
  readonly phoneMasked: boolean
  readonly idMasked: boolean
  readonly avatarUrl: string
  readonly campus: CampusCode
  readonly level: DemoRescuerLevel
  readonly qualificationStatus: DemoResponderStatus
  readonly lat: number
  readonly lng: number
  readonly certificateNo: string
  readonly certificateNoMasked: string
  readonly issuingAuthority: string
  readonly issueDate: string
  readonly workUnit: string
  readonly skillCpr: boolean
  readonly skillAed: boolean
  readonly skillStb: boolean
  readonly trainingYear: number
  readonly trainingProject: string
  readonly trainingSessionName: string
  readonly theoryScore: number
  readonly traumaAssessmentResult: string
  readonly cprAssessmentResult: string
  readonly aedAssessmentResult: string
  readonly cprAedCombinedResult: string
  readonly sourceFile: string
  readonly sourceSheet: string
  readonly sourceRowNo: number
  readonly sourceSeqNo: string
  readonly remark: string
  readonly boundOpenId: string | null
  readonly source: string
  readonly sourceApplicationId: string | null
}

export interface DemoCourseStep {
  readonly order: number
  readonly title: string
  readonly description: string
  readonly image: string
  readonly keyPoints: readonly string[]
}

export interface DemoCourse {
  readonly id: string
  readonly title: string
  readonly thumbnail: string
  readonly type: 'article' | 'video'
  readonly category: string
  readonly content: string
  readonly videoUrl: string | null
  readonly videos: readonly {
    readonly order: number
    readonly title: string
    readonly url: string
  }[]
  readonly disclaimer: string
  readonly pinned: boolean
  readonly sortOrder: number
  readonly isEnabled: boolean
  readonly cacheVersion: string
  readonly estimatedTime: string
  readonly viewCount: number
  readonly createdAt: string
  readonly steps: readonly DemoCourseStep[]
}

export interface DemoQuickAction {
  readonly id: string
  readonly title: string
  readonly icon: string
  readonly summary: string
  readonly sortOrder: number
  readonly isEnabled: boolean
  readonly steps: readonly DemoCourseStep[]
}

export interface DemoKnowledgeSummaryItem {
  readonly key: string
  readonly title: string
  readonly value: number
  readonly sortOrder: number
}

export interface DemoKnowledgeSummary {
  readonly items: readonly DemoKnowledgeSummaryItem[]
  readonly updatedAt: string
}

export type DemoPrimaryRole = 'student' | 'teacher' | 'staff'

export type DemoAccountStatus = 'active' | 'disabled'

export interface DemoUser {
  readonly openId: string
  readonly name: string
  readonly avatarUrl: string
  readonly gender: DemoGender | 'unknown'
  readonly phone: string
  readonly primaryRole: DemoPrimaryRole
  readonly accountStatus: DemoAccountStatus
}

export interface DemoSchoolBinding {
  readonly personType: DemoPrimaryRole
  readonly name: string
  readonly college: string
  readonly className: string | null
  readonly studentId: string | null
  readonly staffId: string | null
  readonly phone: string
  readonly note: string
}

export type DemoRoleCode = 'requester' | 'active-rescuer' | 'unqualified'

export interface DemoRole {
  readonly role: DemoRoleCode
  readonly loginCode: string
  readonly openId: string
  readonly displayName: string
  readonly campus: CampusCode
  readonly primaryRole: DemoPrimaryRole
  readonly accountStatus: DemoAccountStatus
  readonly responderProfileId: string | null
  readonly qualificationStatus: DemoResponderStatus | null
  readonly expectedCertifiedResponder: boolean
  readonly schoolBinding: DemoSchoolBinding
}
