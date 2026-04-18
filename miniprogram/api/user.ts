/**
 * 用户与档案相关 API
 *
 * 接口来源：CLAUDE.md §8.1 + BackEnd.md §4.6
 *
 * GET  /api/user/profile             — 获取用户信息 [登录]
 * PUT  /api/user/profile             — 更新用户信息 [登录]
 * GET  /api/user/archives            — 急救档案列表 [登录]
 * GET  /api/user/archives/:id/phone  — 查看完整电话 [登录 + 权限]
 */
import { get, put } from '@/services/request'
import type {
  UserProfile,
  UpdateUserProfilePayload,
  ArchiveListParams,
  ArchiveListResponse,
} from '@/types/user'

/** 获取用户信息 */
export function fetchUserProfile(): Promise<UserProfile> {
  return get<UserProfile>('/api/user/profile')
}

/** 更新用户信息（仅允许 name/avatarUrl/gender/phone） */
export function updateUserProfile(
  payload: UpdateUserProfilePayload,
): Promise<void> {
  return put('/api/user/profile', payload as unknown as Record<string, unknown>)
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

/**
 * 查看档案完整电话号码
 * 仅值班老师/管理员可调用，需填写用途，后端记录审计日志
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
