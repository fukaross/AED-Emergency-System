<template>
  <admin-layout>
    <div class="page-content">
      <div v-if="loading && !detail" class="admin-card state-card">
        <el-skeleton :rows="6" animated />
      </div>

      <div v-else-if="errorMessage" class="admin-card state-card">
        <el-result icon="error" :title="errorMessage">
          <template #extra>
            <el-button type="primary" @click="retry">重新加载</el-button>
            <el-button type="primary" @click="goBack">返回列表</el-button>
          </template>
        </el-result>
      </div>

      <template v-else-if="detail">
        <el-alert
          v-if="refreshErrorMessage"
          class="refresh-alert"
          type="warning"
          show-icon
          :closable="false"
          :title="staleWarning"
        />

        <div class="admin-card header-card">
          <div class="header-main">
            <h3 class="header-title">会话 {{ detail.requestNo }}</h3>
            <status-tag :status="detail.helpStatus" category="help" />
          </div>
          <div class="header-meta">
            <span>求助人：{{ detail.requester.displayName }}</span>
            <span>SOS：{{ detail.sos.address }}</span>
            <span>参与者：{{ detail.participants.length }} 人</span>
            <span v-if="detail.closedAt">
              关闭于 {{ formatCompactDateTime(detail.closedAt) }}<template v-if="detail.closeNote">（{{ detail.closeNote }}）</template>
            </span>
          </div>
        </div>

        <div class="detail-grid">
          <div class="admin-card map-card no-padding">
            <rescue-map :sos="detail.sos" :participants="detail.participants" />
          </div>

          <div class="side-column">
            <div class="admin-card">
              <h4 class="section-title">参与者与状态</h4>
              <div v-if="!detail.participants.length" class="empty-line">暂无参与者</div>
              <div v-for="participant in detail.participants" :key="participant.openId" class="participant-row">
                <span class="participant-name">{{ participant.displayName }}</span>
                <span class="participant-badges">
                  <el-tag v-if="participant.takenAed" type="success" size="small">拿了AED</el-tag>
                  <el-tag v-if="participant.arrived" type="danger" size="small">已到达</el-tag>
                  <el-tag v-if="participant.signal !== 'ok'" type="info" size="small">
                    {{ signalText(participant.signal) }}
                  </el-tag>
                </span>
              </div>
            </div>

            <div class="admin-card messages-card">
              <h4 class="section-title">消息与状态时间线</h4>
              <div v-if="!messageViews.length" class="empty-line">暂无消息</div>
              <div v-else class="message-list">
                <div
                  v-for="message in messageViews"
                  :key="message.key"
                  class="message-row"
                  :class="{ 'message-row--event': message.isStatusEvent }"
                >
                  <span class="message-text">{{ message.text }}</span>
                  <span class="message-time">
                    {{ message.createdAt ? formatCompactDateTime(message.createdAt) : '时间无效' }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import rescueMap from '@/components/rescue-map.vue'
import { useRescueSessionDetail } from '@/composables/use-rescue-session-detail'
import { formatCompactDateTime } from '@/utils/date-time'
import { buildRescueMessageViews, signalText } from '@/utils/rescue-session'

const route = useRoute()
const router = useRouter()
const initialHelpId = String(route.params.helpId || '')

const {
  detail,
  loading,
  errorMessage,
  refreshErrorMessage,
  lastSuccessfulAt,
  loadData,
  setVisible,
  setHelpId,
  destroy,
} = useRescueSessionDetail({
  helpId: initialHelpId,
  initiallyVisible: !document.hidden,
})

const staleWarning = computed(() => {
  const lastUpdated = formatCompactDateTime(lastSuccessfulAt.value)
  return `数据更新失败，当前显示 ${lastUpdated || '上次成功请求'} 的数据：${refreshErrorMessage.value}`
})
const messageViews = computed(() => buildRescueMessageViews(detail.value?.messages))

function handleVisibilityChange(): void {
  void setVisible(!document.hidden)
}

function goBack(): void {
  router.push('/rescue-sessions')
}

async function retry(): Promise<void> {
  await loadData()
}

watch(
  () => String(route.params.helpId || ''),
  (helpId) => { void setHelpId(helpId) },
)

onMounted(async () => {
  document.addEventListener('visibilitychange', handleVisibilityChange)
  if (!initialHelpId) {
    errorMessage.value = '缺少求助编号'
    return
  }
  await loadData()
})

onUnmounted(() => {
  destroy()
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})
</script>

<style scoped lang="scss">
.page-content {
  padding: 24px;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.state-card {
  padding: 24px;
}

.refresh-alert {
  margin-bottom: 16px;
}

.header-card {
  padding: 16px 20px;
  margin-bottom: 16px;
}

.header-main {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.header-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.header-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  color: #606266;
  font-size: 13px;
}

.detail-grid {
  display: grid;
  grid-template-columns: 3fr 2fr;
  gap: 16px;

  @media (max-width: 1023px) {
    grid-template-columns: 1fr;
  }
}

.map-card {
  min-height: 480px;
}

.side-column {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  margin: 0 0 12px;
  font-size: 15px;
  font-weight: 600;
}

.empty-line {
  color: #909399;
  font-size: 13px;
}

.participant-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid #f0f0f0;

  &:last-child {
    border-bottom: none;
  }
}

.participant-name {
  font-size: 14px;
  color: #303133;
}

.participant-badges {
  display: flex;
  gap: 6px;
}

.messages-card {
  flex: 1;
}

.message-list {
  max-height: 360px;
  overflow-y: auto;
}

.message-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
  padding: 6px 0;
  border-bottom: 1px solid #f5f5f5;

  &:last-child {
    border-bottom: none;
  }
}

.message-row--event .message-text {
  color: #e6a23c;
}

.message-text {
  font-size: 13px;
  color: #303133;
  word-break: break-all;
}

.message-time {
  flex-shrink: 0;
  font-size: 12px;
  color: #909399;
}
</style>
