export type IdentityApplicationStatus = 'submitted' | 'approved' | 'rejected'
export type SchoolPersonType = 'student' | 'teacher' | 'staff'
export type SchoolProfileStatus = 'active' | 'inactive'

export interface AdminSchoolProfileItem {
  profileId: string
  personType: SchoolPersonType
  name: string
  college: string
  className: string | null
  studentId: string | null
  staffId: string | null
  phoneMasked: string | null
  status: SchoolProfileStatus
  updatedAt: string
}

export interface AdminSchoolProfileDetail extends AdminSchoolProfileItem {
  phone: string | null
  createdAt: string
}

export interface SchoolProfilePayload {
  personType: SchoolPersonType
  name: string
  college: string
  className?: string | null
  studentId?: string | null
  staffId?: string | null
  phone?: string | null
  status: SchoolProfileStatus
}

export interface AdminSchoolProfileListResponse {
  items: AdminSchoolProfileItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface SchoolBindingPayload {
  personType: SchoolPersonType
  name: string
  college: string
  className: string | null
  studentId: string | null
  staffId: string | null
  phone: string | null
  note: string | null
}

export interface AdminSchoolBindingItem {
  applicationId: string
  openId: string
  applicantName: string
  college: string
  studentId: string | null
  staffId: string | null
  status: IdentityApplicationStatus
  schoolProfileId: number | null
  submittedAt: string
  updatedAt: string
  reviewedAt: string | null
}

export interface AdminSchoolBindingDetail extends AdminSchoolBindingItem {
  payload: SchoolBindingPayload
  reviewNote: string | null
  reviewedBy: string | null
}

export interface AdminSchoolBindingListResponse {
  items: AdminSchoolBindingItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApproveSchoolBindingPayload {
  schoolProfileId?: number | null
  newProfile?: SchoolProfilePayload | null
  reviewNote?: string | null
}

export interface RejectIdentityPayload {
  reviewNote: string
}

export interface AdminSchoolBindingReviewResponse {
  application: AdminSchoolBindingDetail
}

export interface ResponderClaimPayload {
  name: string
  idNumberLast6: string
  phone: string
  certificateNo: string | null
  note: string | null
}

export interface AdminResponderClaimItem {
  applicationId: string
  openId: string
  applicantName: string
  phoneMasked: string
  status: IdentityApplicationStatus
  responderProfileId: string | null
  submittedAt: string
  updatedAt: string
  reviewedAt: string | null
}

export interface AdminResponderClaimDetail extends AdminResponderClaimItem {
  payload: ResponderClaimPayload
  reviewNote: string | null
  reviewedBy: string | null
}

export interface AdminResponderClaimListResponse {
  items: AdminResponderClaimItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApproveResponderClaimPayload {
  responderProfileId: string
  reviewNote?: string | null
}

export interface AdminResponderClaimReviewResponse {
  application: AdminResponderClaimDetail
}
