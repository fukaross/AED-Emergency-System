export interface AdminRescuerItem {
  id: string
  recordId: string
  name: string
  avatarUrl: string
  /** 校区 code 来自 config/site.json 的 campuses[]，由后端按同一配置校验，不是源码枚举。 */
  campus: string
  level: 'honor' | 'trainee' | 'official' | 'core' | null
  qualificationStatus: 'active' | 'inactive'
  lat: number
  lng: number
  certificateNoMasked: string | null
  workUnit: string | null
  phoneMasked: string
  skillCpr: boolean
  skillAed: boolean
  skillStb: boolean
}

export interface AdminRescuerDetail extends AdminRescuerItem {
  gender: string
  birthDate: string
  phone: string
  idNumber: string
  idNumberMasked: string
  phoneMaskedFlag: boolean
  idMaskedFlag: boolean
  certificateNo: string | null
  issuingAuthority: string | null
  issueDate: string | null
  trainingYear: number
  trainingProject: string | null
  trainingSessionName: string | null
  theoryScore: number
  traumaAssessmentResult: string | null
  cprAssessmentResult: string | null
  aedAssessmentResult: string | null
  cprAedCombinedResult: string | null
  sourceFile: string | null
  sourceSheet: string | null
  sourceRowNo: number
  sourceSeqNo: string | null
  remark: string | null
  boundOpenId: string | null
  boundUserName: string | null
  source: 'seed' | 'volunteer_application' | 'admin_manual' | string
  sourceApplicationId: number | null
}

export interface AdminRescuerListResponse {
  items: AdminRescuerItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface UpdateRescuerPayload {
  name?: string
  avatarUrl?: string
  phone?: string
  campus?: string
  level?: string | null
  qualificationStatus?: string
  lat?: number
  lng?: number
  certificateNo?: string | null
  issuingAuthority?: string | null
  issueDate?: string | null
  workUnit?: string | null
  skillCpr?: boolean
  skillAed?: boolean
  skillStb?: boolean
  idNumber?: string
  cprAssessmentResult?: string | null
  aedAssessmentResult?: string | null
  traumaAssessmentResult?: string | null
  cprAedCombinedResult?: string | null
}
