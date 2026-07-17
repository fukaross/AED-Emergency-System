<template>
  <admin-layout>
    <div class="detail-container">
      <!-- Header bar with Back button -->
      <div class="detail-header-action">
        <el-button type="default" size="default" @click="goBack">
          ← 返回列表
        </el-button>
        <el-button v-if="detail" type="primary" plain size="default" @click="goRescueSession">
          会话监控
        </el-button>
      </div>

      <div v-loading="loading">
        <template v-if="detail">
          <!-- 1. Basic Info Card -->
          <div class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">基本信息</h3>
            </div>
            <div class="card-body-custom">
              <div class="description-list">
                <div class="description-item">
                  <span class="desc-label">编号</span>
                  <span class="desc-value">{{ detail.requestNo }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">状态</span>
                  <span class="desc-value">
                    <status-tag :status="detail.status" category="help" />
                  </span>
                </div>
                <div class="description-item">
                  <span class="desc-label">渠道</span>
                  <span class="desc-value">
                    <status-tag :status="detail.requestChannel" category="channel" />
                  </span>
                </div>
                <div class="description-item">
                  <span class="desc-label">类型</span>
                  <span class="desc-value">{{ getHelpTypeCn(detail.type) }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">位置</span>
                  <span class="desc-value">{{ detail.address }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">发起时间</span>
                  <span class="desc-value">{{ formatTime(detail.createdAt) }}</span>
                </div>
              </div>
              <div class="description-item content-row">
                <span class="desc-label">内容</span>
                <span class="desc-value main-content-text">{{ detail.content }}</span>
              </div>
              <div v-if="detail.images && detail.images.length > 0" class="description-item content-row">
                <span class="desc-label">现场图片</span>
                <div class="desc-value request-images">
                  <el-image
                    v-for="(img, idx) in detail.images"
                    :key="img"
                    :src="img"
                    :preview-src-list="detail.images"
                    :initial-index="idx"
                    fit="cover"
                    class="request-thumbnail"
                    preview-teleported
                  />
                </div>
              </div>
            </div>
          </div>

          <!-- 2. Report Uploaders Card -->
          <div class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">报告上传者 ({{ detail.reportersCount }}人)</h3>
            </div>
            <div class="card-body-custom">
              <div v-if="detail.reporters && detail.reporters.length > 0" class="reporter-list">
                <div v-for="reporter in detail.reporters" :key="reporter.openId || reporter.lastReportedAt" class="reporter-item">
                  <span class="reporter-name">{{ reporter.displayName }}</span>
                  <span class="reporter-status">
                    {{ reporter.reportsCount }}份报告 · 最近 {{ formatTimeHourMin(reporter.lastReportedAt) }}
                  </span>
                </div>
              </div>
              <div v-else class="empty-text">
                暂无报告上传者
              </div>
            </div>
          </div>

          <!-- 3. Help Reports Card -->
          <div v-if="detail.reports && detail.reports.length > 0" class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">救助报告</h3>
            </div>
            <div class="card-body-custom report-container">
              <div v-for="(rep, index) in detail.reports" :key="rep.id" class="report-item">
                <div class="report-header">
                  <span class="reporter-label">上传者：{{ formatReporterOpenId(rep.reporterOpenId) }}</span>
                  <span class="report-time">时间：{{ formatTime(rep.createdAt) }}</span>
                </div>
                <p class="report-content">{{ rep.content }}</p>
                <div v-if="rep.images && rep.images.length > 0" class="report-images">
                  <el-image
                    v-for="(img, idx) in rep.images"
                    :key="idx"
                    :src="img"
                    :preview-src-list="rep.images"
                    :initial-index="idx"
                    fit="cover"
                    class="report-thumbnail"
                    preview-teleported
                  />
                </div>
                <div v-if="index < detail.reports.length - 1" class="report-divider" />
              </div>
            </div>
          </div>

          <!-- 4. Timeline Card -->
          <div class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">时间线</h3>
            </div>
            <div class="card-body-custom">
              <el-timeline v-if="detail.timeline && detail.timeline.length > 0" class="detail-timeline">
                <el-timeline-item
                  v-for="(event, index) in detail.timeline"
                  :key="index"
                  :timestamp="formatTime(event.createdAt)"
                  color="#409eff"
                  placement="top"
                >
                  <span class="timeline-desc">{{ event.description }}</span>
                </el-timeline-item>
              </el-timeline>
              <div v-else class="empty-text">
                暂无时间线记录
              </div>
            </div>
          </div>

          <!-- 5. Admin Action Card -->
          <div class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">管理操作</h3>
            </div>
            <div class="card-body-custom">
              <!-- If active: show processing controls -->
              <div v-if="detail.status !== 'closed' && detail.status !== 'false_alarm'" class="action-form">
                <el-input
                  v-model="closeNote"
                  type="textarea"
                  :rows="2"
                  placeholder="管理处理备注（可选）"
                  class="action-note"
                />
                <div class="action-buttons">
                  <el-button
                    type="primary"
                    :loading="actionLoading"
                    @click="handleCloseRequest('closed')"
                  >
                    关闭求助
                  </el-button>
                  <el-button
                    type="warning"
                    :loading="actionLoading"
                    @click="handleCloseRequest('false_alarm')"
                  >
                    标记误报
                  </el-button>
                </div>
              </div>

              <!-- If resolved: display archived info -->
              <div v-else class="archived-info">
                <div class="description-item single-col">
                  <span class="desc-label">关闭时间</span>
                  <span class="desc-value">{{ formatTime(detail.closedAt || '') }}</span>
                </div>
                <div class="description-item single-col mt-12">
                  <span class="desc-label">处理备注</span>
                  <span class="desc-value archive-note">{{ detail.closeNote || '无备注信息' }}</span>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>

    <el-dialog
      v-model="confirmDialogVisible"
      title="确认提示"
      width="420px"
      :close-on-click-modal="!actionLoading"
      :close-on-press-escape="!actionLoading"
    >
      <span>{{ confirmDialogMessage }}</span>
      <template #footer>
        <el-button :disabled="actionLoading" @click="confirmDialogVisible = false">
          取消
        </el-button>
        <el-button type="primary" :loading="actionLoading" @click="submitCloseRequest">
          确定
        </el-button>
      </template>
    </el-dialog>
  </admin-layout>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getHelpRequestDetail, closeHelpRequest } from '@/api/help'
import type { AdminHelpDetailResponse } from '@/types/help'
import { getApiErrorMessage } from '@/api/client'

const route = useRoute()
const router = useRouter()
const requestId = route.params.id as string

const POLL_INTERVAL_MS = 10_000

const loading = ref(true)
const actionLoading = ref(false)
const detail = ref<AdminHelpDetailResponse | null>(null)
const closeNote = ref('')
const confirmDialogVisible = ref(false)
const pendingTargetStatus = ref<'closed' | 'false_alarm' | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false

const typeCnMap: Record<string, string> = {
  help_hand: '求助帮手',
  call_120: '呼叫 120',
  call_school: '呼叫校医',
  general: '普通求助',
}

function getHelpTypeCn(type: string) {
  return typeCnMap[type] || type
}

function goBack() {
  router.push('/help-requests')
}

/** 跳转该求助关联的急救会话监控页（BackEnd.md §4.12 管理端只读监控入口） */
function goRescueSession() {
  if (!detail.value) return
  router.push(`/rescue-sessions/${detail.value.id}`)
}

// Convert UTC timestamps to local dates
function formatTime(dateStr: string) {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const yyyy = date.getFullYear()
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  const ss = String(date.getSeconds()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd} ${hh}:${min}:${ss}`
}

function formatTimeHourMin(dateStr: string) {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${hh}:${min}`
}

function formatReporterOpenId(openId: string) {
  if (!openId) return '未知上传者'
  return openId.length <= 10 ? openId : `${openId.slice(0, 6)}...${openId.slice(-4)}`
}

const confirmDialogMessage = computed(() => {
  const actionText = pendingTargetStatus.value === 'false_alarm' ? '标记为误报' : '关闭此求助'
  return `确定要进行"${actionText}"操作吗？此操作不可逆。`
})

async function loadDetail() {
  loading.value = true
  try {
    const res = await getHelpRequestDetail(requestId)
    detail.value = res.data.data
    if (res.data.data.closeNote) {
      closeNote.value = res.data.data.closeNote
    }
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取求助详情数据失败'))
  } finally {
    loading.value = false
  }
}

// Trigger state close action
function handleCloseRequest(targetStatus: 'closed' | 'false_alarm') {
  pendingTargetStatus.value = targetStatus
  confirmDialogVisible.value = true
}

async function submitCloseRequest() {
  if (pendingTargetStatus.value === null) return

  actionLoading.value = true
  try {
    await closeHelpRequest(requestId, {
      status: pendingTargetStatus.value,
      note: closeNote.value || undefined,
    })
    confirmDialogVisible.value = false
    ElMessage.success('操作成功')
    await loadDetail()
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '操作失败，请重试'))
  } finally {
    actionLoading.value = false
  }
}

