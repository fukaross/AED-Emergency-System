import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getUserDetail, updateUser } from '@/api/users'
import { getApiErrorMessage } from '@/api/client'
import { logApiError } from '@/utils/safe-api-error'
import type {
  AccountStatus,
  AdminUserDetail,
  AdminUserUpdatePayload,
  PrimaryRole,
} from '@/types/user'

const DEFAULT_REFRESH_INTERVAL_MS = 10_000

export interface UserEditForm {
  primaryRole: PrimaryRole
  accountStatus: AccountStatus
  adminRemark: string
}

interface UseUserDetailOptions {
  openId: string
  refreshIntervalMs?: number
}

interface DetailLoadOptions {
  readonly syncForm: boolean
  readonly silent: boolean
  readonly preserveDirty: boolean
}

/** Builds the minimal update contract without mutating the form or current detail. */
export function buildUserUpdatePayload(
  form: Readonly<UserEditForm>,
  current: Readonly<AdminUserDetail>,
): AdminUserUpdatePayload {
  const payload: AdminUserUpdatePayload = {}
  if (form.primaryRole !== current.primaryRole) {
    payload.primaryRole = form.primaryRole
  }
  if (form.accountStatus !== current.accountStatus) {
    payload.accountStatus = form.accountStatus
  }
  if (form.adminRemark !== (current.adminRemark || '')) {
    payload.adminRemark = form.adminRemark
  }
  return payload
}

class UserDetailController {
  readonly loading = ref(true)
  readonly saveLoading = ref(false)
  readonly detail = ref<AdminUserDetail | null>(null)
  readonly editForm = reactive<UserEditForm>({
    primaryRole: 'student',
    accountStatus: 'active',
    adminRemark: '',
  })
  private readonly refreshIntervalMs: number
  private readonly visibilityHandler = () => this.handleVisibilityChange()
  private refreshTimer: number | null = null
  private activeRequest: AbortController | null = null
  private requestGeneration = 0
  private destroyed = false
  private listeningForVisibility = false

  constructor(private readonly options: UseUserDetailOptions) {
    this.refreshIntervalMs = options.refreshIntervalMs ?? DEFAULT_REFRESH_INTERVAL_MS
  }

  syncEditForm(data: AdminUserDetail) {
    this.editForm.primaryRole = data.primaryRole
    this.editForm.accountStatus = data.accountStatus
    this.editForm.adminRemark = data.adminRemark || ''
  }

  isEditDirty() {
    if (!this.detail.value) return false
    return Object.keys(buildUserUpdatePayload(this.editForm, this.detail.value)).length > 0
  }

  private cancelActiveRequest() {
    this.requestGeneration += 1
    this.activeRequest?.abort()
    this.activeRequest = null
  }

  private isStale(ticket: number): boolean {
    return this.destroyed || ticket !== this.requestGeneration
  }

  private applyDetail(data: AdminUserDetail, options: DetailLoadOptions) {
    const shouldSync = options.syncForm && (!options.preserveDirty || !this.isEditDirty())
    this.detail.value = data
    if (shouldSync) this.syncEditForm(data)
  }

  private async requestDetail(options: DetailLoadOptions) {
    if (this.destroyed) return
    this.cancelActiveRequest()
    const controller = new AbortController()
    this.activeRequest = controller
    const ticket = ++this.requestGeneration
    if (!options.silent) this.loading.value = true

    try {
      const response = await getUserDetail(this.options.openId, controller.signal)
      if (this.isStale(ticket)) return
      this.applyDetail(response.data.data, options)
    } catch (error: unknown) {
      if (controller.signal.aborted || this.isStale(ticket)) return
      logApiError({ context: 'users.detail', error, fallback: '获取用户详情信息失败' })
      if (!options.silent) {
        ElMessage.error(getApiErrorMessage(error, '获取用户详情信息失败'))
      }
    } finally {
      if (!this.isStale(ticket) && !options.silent) this.loading.value = false
      if (this.activeRequest === controller) this.activeRequest = null
    }
  }

  loadDetail(syncForm = true, silent = false) {
    return this.requestDetail({ syncForm, silent, preserveDirty: true })
  }

  refreshNow() {
    if (document.hidden || this.destroyed) return
    void this.loadDetail(true, true)
  }

  stopPolling() {
    if (this.refreshTimer === null) return
    window.clearInterval(this.refreshTimer)
    this.refreshTimer = null
  }

  startPolling() {
    this.stopPolling()
    this.refreshTimer = window.setInterval(() => this.refreshNow(), this.refreshIntervalMs)
  }

  handleVisibilityChange() {
    if (document.hidden) {
      this.stopPolling()
      this.cancelActiveRequest()
      return
    }
    this.refreshNow()
    this.startPolling()
  }

  async handleSave() {
    if (!this.detail.value) return
    const payload = buildUserUpdatePayload(this.editForm, this.detail.value)
    if (Object.keys(payload).length === 0) {
      ElMessage.warning('内容未做任何修改')
      return
    }

    this.saveLoading.value = true
    try {
      this.cancelActiveRequest()
      await updateUser(this.options.openId, payload)
      if (this.destroyed) return
      ElMessage.success('保存修改成功')
      await this.requestDetail({ syncForm: true, silent: false, preserveDirty: false })
    } catch (error: unknown) {
      if (this.destroyed) return
      logApiError({ context: 'users.update', error, fallback: '更新用户信息失败' })
      ElMessage.error(getApiErrorMessage(error, '更新用户信息失败'))
    } finally {
      this.saveLoading.value = false
    }
  }

  copyOpenId(text: string) {
    if (!text) return
    navigator.clipboard.writeText(text).then(() => {
      ElMessage.success('OpenID 已成功复制到剪贴板')
    }).catch((error: unknown) => {
      logApiError({ context: 'users.copy-open-id', error, fallback: '复制失败' })
      ElMessage.error('复制失败，请重试')
    })
  }

  initialize() {
    if (this.destroyed) return
    void this.loadDetail()
    this.startPolling()
    document.addEventListener('visibilitychange', this.visibilityHandler)
    this.listeningForVisibility = true
  }

  destroy() {
    this.destroyed = true
    this.cancelActiveRequest()
    this.stopPolling()
    if (this.listeningForVisibility) {
      document.removeEventListener('visibilitychange', this.visibilityHandler)
      this.listeningForVisibility = false
    }
  }
}

/** Exposes stable page bindings while keeping the controller methods bound. */
export function useUserDetail(options: UseUserDetailOptions) {
  const controller = new UserDetailController(options)
  return {
    copyOpenId: (text: string) => controller.copyOpenId(text),
    destroy: () => controller.destroy(),
    detail: controller.detail,
    editForm: controller.editForm,
    handleSave: () => controller.handleSave(),
    initialize: () => controller.initialize(),
    isEditDirty: () => controller.isEditDirty(),
    loadDetail: (syncForm = true, silent = false) => controller.loadDetail(syncForm, silent),
    loading: controller.loading,
    saveLoading: controller.saveLoading,
  }
}
