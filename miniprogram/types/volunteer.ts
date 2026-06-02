// 志愿者入队申请的类型
import type { Gender } from './user'

export type VolunteerApplicationStatus =
  | 'submitted'
  | 'approved'
  | 'rejected'

export interface VolunteerImageAttachment {
  readonly url: string
  readonly fileId?: string
  readonly name?: string
  readonly localPath?: string
}

export interface VolunteerApplicationPayload {
  readonly idPhoto: VolunteerImageAttachment
  readonly name: string
  readonly gender: Gender
  readonly birthDate: string
  readonly phone: string
  readonly email: string
  readonly politicalStatus: string
  readonly education: string
  readonly occupation: string
  readonly professionalTitle: string
  readonly emergencyContactName: string
  readonly emergencyContactPhone: string
  readonly qualificationImages: readonly VolunteerImageAttachment[]
  readonly volunteerCertificateImages: readonly VolunteerImageAttachment[]
  readonly idNumber: string
  readonly idCardFront: VolunteerImageAttachment
  readonly idCardBack: VolunteerImageAttachment
  readonly homeAddress: string
  readonly personalResume: string
  readonly joinReason: string
  readonly commitmentAccepted: boolean
}

export interface VolunteerApplicationResponse {
  readonly applicationId: string
  readonly status: VolunteerApplicationStatus
  readonly submittedAt: string
}
