<template>
  <admin-layout>
    <div class="page-content">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">急救会话监控</h3>
        </div>

        <el-tabs v-model="activeTab" class="session-tabs" @tab-change="handleTabChange">
          <el-tab-pane label="进行中" name="active" />
          <el-tab-pane label="历史存档" name="history" />
        </el-tabs>

        <div v-if="!hasLoadedSuccessfully && !errorMessage" class="state-card">
          <el-skeleton :rows="5" animated />
        </div>

        <div v-else-if="!hasLoadedSuccessfully" class="state-card">
          <el-result icon="error" :title="errorMessage">
            <template #extra>
              <el-button type="primary" @click="retry">重新加载</el-button>
            </template>
          </el-result>
        </div>

        <template v-else>
          <el-alert
            v-if="refreshErrorMessage"
            class="refresh-alert"
            type="warning"
            show-icon
            :closable="false"
            :title="staleWarning"
          />

          <div class="table-responsive">
            <el-table :data="tableData" style="width: 100%;" class="data-table">
              <el-table-column prop="requestNo" label="求助编号" width="160" align="left" />
              <el-table-column prop="address" label="位置" min-width="160" align="left" show-overflow-tooltip />
              <el-table-column prop="helpStatus" label="求助状态" width="100" align="center">
                <template #default="{ row }">
                  <status-tag :status="row.helpStatus" category="help" />
                </template>
              </el-table-column>
              <el-table-column prop="participantsCount" label="参与者" width="90" align="center">
                <template #default="{ row }">{{ row.participantsCount }}人</template>
              </el-table-column>
              <el-table-column prop="messagesCount" label="消息数" width="90" align="center" />
              <el-table-column prop="lastActivityAt" label="最近活动" width="150" align="left">
                <template #default="{ row }">{{ formatCompactDateTime(row.lastActivityAt) || '—' }}</template>
              </el-table-column>
              <el-table-column prop="createdAt" label="发布时间" width="150" align="left">
                <template #default="{ row }">{{ formatCompactDateTime(row.createdAt) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="90" align="center" fixed="right">
                <template #default="{ row }">
                  <el-button text type="primary" size="small" @click="viewDetail(row.helpId)">
                    监控
                  </el-button>
                </template>
              </el-table-column>

              <template #empty>
                <el-empty :description="activeTab === 'active' ? '暂无进行中的急救会话' : '暂无历史会话存档'" />
              </template>
            </el-table>
          </div>

          <div class="pagination-container">
            <el-pagination
              v-model:current-page="currentPage"
              :page-size="pageSize"
              :total="totalItems"
              small
              layout="total, prev, pager, next"
              @current-change="handlePageChange"
            />
          </div>
        </template>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { useRescueSessionList } from '@/composables/use-rescue-session-list'
import { formatCompactDateTime } from '@/utils/date-time'

type SessionTab = 'active' | 'history'

const router = useRouter()

const pageSize = ref(10)
const currentPage = ref(1)
const activeTab = ref<SessionTab>('active')

const {
  items: tableData,
  totalItems,
  errorMessage,
  refreshErrorMessage,
  lastSuccessfulAt,
  hasLoadedSuccessfully,
  initialize,
  retry,
  setVisible,
  setQuery,
  destroy,
} = useRescueSessionList({
  query: { status: activeTab.value, page: currentPage.value, pageSize: pageSize.value },
  initiallyVisible: !document.hidden,
})

const staleWarning = computed(() => {
  const lastUpdated = formatCompactDateTime(lastSuccessfulAt.value)
  return `数据更新失败，当前显示 ${lastUpdated || '上次成功请求'} 的数据：${refreshErrorMessage.value}`
})

function handleTabChange() {
  currentPage.value = 1
  void setQuery(currentQuery())
}

function handlePageChange(page: number) {
  currentPage.value = page
  void setQuery(currentQuery())
}

function viewDetail(helpId: string) {
  router.push(`/rescue-sessions/${helpId}`)
}

function currentQuery() {
  return {
    status: activeTab.value,
    page: currentPage.value,
    pageSize: pageSize.value,
  } as const
}

function handleVisibilityChange() {
  void setVisible(!document.hidden)
}

onMounted(async () => {
  document.addEventListener('visibilitychange', handleVisibilityChange)
  await initialize()
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

.session-tabs {
  padding: 0 16px;
  background-color: #ffffff;

  :deep(.el-tabs__header) {
    margin-bottom: 0;
  }
}

.state-card {
  padding: 24px;
}

.refresh-alert {
  margin: 12px 16px;
}

.data-table {
  border-top: 1px solid #ebeef5;
}
</style>
