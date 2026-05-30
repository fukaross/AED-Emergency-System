<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card no-padding">
        <div class="card-header-bar">
          <h3 class="card-header-title">课程管理</h3>
          <el-button type="primary" size="small" @click="goCreate">+ 新增课程</el-button>
        </div>

        <div class="filter-bar">
          <el-select v-model="filterCategory" placeholder="分类" clearable style="width:160px" @change="handleFilterChange">
            <el-option label="全部" value="" />
            <el-option label="emergency" value="emergency" />
            <el-option label="first-aid" value="first-aid" />
          </el-select>
          <el-input v-model="searchKeyword" placeholder="搜索标题" clearable style="width:200px" @keyup.enter="handleFilterChange" @clear="handleFilterChange" />
          <el-button type="primary" @click="handleFilterChange">查询</el-button>
        </div>

        <div class="table-responsive">
          <el-table v-loading="loading" :data="tableData" class="data-table" stripe>
            <el-table-column prop="id" label="ID" width="100" />
            <el-table-column prop="title" label="标题" min-width="160" />
            <el-table-column label="类型" width="80">
              <template #default="{ row }">{{ row.type === 'video' ? '视频' : '图文' }}</template>
            </el-table-column>
            <el-table-column prop="category" label="分类" width="100" />
            <el-table-column label="置顶" width="70">
              <template #default="{ row }">{{ row.pinned ? '●' : '-' }}</template>
            </el-table-column>
            <el-table-column prop="sortOrder" label="排序" width="70" />
            <el-table-column label="启用" width="70">
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

        <div class="pagination-container">
          <el-pagination v-model:current-page="currentPage" :page-size="pageSize" :total="total" small background layout="total, prev, pager, next" @current-change="handlePageChange" />
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import statusTag from '@/components/status-tag.vue'
import { getCourses, deleteCourse } from '@/api/classroom'
import type { AdminCourseItem } from '@/types/classroom'

const router = useRouter()
const loading = ref(false)
const tableData = ref<AdminCourseItem[]>([])
const total = ref(0)
const currentPage = ref(1)
const pageSize = 20

const filterCategory = ref<string>('')
const searchKeyword = ref<string>('')

let timer: ReturnType<typeof setInterval> | null = null

async function load(page?: number) {
  loading.value = true
  try {
    const res = await getCourses({
      page: page ?? currentPage.value,
      pageSize,
      category: filterCategory.value || undefined,
      keyword: searchKeyword.value || undefined,
    })
    const d = res.data.data!
    tableData.value = d.items
    total.value = d.total
    currentPage.value = d.page
  } catch { /* ignore */ }
  finally { loading.value = false }
}

function handleFilterChange() { currentPage.value = 1; load(1) }
function handlePageChange(p: number) { load(p) }

function goCreate() { router.push('/classroom/courses/new') }
function goDetail(id: string) { router.push(`/classroom/courses/${id}`) }

async function handleDelete(row: AdminCourseItem) {
  try {
    await deleteCourse(row.id)
    ElMessage.success('已删除')
    await load(currentPage.value)
  } catch {
    ElMessage.error('删除失败')
  }
}

onMounted(() => { load(); timer = setInterval(() => load(currentPage.value), 10000) })
onUnmounted(() => { if (timer) clearInterval(timer) })
</script>

<style scoped>
.card-header-bar { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-bottom: 1px solid #ebeef5; }
.card-header-title { font-size: 16px; font-weight: 500; margin: 0; }
.filter-bar { display: flex; gap: 12px; padding: 12px 20px; align-items: center; flex-wrap: wrap; }
.table-responsive { padding: 0 20px 20px; }
.pagination-container { display: flex; justify-content: flex-end; padding: 0 20px 16px; }
</style>
