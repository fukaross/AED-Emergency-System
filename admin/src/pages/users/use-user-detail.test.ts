import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessage } from 'element-plus'
import { getUserDetail, updateUser } from '@/api/users'
import type { AdminUserDetail } from '@/types/user'
import { buildUserUpdatePayload, useUserDetail } from './use-user-detail'

vi.mock('@/api/users', () => ({
  getUserDetail: vi.fn(),
  updateUser: vi.fn(),
}))
vi.mock('@/api/client', () => ({
  getApiErrorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/utils/safe-api-error', () => ({ logApiError: vi.fn() }))
vi.mock('element-plus', () => ({
  ElMessage: {
    error: vi.fn(),
    success: vi.fn(),
    warning: vi.fn(),
  },
}))

const getUserDetailMock = vi.mocked(getUserDetail)
const updateUserMock = vi.mocked(updateUser)

function buildDetail(overrides: Partial<AdminUserDetail> = {}): AdminUserDetail {
  return {
    openId: 'openid-1',
    name: '测试用户',
    avatarUrl: null,
    phone: '13812345678',
    primaryRole: 'student',
    accountStatus: 'active',
    responderStatus: null,
    certified: false,
    level: null,
    adminRemark: null,
    updatedAt: null,
    schoolProfile: null,
    rescuerSummary: null,
    subscriptions: null,
    createdAt: '2026-07-18T08:00:00Z',
    lastLoginAt: null,
    ...overrides,
  }
}

function detailResponse(detail: AdminUserDetail) {
  return { data: { data: detail } } as Awaited<ReturnType<typeof getUserDetail>>
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => { resolve = resolvePromise })
  return { promise, resolve }
}

beforeEach(() => {
  getUserDetailMock.mockReset()
  updateUserMock.mockReset()
  vi.mocked(ElMessage.error).mockReset()
  vi.mocked(ElMessage.success).mockReset()
  vi.mocked(ElMessage.warning).mockReset()
})

describe('buildUserUpdatePayload', () => {
  it('returns only fields changed by the administrator', () => {
    const current = buildDetail()
    const payload = buildUserUpdatePayload({
      primaryRole: 'teacher',
      accountStatus: 'active',
      adminRemark: '',
    }, current)

    expect(payload).toEqual({ primaryRole: 'teacher' })
    expect(current.primaryRole).toBe('student')
  })
})

describe('useUserDetail', () => {
  it('loads detail and synchronizes the edit form', async () => {
    getUserDetailMock.mockResolvedValue(detailResponse(buildDetail({ adminRemark: '关注用户' })))
    const state = useUserDetail({ openId: 'openid-1' })

    await state.loadDetail()

    expect(state.detail.value?.name).toBe('测试用户')
    expect(state.editForm.adminRemark).toBe('关注用户')
    expect(state.loading.value).toBe(false)
  })

  it('preserves dirty edits during a silent refresh', async () => {
    getUserDetailMock
      .mockResolvedValueOnce(detailResponse(buildDetail()))
      .mockResolvedValueOnce(detailResponse(buildDetail({ primaryRole: 'staff' })))
    const state = useUserDetail({ openId: 'openid-1' })
    await state.loadDetail()
    state.editForm.primaryRole = 'teacher'

    await state.loadDetail(false, true)

    expect(state.detail.value?.primaryRole).toBe('staff')
    expect(state.editForm.primaryRole).toBe('teacher')
    expect(state.isEditDirty()).toBe(true)
  })

  it('preserves edits made while a detail refresh is in flight', async () => {
    const pending = deferred<Awaited<ReturnType<typeof getUserDetail>>>()
    getUserDetailMock
      .mockResolvedValueOnce(detailResponse(buildDetail()))
      .mockImplementationOnce(() => pending.promise)
    const state = useUserDetail({ openId: 'openid-1' })
    await state.loadDetail()

    const refresh = state.loadDetail(true, true)
    state.editForm.primaryRole = 'teacher'
    pending.resolve(detailResponse(buildDetail({ primaryRole: 'staff' })))
    await refresh

    expect(state.detail.value?.primaryRole).toBe('staff')
    expect(state.editForm.primaryRole).toBe('teacher')
  })

  it('aborts an older request and ignores its late response', async () => {
    const older = deferred<Awaited<ReturnType<typeof getUserDetail>>>()
    const newer = deferred<Awaited<ReturnType<typeof getUserDetail>>>()
    getUserDetailMock
      .mockImplementationOnce(() => older.promise)
      .mockImplementationOnce(() => newer.promise)
    const state = useUserDetail({ openId: 'openid-1' })

    const olderLoad = state.loadDetail()
    const olderSignal = getUserDetailMock.mock.calls[0]?.[1]
    const newerLoad = state.loadDetail()
    expect(olderSignal?.aborted).toBe(true)
    newer.resolve(detailResponse(buildDetail({ name: '较新详情' })))
    await newerLoad
    older.resolve(detailResponse(buildDetail({ name: '过期详情' })))
    await olderLoad

    expect(state.detail.value?.name).toBe('较新详情')
  })

  it('aborts and ignores a late response after destroy', async () => {
    const pending = deferred<Awaited<ReturnType<typeof getUserDetail>>>()
    getUserDetailMock.mockImplementationOnce(() => pending.promise)
    const state = useUserDetail({ openId: 'openid-1' })

    const load = state.loadDetail()
    const signal = getUserDetailMock.mock.calls[0]?.[1]
    state.destroy()
    expect(signal?.aborted).toBe(true)
    pending.resolve(detailResponse(buildDetail()))
    await load

    expect(state.detail.value).toBeNull()
  })
})

describe('useUserDetail save', () => {
  it('submits a delta payload and refreshes the detail', async () => {
    getUserDetailMock
      .mockResolvedValueOnce(detailResponse(buildDetail()))
      .mockResolvedValueOnce(detailResponse(buildDetail({ accountStatus: 'disabled' })))
    updateUserMock.mockResolvedValue({
      data: { code: 0, message: 'ok', data: null },
    } as Awaited<ReturnType<typeof updateUser>>)
    const state = useUserDetail({ openId: 'openid-1' })
    await state.loadDetail()
    state.editForm.accountStatus = 'disabled'

    await state.handleSave()

    expect(updateUserMock).toHaveBeenCalledWith('openid-1', { accountStatus: 'disabled' })
    expect(state.detail.value?.accountStatus).toBe('disabled')
    expect(ElMessage.success).toHaveBeenCalledWith('保存修改成功')
  })

  it('does not call the update endpoint when nothing changed', async () => {
    getUserDetailMock.mockResolvedValue(detailResponse(buildDetail()))
    const state = useUserDetail({ openId: 'openid-1' })
    await state.loadDetail()

    await state.handleSave()

    expect(updateUserMock).not.toHaveBeenCalled()
    expect(ElMessage.warning).toHaveBeenCalledWith('内容未做任何修改')
  })
})
