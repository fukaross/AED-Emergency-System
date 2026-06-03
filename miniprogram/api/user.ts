// 用户、档案、志愿者入队相关的接口
import { get, post, put } from '@/services/request'
import type {
  ArchiveDetail,
  UserProfile,
  UpdateUserProfilePayload,
  ArchiveListParams,
  ArchiveListResponse,
  IdentityApplicationListResponse,
  IdentityApplicationResponse,
  ResponderClaimDetail,
  SchoolBindingDetail,
  SubmitResponderClaimPayload,
  SubmitSchoolBindingPayload,
} from '@/types/user'
import type {
  VolunteerApplicationPayload,
  VolunteerApplicationResponse,
} from '@/types/volunteer'
import {
  loadMockUserProfile,
  queryMockResponderClaims,
  queryMockSchoolBindings,
  saveMockResponderClaim,
  saveMockSchoolBinding,
  saveMockVolunteerApplication,
  saveMockUserProfile,
} from '@/mock/user'
import { USER_BACKEND_READY, USE_MOCK } from '@/utils/runtime-env'

const USE_USER_MOCK = !USER_BACKEND_READY && USE_MOCK

function toProfileUpdateBody(
  payload: UpdateUserProfilePayload,
): Record<string, string | undefined> {
  return {
    name: payload.name,
    avatarUrl: payload.avatarUrl,
    gender: payload.gender,
    phone: payload.phone,
  }
}

/** 获取用户信息 */
export function fetchUserProfile(): Promise<UserProfile> {
  if (USE_USER_MOCK) {
    return Promise.resolve(loadMockUserProfile())
  }
  return get<UserProfile>('/api/user/profile')
}

/** 更新用户信息（仅允许 name/avatarUrl/gender/phone） */
export function updateUserProfile(
  payload: UpdateUserProfilePayload,
): Promise<void> {
  if (USE_USER_MOCK) {
    saveMockUserProfile(payload)
    return Promise.resolve()
  }
  return put('/api/user/profile', toProfileUpdateBody(payload))
}

/** 获取急救档案列表 */
export function fetchArchives(
  params?: ArchiveListParams,
): Promise<ArchiveListResponse> {
  return get<ArchiveListResponse>(
    '/api/user/archives',
    params as unknown as Record<string, unknown>,
  )
}

/** 获取急救档案详情（含证书历史） */
export function fetchArchiveDetail(
  archiveId: string,
): Promise<ArchiveDetail> {
  return get<ArchiveDetail>(`/api/user/archives/${archiveId}`)
}

/**
 * 查看档案完整电话 —— 登录用户紧急联络场景使用，需要填用途
 */
export function fetchArchivePhone(
  archiveId: string,
  purpose: string,
): Promise<{ phone: string }> {
  return get<{ phone: string }>(
    `/api/user/archives/${archiveId}/phone`,
    { purpose },
  )
}

/** 提交校园 AED 急救志愿者入队申请 */
export function submitVolunteerApplication(
  payload: VolunteerApplicationPayload,
): Promise<VolunteerApplicationResponse> {
  if (USE_USER_MOCK) {
    return Promise.resolve(saveMockVolunteerApplication(payload))
  }

  return post<VolunteerApplicationResponse>(
    '/api/user/volunteer-applications',
    payload as unknown as Record<string, unknown>,
  )
}

export function submitSchoolBinding(
  payload: SubmitSchoolBindingPayload,
): Promise<IdentityApplicationResponse> {
  if (USE_USER_MOCK) {
    return Promise.resolve(saveMockSchoolBinding(payload))
  }

  return post<IdentityApplicationResponse>(
    '/api/user/school-bindings',
    payload as unknown as Record<string, unknown>,
  )
}

export function fetchSchoolBindings(): Promise<
  IdentityApplicationListResponse<SchoolBindingDetail>
> {
  if (USE_USER_MOCK) {
    return Promise.resolve(queryMockSchoolBindings())
  }

  return get<IdentityApplicationListResponse<SchoolBindingDetail>>(
    '/api/user/school-bindings',
  )
}

export function submitResponderClaim(
  payload: SubmitResponderClaimPayload,
): Promise<IdentityApplicationResponse> {
  if (USE_USER_MOCK) {
    return Promise.resolve(saveMockResponderClaim(payload))
  }

  return post<IdentityApplicationResponse>(
    '/api/user/responder-claims',
    payload as unknown as Record<string, unknown>,
  )
}

export function fetchResponderClaims(): Promise<
  IdentityApplicationListResponse<ResponderClaimDetail>
> {
  if (USE_USER_MOCK) {
    return Promise.resolve(queryMockResponderClaims())
  }

  return get<IdentityApplicationListResponse<ResponderClaimDetail>>(
    '/api/user/responder-claims',
  )
}
