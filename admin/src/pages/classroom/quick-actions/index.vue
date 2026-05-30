<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">关键步骤管理</h3>
          <el-button type="primary" size="small" @click="goCreate">+ 新增关键步骤</el-button>
        </div>

        <div class="table-responsive">
          <el-alert v-if="loadError" :title="loadError" type="error" show-icon :closable="false" class="table-alert" />
          <el-table v-loading="loading" :data="tableData" class="data-table" stripe>
            <el-table-column prop="id" label="ID" width="80" />
            <el-table-column label="图标" width="92">
              <template #default="{ row }">
                <el-image
                  v-if="row.icon"
                  :src="row.icon"
                  :preview-src-list="[row.icon]"
                  fit="cover"
                  class="icon-preview"
                />
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column prop="title" label="标题" min-width="160" />
            <el-table-column prop="sortOrder" label="排序" width="80" />
            <el-table-column label="状态" width="80">
              <template #default="{ row }">
                <status-tag :status="row.isEnabled ? 'online' : 'offline'" category="device" />
              </template>
            </el-table-column>
            <el-table-column label="操作" width="160" fixed="right">
              <template #default="{ row }">
                <el-button text type="primary" size="small" @click="goDetail(row.id)">编辑</el-button>
                <el-popconfirm title="确定删除？" confirm-button-text="删除" cancel-button-text="取消" @confirm="handleDelete(row)">
                  <template #reference>
                    <el-button text type="danger" size="small">删除</el-button>
                  </template>
                </el-popconfirm>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { getApiErrorMessage } from '@/api/client'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getQuickActions, deleteQuickAction } from '@/api/classroom'
import type { AdminQuickActionItem } from '@/types/classroom'

const router = useRouter()
const loading = ref(false)
const tableData = ref<AdminQuickActionItem[]>([])
const loadError = ref('')

let timer: ReturnType<typeof setInterval> | null = null
let firstLoadFinished = false

async function load(showError = false) {
  loading.value = true
  try {
    const res = await getQuickActions()
    tableData.value = res.data.data ?? []
    loadError.value = ''
  } catch (error) {
    if (!firstLoadFinished || showError) {
      loadError.value = getApiErrorMessage(error, '关键步骤加载失败')
      ElMessage.error(loadError.value)
    }
  } finally {
    firstLoadFinished = true
    loading.value = false
  }
}

function goCreate() { router.push('/classroom/quick-actions/new') }
function goDetail(id: string) { router.push(`/classroom/quick-actions/${id}`) }

async function handleDelete(row: AdminQuickActionItem) {
  try {
    await deleteQuickAction(row.id)
    ElMessage.success('已删除')
    await load(true)
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '删除失败'))
  }
}

onMounted(() => { load(); timer = setInterval(load, 10000) })
onUnmounted(() => { if (timer) clearInterval(timer) })
</script>

<style scoped>
.card-header-bar { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-bottom: 1px solid #ebeef5; }
.card-header-title { font-size: 16px; font-weight: 500; margin: 0; }
.table-responsive { padding: 0 20px 20px; }
.table-alert { margin: 12px 0; }
.icon-preview { width: 44px; height: 44px; border-radius: 4px; border: 1px solid #ebeef5; }
</style>
