import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalizedLoaded, Router } from 'vue-router'
import { getUsers } from '@/api/users'
import type { AdminUserListItem } from '@/types/user'
import { UsersListController } from './use-users-list'

vi.mock('@/api/users', () => ({ getUsers: vi.fn() }))
vi.mock('@/api/client', () => ({
  getApiErrorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/utils/safe-api-error', () => ({ logApiError: vi.fn() }))
vi.mock('element-plus', () => ({ ElMessage: { error: vi.fn() } }))

function createController() {
  const route = { path: '/users', query: {} } as RouteLocationNormalizedLoaded
  const push = vi.fn()
  const router = { push } as unknown as Router
  return { controller: new UsersListController(route, router), push }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

function buildUser(openId: string): AdminUserListItem {
  return {
    openId,
    name: openId,
    avatarUrl: null,
    phone: null,
    primaryRole: 'student',
    accountStatus: 'active',
    responderStatus: null,
    certified: false,
    level: null,
    adminRemark: null,
    updatedAt: null,
  }
}

function buildResponse(openId: string) {
  return {
    data: {
      data: {
        items: [buildUser(openId)],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
      },
    },
  }
}

beforeEach(() => {
  vi.clearAllMocks()
})

describe('UsersListController navigation', () => {
  it('resets the page for filter changes and preserves it for pagination', () => {
    const { controller, push } = createController()
    const bindings = controller.createBindings()
    bindings.filterRole.value = 'student'
    bindings.filterStatus.value = 'active'
    bindings.searchKeyword.value = '张三'

    bindings.handleFilterChange()
    expect(push).toHaveBeenLastCalledWith({
      path: '/users',
      query: { role: 'student', status: 'active', keyword: '张三', page: 1 },
    })

    bindings.handlePageChange(3)
    expect(push).toHaveBeenLastCalledWith({
      path: '/users',
      query: { role: 'student', status: 'active', keyword: '张三', page: 3 },
    })
  })

  it('keeps the existing user detail route contract', () => {
    const { controller, push } = createController()
    controller.createBindings().viewDetail('openid-1')
    expect(push).toHaveBeenCalledWith('/users/openid-1')
  })
})

describe('UsersListController request ordering', () => {
  it('keeps the newest page when an older request finishes last', async () => {
    const first = deferred<ReturnType<typeof buildResponse>>()
    const second = deferred<ReturnType<typeof buildResponse>>()
    const signals: AbortSignal[] = []
    vi.mocked(getUsers).mockImplementation((_params, signal) => {
      if (signal) signals.push(signal)
      const result = signals.length === 1 ? first.promise : second.promise
      return result as ReturnType<typeof getUsers>
    })
    const { controller } = createController()

    const firstLoad = controller.loadData()
    controller.currentPage.value = 2
    const secondLoad = controller.loadData()
    expect(signals[0].aborted).toBe(true)
    second.resolve(buildResponse('new-page-user'))
    await secondLoad
    first.resolve(buildResponse('old-page-user'))
    await firstLoad

    expect(controller.tableData.value.map((item) => item.openId)).toEqual(['new-page-user'])
  })
})
