export type VolunteerApplicationStatus = 'submitted' | 'approved' | 'rejected'

export interface VolunteerImageAttachment {
  url: string
  fileId: string | null
  name: string | null
}

export interface VolunteerApplicationPayload {
  idPhoto: VolunteerImageAttachment
  name: string
  gender: string
  birthDate: string
  phone: string
  email: string
  politicalStatus: string
  education: string
  occupation: string
  professionalTitle: string
  emergencyContactName: string
  emergencyContactPhone: string
  qualificationImages: VolunteerImageAttachment[]
  volunteerCertificateImages: VolunteerImageAttachment[]
  idNumber: string
  idCardFront: VolunteerImageAttachment
  idCardBack: VolunteerImageAttachment
  homeAddress: string
  personalResume: string
  joinReason: string
  commitmentAccepted: boolean
}

export interface AdminVolunteerApplicationItem {
  applicationId: string
  openId: string
  applicantName: string
  phoneMasked: string
  status: VolunteerApplicationStatus
  attemptNumber: number
  derivedFromId: number | null
  createdRescuerId: string | null
  submittedAt: string
  updatedAt: string
  reviewedAt: string | null
}

export interface AdminVolunteerApplicationDetail extends AdminVolunteerApplicationItem {
  payload: VolunteerApplicationPayload
  reviewNote: string | null
  reviewedBy: string | null
}

export interface AdminVolunteerApplicationListResponse {
  items: AdminVolunteerApplicationItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApproveVolunteerApplicationPayload {
  campus: 'guilinyang' | 'fucheng'
  lat: number
  lng: number
  reviewNote?: string | null
}

export interface RejectVolunteerApplicationPayload {
  reviewNote: string
}

export interface AdminVolunteerApplicationReviewResponse {
  application: AdminVolunteerApplicationDetail
  createdRescuerId: string | null
}
