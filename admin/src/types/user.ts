export type PrimaryRole = 'student' | 'teacher' | 'staff' | 'admin'
export type AccountStatus = 'active' | 'disabled'
export type ResponderStatus = 'active' | 'inactive'
export type RescuerLevel = 'honor' | 'trainee' | 'official' | 'core'

export interface AdminUserListItem {
  openId: string
  name: string
  avatarUrl: string | null
  phone: string | null
  primaryRole: PrimaryRole
  accountStatus: AccountStatus
  responderStatus: ResponderStatus | null
  certified: boolean
  level: RescuerLevel | null
  adminRemark: string | null
  updatedAt: string | null
}

export interface AdminUserListResponse {
  items: AdminUserListItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface SchoolProfile {
  college: string
  studentId?: string
  staffId?: string
  bindingStatus: 'pending' | 'active' | 'rejected' | 'replaced' | 'unbound'
}

export interface RescuerSummary {
  archiveProfileId: string
  responderProfileId: string
  certified: boolean
  level: RescuerLevel | null
  responderType: 'rescuer' | 'duty_teacher'
  responderStatus: ResponderStatus
  certExpiry?: string | null
  currentCertificateRecordId?: string | null
  currentCertificateNo?: string | null
  workUnit?: string | null
}

export interface AdminUserDetail extends AdminUserListItem {
  schoolProfile: SchoolProfile | null
  rescuerSummary: RescuerSummary | null
  subscriptions: Record<string, unknown> | null
  createdAt: string | null
  lastLoginAt: string | null
}

export interface AdminUserUpdatePayload {
  primaryRole?: PrimaryRole
  accountStatus?: AccountStatus
  adminRemark?: string | null
}
