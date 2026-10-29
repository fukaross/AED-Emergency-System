import type {
  IdentityApplicationListResponse,
  IdentityApplicationResponse,
  ResponderClaimDetail,
  SchoolBindingDetail,
  SubmitResponderClaimPayload,
  SubmitSchoolBindingPayload,
} from '@/types/user'

const MOCK_SCHOOL_BINDING_STORAGE_KEY = 'aed_mock_school_binding'
const MOCK_RESPONDER_CLAIM_STORAGE_KEY = 'aed_mock_responder_claim'

export function saveMockSchoolBinding(
  payload: SubmitSchoolBindingPayload,
): IdentityApplicationResponse {
  const response = createMockIdentityResponse('school')
  writeMockIdentity(MOCK_SCHOOL_BINDING_STORAGE_KEY, payload, response)
  return response
}

export function queryMockSchoolBindings(): IdentityApplicationListResponse<SchoolBindingDetail> {
  const stored = readMockIdentity<SubmitSchoolBindingPayload, IdentityApplicationResponse>(
    MOCK_SCHOOL_BINDING_STORAGE_KEY,
  )
  const items = stored ? [toMockSchoolBinding(stored.payload, stored.response)] : []
  return toIdentityPage(items)
}

export function saveMockResponderClaim(
  payload: SubmitResponderClaimPayload,
): IdentityApplicationResponse {
  const response = createMockIdentityResponse('claim')
  writeMockIdentity(MOCK_RESPONDER_CLAIM_STORAGE_KEY, payload, response)
  return response
}

export function queryMockResponderClaims(): IdentityApplicationListResponse<ResponderClaimDetail> {
  const stored = readMockIdentity<SubmitResponderClaimPayload, IdentityApplicationResponse>(
    MOCK_RESPONDER_CLAIM_STORAGE_KEY,
  )
  const items = stored ? [toMockResponderClaim(stored.payload, stored.response)] : []
  return toIdentityPage(items)
}

function createMockIdentityResponse(prefix: string): IdentityApplicationResponse {
  return {
    applicationId: `${prefix}-${Date.now()}`,
    status: 'submitted',
    submittedAt: new Date().toISOString(),
  }
}

function writeMockIdentity<TPayload>(
  key: string,
  payload: TPayload,
  response: IdentityApplicationResponse,
): void {
  try {
    uni.setStorageSync(key, JSON.stringify({ payload, response }))
  } catch {
    console.warn('[Mock User API] 保存身份申请失败')
  }
}

function readMockIdentity<TPayload, TResponse>(key: string): {
  readonly payload: TPayload
  readonly response: TResponse
} | null {
  try {
    const stored = uni.getStorageSync(key) as string
    return stored ? JSON.parse(stored) as { payload: TPayload; response: TResponse } : null
  } catch {
    return null
  }
}

function toMockSchoolBinding(
  payload: SubmitSchoolBindingPayload,
  response: IdentityApplicationResponse,
): SchoolBindingDetail {
  return {
    applicationId: response.applicationId,
    status: response.status,
    schoolProfileId: null,
    submittedAt: response.submittedAt,
    updatedAt: response.submittedAt,
    reviewNote: null,
    reviewedAt: null,
    payload: {
      personType: payload.personType,
      name: payload.name,
      college: payload.college,
      className: payload.className ?? null,
      studentId: payload.studentId ?? null,
      staffId: payload.staffId ?? null,
      phone: payload.phone ?? null,
      note: payload.note ?? null,
    },
  }
}

function toMockResponderClaim(
  payload: SubmitResponderClaimPayload,
  response: IdentityApplicationResponse,
): ResponderClaimDetail {
  return {
    applicationId: response.applicationId,
    status: response.status,
    responderProfileId: null,
    submittedAt: response.submittedAt,
    updatedAt: response.submittedAt,
    reviewNote: null,
    reviewedAt: null,
    payload: {
      name: payload.name,
      idNumberLast6: payload.idNumberLast6,
      phone: payload.phone,
      certificateNo: payload.certificateNo ?? null,
      note: payload.note ?? null,
    },
  }
}

function toIdentityPage<T>(items: readonly T[]): IdentityApplicationListResponse<T> {
  return {
    items,
    total: items.length,
    page: 1,
    pageSize: 20,
    totalPages: items.length ? 1 : 0,
  }
}
