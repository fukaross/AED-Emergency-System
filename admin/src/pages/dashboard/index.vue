<template>
  <admin-layout>
    <div class="dashboard-container">
      <welcome-banner :username="username" />

      <!-- KPI 卡片区（密） -->
      <el-skeleton :loading="loading" animated>
        <template #template>
          <div class="kpi-grid">
            <div v-for="i in 4" :key="i" class="kpi-skeleton">
              <el-skeleton-item variant="circle" style="width: 40px; height: 40px" />
              <el-skeleton-item variant="text" style="width: 40%; margin-top: 16px" />
              <el-skeleton-item variant="h1" style="width: 55%; margin-top: 10px; height: 28px" />
            </div>
          </div>
        </template>

        <template #default>
          <div class="kpi-grid">
            <kpi-card
              label="总用户数"
              :value="data.usersTotal"
              sub="系统注册用户总量"
              :accent="ACCENT.user"
              :icon="User"
            />
            <kpi-card
              label="总求助数"
              :value="data.helpRequestsTotal"
              sub="累计紧急求助事件"
              :accent="ACCENT.help"
              :icon="Histogram"
            />
            <kpi-card
              label="待审核求助"
              :value="data.pendingReviewTotal"
              sub="点击立即查看处理"
              :accent="ACCENT.pending"
              :icon="BellFilled"
              clickable
              @click="goToPendingReview"
            />
            <kpi-card
              label="今日新增求助"
              :value="data.todayNewHelpRequests"
              sub="今天发生的求助数量"
              :accent="ACCENT.today"
              :icon="Calendar"
            />
          </div>
        </template>
      </el-skeleton>

      <!-- 图表区（疏） -->
      <div class="chart-grid">
        <stat-chart-card
          title="用户增长"
          subtitle="近 30 天累计"
          :loading="loading"
          :is-empty="!hasUserGrowth"
          :enter-delay="0"
        >
          <growth-line-chart :points="charts.userGrowth" :color="ACCENT.user" series-name="用户" />
        </stat-chart-card>

        <stat-chart-card
          title="急救员增长"
          subtitle="持证累计 · 近 30 天"
          :loading="loading"
          :is-empty="!hasResponderGrowth"
          :enter-delay="100"
        >
          <growth-line-chart
            :points="charts.responderGrowth"
            :color="ACCENT.today"
            series-name="急救员"
          />
        </stat-chart-card>

        <stat-chart-card
          title="求助处理概览"
          subtitle="近 14 天"
          :loading="loading"
          :is-empty="!hasProcessing"
          :enter-delay="200"
        >
          <help-processing-chart :points="charts.helpProcessing" />
        </stat-chart-card>

        <stat-chart-card
          title="求助状态分布"
          :loading="loading"
          :is-empty="!hasStatus"
          :enter-delay="300"
        >
          <help-status-chart :slices="charts.statusDistribution" />
        </stat-chart-card>
      </div>

      <!-- 日志区（密） -->
      <recent-logs :logs="data.recentLogs" :loading="loading" :is-super-admin="isSuperAdmin" />
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { User, Histogram, BellFilled, Calendar } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import welcomeBanner from './components/welcome-banner.vue'
import kpiCard from './components/kpi-card.vue'
import statChartCard from './components/stat-chart-card.vue'
import recentLogs from './components/recent-logs.vue'
import growthLineChart from './components/charts/growth-line-chart.vue'
import helpProcessingChart from './components/charts/help-processing-chart.vue'
import helpStatusChart from './components/charts/help-status-chart.vue'
import { getDashboard } from '@/api/dashboard'
import { useAdminStore } from '@/stores/admin'
import type { AdminDashboardResponse } from '@/types/dashboard'
import { getApiErrorMessage } from '@/api/client'

const POLL_INTERVAL_MS = 5_000
const ACCENT = { user: '#409eff', help: '#17b3c4', pending: '#e6a23c', today: '#2bb47e' }

const router = useRouter()
const store = useAdminStore()
const loading = ref(true)

const data = ref<AdminDashboardResponse>({
  usersTotal: 0,
  helpRequestsTotal: 0,
  pendingReviewTotal: 0,
  todayNewHelpRequests: 0,
  recentLogs: [],
  charts: { userGrowth: [], responderGrowth: [], helpProcessing: [], statusDistribution: [] },
})

const isSuperAdmin = computed(() => store.isSuperAdmin)
const username = computed(() => store.username)
const charts = computed(() => data.value.charts)
const hasUserGrowth = computed(() => charts.value.userGrowth.some((p) => p.total > 0))
const hasResponderGrowth = computed(() => charts.value.responderGrowth.some((p) => p.total > 0))
const hasProcessing = computed(() =>
  charts.value.helpProcessing.some((p) => p.processed + p.pending > 0),
)
const hasStatus = computed(() => charts.value.statusDistribution.length > 0)

let pollTimer: ReturnType<typeof setInterval> | null = null
let isPolling = false

function goToPendingReview() {
  router.push({ path: '/help-requests', query: { status: 'pending_review' } })
}

async function loadDashboardData() {
  loading.value = true
  try {
    const res = await getDashboard()
    data.value = res.data.data
  } catch (error: unknown) {
    console.error(error)
    ElMessage.error(getApiErrorMessage(error, '获取仪表盘数据失败，请重试'))
  } finally {
    loading.value = false
  }
}

async function pollDashboard() {
  if (isPolling) return
  isPolling = true
  try {
    const res = await getDashboard()
    data.value = res.data.data
  } catch {
    /* silently ignore poll errors */
  } finally {
    isPolling = false
  }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(pollDashboard, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
  isPolling = false
}

onMounted(() => {
  loadDashboardData()
  startPolling()
})

onUnmounted(() => {
  stopPolling()
})
</script>

<style scoped lang="scss">
@use '../../styles/variables' as *;

.dashboard-container {
  padding: $page-padding;

  @media (max-width: 767px) {
    padding: 12px;
  }
}

.kpi-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: $section-gap;
  margin-bottom: $chart-gap;

  @media (max-width: 1023px) {
    grid-template-columns: repeat(2, 1fr);
  }

  @media (max-width: 767px) {
    grid-template-columns: 1fr;
    gap: 12px;
  }
}

.kpi-skeleton {
  background-color: $card-bg;
  border: $card-border;
  border-radius: $radius-card;
  box-shadow: $card-shadow;
  padding: $card-padding;
}

.chart-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: $chart-gap;
  margin-bottom: $section-gap;

  @media (max-width: 1023px) {
    grid-template-columns: 1fr;
  }
}
</style>
