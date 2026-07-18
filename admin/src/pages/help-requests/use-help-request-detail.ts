import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { closeHelpRequest, getHelpRequestDetail } from '@/api/help'
import { getApiErrorMessage } from '@/api/client'
import type { AdminHelpDetailResponse, HelpRequestStatus } from '@/types/help'
import { logApiError } from '@/utils/safe-api-error'

const POLL_INTERVAL_MS = 10_000
const CLOSED_STATUSES: ReadonlySet<HelpRequestStatus> = new Set(['closed', 'false_alarm'])

export type HelpCloseTarget = 'closed' | 'false_alarm'
type DetailRequestMode = 'initial' | 'refresh'

export interface HelpRequestDetailOptions {
  readonly requestId: string
  readonly initiallyVisible?: boolean
}

class HelpRequestDetailController {
  readonly loading = ref(true)
  readonly actionLoading = ref(false)
  readonly detail = ref<AdminHelpDetailResponse | null>(null)
  readonly closeNote = ref('')
  readonly loadErrorMessage = ref('')
  readonly refreshErrorMessage = ref('')
  readonly lastSuccessfulAt = ref<Date | null>(null)
  readonly confirmDialogVisible = ref(false)
  readonly pendingTargetStatus = ref<HelpCloseTarget | null>(null)
  readonly confirmDialogMessage = computed(() => {
    const actionText = this.pendingTargetStatus.value === 'false_alarm' ? '标记为误报' : '关闭此求助'
    return `确定要进行"${actionText}"操作吗？此操作不可逆。`
  })

  private pollTimer: ReturnType<typeof setTimeout> | null = null
  private activeRequest: AbortController | null = null
  private requestGeneration = 0
  private destroyed = false
  private initialErrorNotified = false
  private visible: boolean

  constructor(private readonly options: HelpRequestDetailOptions) {
    this.visible = options.initiallyVisible ?? true
  }

  private isClosed(): boolean {
    return this.detail.value !== null && CLOSED_STATUSES.has(this.detail.value.status)
  }

  private stopPolling() {
    if (this.pollTimer !== null) clearTimeout(this.pollTimer)
    this.pollTimer = null
  }

  private cancelActiveRequest() {
    this.requestGeneration += 1
    this.activeRequest?.abort()
    this.activeRequest = null
  }

  private schedulePolling() {
    this.stopPolling()
    if (!this.canRequest() || this.isClosed()) return
    this.pollTimer = setTimeout(() => { void this.runPoll() }, POLL_INTERVAL_MS)
  }

  private isStale(ticket: number): boolean {
    return this.destroyed || ticket !== this.requestGeneration
  }

  private applyDetail(value: AdminHelpDetailResponse, mode: DetailRequestMode) {
    this.detail.value = value
    if (mode === 'initial' && value.closeNote) this.closeNote.value = value.closeNote
    this.loadErrorMessage.value = ''
    this.refreshErrorMessage.value = ''
    this.lastSuccessfulAt.value = new Date()
  }

  private async requestDetail(mode: DetailRequestMode): Promise<boolean> {
    if (!this.canRequest()) return false
    this.activeRequest?.abort()
    const controller = new AbortController()
    this.activeRequest = controller
    const ticket = ++this.requestGeneration
    if (mode === 'initial') this.loading.value = true

    try {
      const response = await getHelpRequestDetail(this.options.requestId, controller.signal)
      if (this.isStale(ticket)) return false
      this.applyDetail(response.data.data, mode)
      return true
    } catch (error: unknown) {
      if (controller.signal.aborted || this.isStale(ticket)) return false
      this.handleRequestError(error, mode)
      return true
    } finally {
      if (!this.isStale(ticket) && mode === 'initial') this.loading.value = false
      if (this.activeRequest === controller) this.activeRequest = null
    }
  }