onMounted(() => {
  loadDetail()
  startPolling()
})

onUnmounted(() => {
  stopPolling()
})

async function pollDetail() {
  if (isPolling || !detail.value) return
  if (detail.value.status === 'closed' || detail.value.status === 'false_alarm') {
    stopPolling()
    return
  }
  isPolling = true
  try {
    const res = await getHelpRequestDetail(requestId)
    detail.value = res.data.data
    if (res.data.data.status === 'closed' || res.data.data.status === 'false_alarm') {
      stopPolling()
    }
  } catch { /* silently ignore poll errors */ }
  finally { isPolling = false }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(pollDetail, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
  isPolling = false
}
</script>

<style scoped lang="scss">
.detail-container {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.detail-header-action {
  margin-bottom: 20px;
}

.card-header-bar-custom {
  padding: 16px 24px;
  border-bottom: 1px solid #ebeef5;
}

.card-title {
  margin: 0;
  font-size: 15px;
  font-weight: 500;
  color: #303133;
}

.card-body-custom {
  padding: 20px 24px;
}

.content-row {
  margin-top: 20px;
  border-top: 1px dashed #ebeef5;
  padding-top: 16px;
}

.main-content-text {
  font-size: 14px;
  color: #303133;
  line-height: 1.6;
}

.request-images {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.request-thumbnail {
  width: 96px;
  height: 96px;
  border-radius: 4px;
  cursor: pointer;
  border: 1px solid #ebeef5;
}

// Report uploader list
.reporter-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.reporter-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background-color: #f8fafc;
  border-radius: 4px;
  font-size: 14px;
}

.reporter-name {
  color: #303133;
  font-weight: 500;
}

.reporter-status {
  color: #606266;
}

// Report styling
.report-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.report-item {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.report-header {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  color: #909399;
}

.report-content {
  font-size: 14px;
  color: #303133;
  margin: 0;
  line-height: 1.5;
}

.report-images {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 6px;
}

.report-thumbnail {
  width: 80px;
  height: 80px;
  border-radius: 4px;
  cursor: pointer;
  border: 1px solid #ebeef5;
  transition: transform 0.2s ease;

  &:hover {
    transform: scale(1.05);
  }
}

.report-divider {
  height: 1px;
  background-color: #ebeef5;
  margin-top: 16px;
}

// Timeline override
.detail-timeline {
  padding-left: 4px;
}

.timeline-desc {
  font-size: 14px;
  color: #303133;
}

// Action configurations
.action-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.action-note {
  width: 100%;
}

.action-buttons {
  display: flex;
  gap: 12px;
}

.archived-info {
  display: flex;
  flex-direction: column;
}

.archive-note {
  background-color: #fafafa;
  padding: 12px 16px;
  border-radius: 4px;
  border: 1px solid #ebeef5;
  width: 100%;
  min-height: 50px;
  white-space: pre-wrap;
}

.mt-12 {
  margin-top: 12px;
}

.empty-text {
  font-size: 14px;
  color: #909399;
  text-align: center;
  padding: 20px 0;
}
</style>
