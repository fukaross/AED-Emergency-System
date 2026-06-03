<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar"><h3 class="card-header-title">学校绑定审核</h3></div>
        <div class="filter-bar">
          <el-select v-model="status" placeholder="状态" clearable style="width: 130px" @change="reloadFirst">
            <el-option label="全部" value="" />
            <el-option label="待审核" value="submitted" />
            <el-option label="已通过" value="approved" />
            <el-option label="已驳回" value="rejected" />
          </el-select>
          <el-input v-model="keyword" placeholder="搜索姓名/学院/OpenID" clearable style="width: 240px" @keyup.enter="reloadFirst" @clear="reloadFirst" />
          <el-button type="primary" @click="reloadFirst">查询</el-button>
        </div>
        <div class="table-responsive">
          <el-table v-loading="loading" :data="rows" class="data-table" stripe>
            <el-table-column prop="applicationId" label="申请ID" width="90" />
            <el-table-column prop="applicantName" label="姓名" width="100" />
            <el-table-column prop="college" label="学院" min-width="140" />
            <el-table-column prop="studentId" label="学号" width="130" />
            <el-table-column label="状态" width="100">
              <template #default="{ row }"><status-tag :status="row.status" category="identityApplication" /></template>
            </el-table-column>
            <el-table-column label="提交时间" min-width="170">
              <template #default="{ row }">{{ formatTime(row.submittedAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="90" fixed="right">
              <template #default="{ row }"><el-button text type="primary" size="small" @click="router.push(`/school-bindings/${row.applicationId}`)">详情</el-button></template>
            </el-table-column>
            <template #empty><el-empty description="暂无申请记录" /></template>
          </el-table>
        </div>
        <div class="pagination-container">
          <el-pagination v-model:current-page="page" :page-size="pageSize" :total="total" small background layout="total, prev, pager, next" @current-change="load" />
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getSchoolBindings } from '@/api/user-identity'
import { getApiErrorMessage } from '@/api/client'
import type { AdminSchoolBindingItem, IdentityApplicationStatus } from '@/types/user-identity'

const router = useRouter()
const loading = ref(false)
const rows = ref<AdminSchoolBindingItem[]>([])
const page = ref(1)
const pageSize = 20
const total = ref(0)
const status = ref('')
const keyword = ref('')

async function load() {
  loading.value = true
  try {
    const res = await getSchoolBindings({ page: page.value, pageSize, status: status.value as IdentityApplicationStatus | undefined, keyword: keyword.value || undefined })
    rows.value = res.data.data.items
    total.value = res.data.data.total
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载申请列表失败'))
  } finally {
    loading.value = false
  }
}

function reloadFirst() {
  page.value = 1
  load()
}

function formatTime(value: string) {
  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}

onMounted(load)
</script>