  private handleRequestError(error: unknown, mode: DetailRequestMode) {
    const fallback = mode === 'initial' ? '获取求助详情数据失败' : '求助详情刷新失败'
    const message = getApiErrorMessage(error, fallback)
    logApiError({ context: `help-requests.detail.${mode}`, error, fallback })
    if (mode === 'refresh') {
      if (this.detail.value) this.refreshErrorMessage.value = message
      else this.loadErrorMessage.value = message
      return
    }
    this.loadErrorMessage.value = message
    if (!this.initialErrorNotified) {
      this.initialErrorNotified = true
      ElMessage.error(message)
    }
  }

  private async runPoll() {
    this.pollTimer = null
    if (!this.canRequest() || this.isClosed()) return
    const completed = await this.requestDetail('refresh')
    if (completed) this.schedulePolling()
  }

  async initialize() {
    if (this.destroyed) return
    if (!this.visible) {
      this.loading.value = false
      return
    }
    const completed = await this.requestDetail('initial')
    if (completed) this.schedulePolling()
  }

  async retry() {
    this.initialErrorNotified = false
    const completed = await this.requestDetail('initial')
    if (completed) this.schedulePolling()
  }

  async setVisible(visible: boolean) {
    if (this.destroyed || visible === this.visible) return
    this.visible = visible
    if (!visible) {
      this.stopPolling()
      this.cancelActiveRequest()
      this.loading.value = false
      return
    }
    const mode: DetailRequestMode = this.detail.value ? 'refresh' : 'initial'
    const completed = await this.requestDetail(mode)
    if (completed) this.schedulePolling()
  }

  handleCloseRequest(targetStatus: HelpCloseTarget) {
    this.pendingTargetStatus.value = targetStatus
    this.confirmDialogVisible.value = true
  }

  async submitCloseRequest() {
    const targetStatus = this.pendingTargetStatus.value
    if (targetStatus === null) return
    this.actionLoading.value = true
    this.stopPolling()
    this.cancelActiveRequest()

    try {
      await closeHelpRequest(this.options.requestId, {
        status: targetStatus,
        note: this.closeNote.value || undefined,
      })
      if (this.destroyed) return
      this.confirmDialogVisible.value = false
      ElMessage.success('操作成功')
      await this.requestDetail('initial')
    } catch (error: unknown) {
      if (!this.destroyed) this.handleCloseError(error)
    } finally {
      if (!this.destroyed) {
        this.actionLoading.value = false
        this.schedulePolling()
      }
    }
  }

  private handleCloseError(error: unknown) {
    const fallback = '操作失败，请重试'
    logApiError({ context: 'help-requests.close', error, fallback })
    ElMessage.error(getApiErrorMessage(error, fallback))
  }

  destroy() {
    this.destroyed = true
    this.cancelActiveRequest()
    this.stopPolling()
  }

  private canRequest(): boolean {
    return !this.destroyed && this.visible
  }
}

/** 管理求助详情的加载、顺序轮询与关闭操作，页面仅负责展示和路由。 */
export function useHelpRequestDetail(options: HelpRequestDetailOptions) {
  const controller = new HelpRequestDetailController(options)
  return {
    actionLoading: controller.actionLoading,
    closeNote: controller.closeNote,
    confirmDialogMessage: controller.confirmDialogMessage,
    confirmDialogVisible: controller.confirmDialogVisible,
    detail: controller.detail,
    handleCloseRequest: controller.handleCloseRequest.bind(controller),
    initialize: controller.initialize.bind(controller),
    lastSuccessfulAt: controller.lastSuccessfulAt,
    loadErrorMessage: controller.loadErrorMessage,
    loading: controller.loading,
    pendingTargetStatus: controller.pendingTargetStatus,
    refreshErrorMessage: controller.refreshErrorMessage,
    retry: controller.retry.bind(controller),
    setVisible: controller.setVisible.bind(controller),
    submitCloseRequest: controller.submitCloseRequest.bind(controller),
    destroy: controller.destroy.bind(controller),
  }
}
