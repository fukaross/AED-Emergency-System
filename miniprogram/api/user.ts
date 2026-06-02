// 用户、档案、志愿者入队相关的接口
import { get, post, put } from '@/services/request'
import type {
  ArchiveDetail,
  UserProfile,
  UpdateUserProfilePayload,
  ArchiveListParams,
  ArchiveListResponse,
} from '@/types/user'
import type {
  VolunteerApplicationPayload,
  VolunteerApplicationResponse,
} from '@/types/volunteer'
import {
  canMockViewFullPhone,
  getMockArchiveDetail,
  getMockArchivePhone,
  loadMockUserProfile,
  queryMockArchives,
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
  if (USE_MOCK) {
    return Promise.resolve(queryMockArchives(params))
  }
  return get<ArchiveListResponse>(
    '/api/user/archives',
    params as unknown as Record<string, unknown>,
  )
}

/** 获取急救档案详情（含证书历史） */
export function fetchArchiveDetail(
  archiveId: string,
): Promise<ArchiveDetail> {
  if (USE_MOCK) {
    const detail = getMockArchiveDetail(archiveId)
    if (!detail) {
      return Promise.reject(new Error('档案不存在'))
    }

    return Promise.resolve(detail)
  }

  return get<ArchiveDetail>(`/api/user/archives/${archiveId}`)
}

/**
 * 查看档案完整电话 —— 只有值班老师/管理员能调，需要填用途
 */
export function fetchArchivePhone(
  archiveId: string,
  purpose: string,
): Promise<{ phone: string }> {
  if (USE_MOCK) {
    if (!purpose.trim()) {
      return Promise.reject(new Error('查看用途不能为空'))
    }

    if (!canMockViewFullPhone()) {
      return Promise.reject(new Error('当前角色无权查看完整号码'))
    }

    const phone = getMockArchivePhone(archiveId)
    if (!phone) {
      return Promise.reject(new Error('档案不存在'))
    }

    return Promise.resolve({ phone })
  }

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
