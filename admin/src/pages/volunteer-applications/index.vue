<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">志愿者审核</h3>
        </div>

        <div class="filter-bar">
          <el-select
            v-model="filterStatus"
            placeholder="状态"
            clearable
            style="width: 130px"
            @change="handleFilterChange"
          >
            <el-option label="全部" value="" />
            <el-option label="待审核" value="submitted" />
            <el-option label="已通过" value="approved" />
            <el-option label="已驳回" value="rejected" />
          </el-select>
          <el-input
            v-model="searchKeyword"
            placeholder="搜索姓名/手机号/OpenID"
            clearable
            style="width: 240px"
            @keyup.enter="handleFilterChange"
            @clear="handleFilterChange"
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
          <el-button type="primary" @click="handleFilterChange">查询</el-button>
        </div>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="tableData" class="data-table" stripe>
            <el-table-column prop="applicationId" label="申请ID" width="90" />
            <el-table-column prop="applicantName" label="姓名" width="100" />
            <el-table-column prop="phoneMasked" label="手机号" width="130" />
            <el-table-column label="状态" width="100">
              <template #default="{ row }">
                <status-tag :status="row.status" category="volunteerApplication" />
              </template>
            </el-table-column>
            <el-table-column prop="attemptNumber" label="轮次" width="70" />
            <el-table-column prop="createdRescuerId" label="急救员草稿" width="130" />
            <el-table-column label="提交时间" min-width="170">
              <template #default="{ row }">
                {{ formatTime(row.submittedAt) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="90" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="goDetail(row.applicationId)">
                  详情
                </el-button>
              </template>
            </el-table-column>
            <template #empty>
              <el-empty description="暂无申请记录" />
            </template>
          </el-table>
        </div>

        <div class="pagination-container">
          <el-pagination
            v-model:current-page="currentPage"
            :page-size="pageSize"
            :total="total"
            small
            background
            layout="total, prev, pager, next"
            @current-change="handlePageChange"
          />
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getVolunteerApplications } from '@/api/volunteer-applications'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminVolunteerApplicationItem } from '@/types/volunteer-application'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const tableData = ref<AdminVolunteerApplicationItem[]>([])
const total = ref(0)
const currentPage = ref(1)
const pageSize = 20
const filterStatus = ref('')
const searchKeyword = ref('')
const sequencer = createRequestSequencer()
const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)

function syncFromRoute() {
  filterStatus.value = (route.query.status as string) || ''
  searchKeyword.value = (route.query.keyword as string) || ''
  currentPage.value = Number(route.query.page) || 1
}

function buildRouteQuery(): LocationQueryRaw {
  const query: Record<string, string> = {}
  if (filterStatus.value) query.status = filterStatus.value
  if (searchKeyword.value) query.keyword = searchKeyword.value
  if (currentPage.value > 1) query.page = String(currentPage.value)
  return query
}

function routeQueryKey(query: LocationQuery | LocationQueryRaw): string {
  return [firstQueryValue(query.status), firstQueryValue(query.keyword), firstQueryValue(query.page)].join('\n')
}

function firstQueryValue(value: LocationQuery[string] | LocationQueryRaw[string]): string {
  if (Array.isArray(value)) return value[0]?.toString() ?? ''
  return value?.toString() ?? ''
}

async function syncRouteQueryOrLoad() {
  const query = buildRouteQuery()
  if (routeQueryKey(route.query) === routeQueryKey(query)) {
    loadData()
    return
  }
  await router.replace({ query })
}

async function loadData() {
  loading.value = true
  const seq = sequencer.next()
  try {
    const res = await getVolunteerApplications({
      page: currentPage.value,
      pageSize,
      status: filterStatus.value ? filterStatus.value as never : undefined,
      keyword: searchKeyword.value || undefined,
    })
    if (!sequencer.isCurrent(seq)) return
    tableData.value = res.data.data.items
    total.value = res.data.data.total
  } catch (error) {
    if (sequencer.isCurrent(seq)) ElMessage.error(getApiErrorMessage(error, '加载申请列表失败'))
  } finally {
    if (sequencer.isCurrent(seq)) loading.value = false
  }
}

async function handleFilterChange() {
  currentPage.value = 1
  await syncRouteQueryOrLoad()
}

async function handlePageChange(page: number) {
  currentPage.value = page
  await syncRouteQueryOrLoad()
}

function goDetail(id: string) {
  router.push(`/volunteer-applications/${id}`)
}

function startPolling() {
  stopPolling()
  pollTimer.value = setInterval(loadData, 10000)
}

function stopPolling() {
  if (pollTimer.value) clearInterval(pollTimer.value)
  pollTimer.value = null
}

function onVisibilityChange() {
  if (document.hidden) stopPolling()
  else { loadData(); startPolling() }
}

function formatTime(value: string) {
  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}

onMounted(() => {
  syncFromRoute()
  loadData()
  startPolling()
  document.addEventListener('visibilitychange', onVisibilityChange)
})

watch(() => route.query, () => {
  syncFromRoute()
  loadData()
})

onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>
