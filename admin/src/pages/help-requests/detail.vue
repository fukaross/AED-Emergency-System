<template>
  <admin-layout>
    <div class="detail-container">
      <div class="detail-header-action">
        <el-button type="default" size="default" @click="goBack">
          ← 返回列表
        </el-button>
        <el-button v-if="detail" type="primary" plain size="default" @click="goRescueSession">
          会话监控
        </el-button>
      </div>

      <div v-loading="loading">
        <el-alert
          v-if="detail && refreshErrorMessage"
          type="warning"
          :closable="false"
          show-icon
          class="refresh-warning"
        >
          <template #title>
            数据刷新失败，当前显示最近一次成功结果（{{ formatCompactDateTime(lastSuccessfulAt) || '时间未知' }}）：{{ refreshErrorMessage }}
          </template>
        </el-alert>

        <el-result
          v-if="!loading && !detail && loadErrorMessage"
          icon="error"
          title="求助详情加载失败"
          :sub-title="loadErrorMessage"
        >
          <template #extra>
            <el-button type="primary" @click="retry">重新加载</el-button>
          </template>
        </el-result>

        <template v-if="detail">
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

          <div class="admin-card">
            <div class="card-header-bar-custom">
              <h3 class="card-title">管理操作</h3>
            </div>
            <div class="card-body-custom">
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
import { onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import type { HelpRequestType } from '@/types/help'
import {
  formatCompactDateTime,
  formatDateTime as formatTime,
  formatTimeOfDay as formatTimeHourMin,
} from '@/utils/date-time'
import { useHelpRequestDetail } from './use-help-request-detail'

const route = useRoute()
const router = useRouter()
const requestId = route.params.id as string
const typeCnMap: Readonly<Record<HelpRequestType, string>> = {
  help_hand: '求助帮手',
  call_120: '呼叫 120',
  call_school: '呼叫校医',
  general: '普通求助',
}
const {
  actionLoading,
  closeNote,
  confirmDialogMessage,
  confirmDialogVisible,
  destroy,
  detail,
  handleCloseRequest,
  initialize,
  lastSuccessfulAt,
  loadErrorMessage,
  loading,
  refreshErrorMessage,
  retry,
  submitCloseRequest,
  setVisible,
} = useHelpRequestDetail({ requestId, initiallyVisible: !document.hidden })
function getHelpTypeCn(type: HelpRequestType) {
  return typeCnMap[type]
}

function goBack() {
  void router.push('/help-requests')
}
/** 跳转该求助关联的急救会话监控页（BackEnd.md §4.12 管理端只读监控入口） */
function goRescueSession() {
  if (!detail.value) return
  void router.push(`/rescue-sessions/${detail.value.id}`)
}
function formatReporterOpenId(openId: string) {
  if (!openId) return '未知上传者'
  return openId.length <= 10 ? openId : `${openId.slice(0, 6)}...${openId.slice(-4)}`
}
function handleVisibilityChange(): void {
  void setVisible(!document.hidden)
}

onMounted(() => {
  document.addEventListener('visibilitychange', handleVisibilityChange)
  void initialize()
})
onUnmounted(() => {
  document.removeEventListener('visibilitychange', handleVisibilityChange)
  destroy()
})
</script>

<style scoped lang="scss" src="./detail.scss"></style>
