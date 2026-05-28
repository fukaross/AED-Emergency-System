export interface RescuerForm {
  name: string
  avatarUrl: string
  phone: string
  campus: string
  level: string | null
  qualificationStatus: string
  lat: number
  lng: number
  certificateNo: string | null
  issuingAuthority: string | null
  issueDate: string | null
  workUnit: string | null
  skillCpr: boolean
  skillAed: boolean
  skillStb: boolean
  idNumber: string
  cprAssessmentResult: string | null
  aedAssessmentResult: string | null
  traumaAssessmentResult: string | null
  cprAedCombinedResult: string | null
}
