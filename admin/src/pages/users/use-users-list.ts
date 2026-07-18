import { onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  useRoute,
  useRouter,
  type LocationQueryRaw,
  type RouteLocationNormalizedLoaded,
  type Router,
} from 'vue-router'
import { getUsers } from '@/api/users'
import { getApiErrorMessage } from '@/api/client'
import type { AdminUserListItem } from '@/types/user'
import { createRequestSequencer, type RequestTicket } from '@/utils/request-sequencer'
import { logApiError } from '@/utils/safe-api-error'

const REFRESH_INTERVAL_MS = 10_000

export class UsersListController {
  readonly loading = ref(false)
  readonly tableData = ref<AdminUserListItem[]>([])
  readonly totalItems = ref(0)
  readonly pageSize = ref(10)
  readonly filterRole = ref('')
  readonly filterStatus = ref('')
  readonly searchKeyword = ref('')
  readonly currentPage = ref(1)
  private readonly requestSequencer = createRequestSequencer()
  private refreshTimer: number | null = null
  private abortController: AbortController | null = null

  constructor(
    private readonly route: RouteLocationNormalizedLoaded,
    private readonly router: Router,
  ) {}

  private syncFromRoute() {
    this.filterRole.value = (this.route.query.role as string) || ''
    this.filterStatus.value = (this.route.query.status as string) || ''
    this.searchKeyword.value = (this.route.query.keyword as string) || ''
    this.currentPage.value = Number(this.route.query.page) || 1
  }

  private updateRouteQuery(resetPage: boolean) {
    const query: LocationQueryRaw = {}
    if (this.filterRole.value) query.role = this.filterRole.value
    if (this.filterStatus.value) query.status = this.filterStatus.value
    if (this.searchKeyword.value) query.keyword = this.searchKeyword.value
    query.page = resetPage ? 1 : this.currentPage.value
    void this.router.push({ path: this.route.path, query })
  }

  handleFilterChange() {
    this.updateRouteQuery(true)
  }

  handlePageChange(page: number) {
    this.currentPage.value = page
    this.updateRouteQuery(false)
  }

  async loadData(silent = false) {
    this.loading.value = !silent
    const ticket = this.beginRequest()
    const controller = this.abortController
    if (!controller) return
    try {
      const response = await getUsers({
        page: this.currentPage.value,
        pageSize: this.pageSize.value,
        keyword: this.searchKeyword.value || undefined,
        accountStatus: this.filterStatus.value || undefined,
        primaryRole: this.filterRole.value || undefined,
      }, controller.signal)
      if (!this.canApply(ticket, controller)) return
      this.tableData.value = response.data.data.items
      this.totalItems.value = response.data.data.total
    } catch (error: unknown) {
      if (!this.canApply(ticket, controller)) return
      const fallback = '获取用户列表数据失败'
      logApiError({ context: 'users.list', error, fallback })
      if (!silent) ElMessage.error(getApiErrorMessage(error, fallback))
    } finally {
      this.finishRequest(ticket, controller)
    }
  }

  private beginRequest(): RequestTicket {
    this.abortController?.abort()
    const ticket = this.requestSequencer.next()
    this.abortController = new AbortController()
    return ticket
  }

  private canApply(ticket: RequestTicket, controller: AbortController): boolean {
    return !controller.signal.aborted && this.requestSequencer.isCurrent(ticket)
  }

  private finishRequest(ticket: RequestTicket, controller: AbortController): void {
    if (!this.requestSequencer.isCurrent(ticket)) return
    if (this.abortController === controller) this.abortController = null
    this.loading.value = false
  }

  private cancelPendingRequest(): void {
    this.requestSequencer.invalidate()
    this.abortController?.abort()
    this.abortController = null
    this.loading.value = false
  }

  private stopPolling() {
    if (this.refreshTimer === null) return
    window.clearInterval(this.refreshTimer)
    this.refreshTimer = null
  }

  private refreshNow() {
    if (!document.hidden) void this.loadData(true)
  }

  private startPolling() {
    this.stopPolling()
    this.refreshTimer = window.setInterval(() => this.refreshNow(), REFRESH_INTERVAL_MS)
  }

  private readonly handleVisibilityChange = () => {
    if (document.hidden) {
      this.stopPolling()
      this.cancelPendingRequest()
      return
    }
    void this.loadData(true)
    this.startPolling()
  }

  private refreshFromRoute() {
    this.syncFromRoute()
    void this.loadData()
  }

  registerLifecycle() {
    watch(() => this.route.query, () => this.refreshFromRoute())
    onMounted(() => {
      this.refreshFromRoute()
      this.startPolling()
      document.addEventListener('visibilitychange', this.handleVisibilityChange)
    })
    onUnmounted(() => {
      this.stopPolling()
      this.cancelPendingRequest()
      document.removeEventListener('visibilitychange', this.handleVisibilityChange)
    })
  }

  viewDetail(openId: string) {
    void this.router.push(`/users/${openId}`)
  }

  createBindings() {
    return {
      currentPage: this.currentPage,
      filterRole: this.filterRole,
      filterStatus: this.filterStatus,
      handleFilterChange: () => this.handleFilterChange(),
      handlePageChange: (page: number) => this.handlePageChange(page),
      loading: this.loading,
      pageSize: this.pageSize,
      searchKeyword: this.searchKeyword,
      tableData: this.tableData,
      totalItems: this.totalItems,
      viewDetail: (openId: string) => this.viewDetail(openId),
    }
  }
}

/** 管理用户列表的筛选路由同步、加载与可见性轮询。 */
export function useUsersList() {
  const controller = new UsersListController(useRoute(), useRouter())
  controller.registerLifecycle()
  return controller.createBindings()
}
