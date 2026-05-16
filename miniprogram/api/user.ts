/**
 * 用户与档案相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.6
 *
 * GET  /api/user/profile             — 获取用户信息 [登录]
 * PUT  /api/user/profile             — 更新用户信息 [登录]
 * GET  /api/user/archives            — 急救档案列表 [登录]
 * GET  /api/user/archives/:id        — 急救档案详情 [登录]
 * GET  /api/user/archives/:id/phone  — 查看完整电话 [登录 + 权限]
 * POST /api/user/volunteer-applications — 提交志愿者入队申请 [登录]
 */
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
import { USE_MOCK } from '@/utils/runtime-env'

/** 获取用户信息 */
export function fetchUserProfile(): Promise<UserProfile> {
  if (USE_MOCK) {
    return Promise.resolve(loadMockUserProfile())
  }
  return get<UserProfile>('/api/user/profile')
}

/** 更新用户信息（仅允许 name/avatarUrl/gender/phone） */
export function updateUserProfile(
  payload: UpdateUserProfilePayload,
): Promise<void> {
  if (USE_MOCK) {
    saveMockUserProfile(payload)
    return Promise.resolve()
  }
  return put('/api/user/profile', payload as unknown as Record<string, unknown>)
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
 * 查看档案完整电话号码
 * 仅值班老师/管理员可调用，需填写用途，后端记录审计日志
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
  if (USE_MOCK) {
    return Promise.resolve(saveMockVolunteerApplication(payload))
  }

  return post<VolunteerApplicationResponse>(
    '/api/user/volunteer-applications',
    payload as unknown as Record<string, unknown>,
  )
}
