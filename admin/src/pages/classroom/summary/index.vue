<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card">
        <div class="card-header-bar">
          <h3 class="card-header-title">课堂统计横幅</h3>
          <el-button type="primary" size="small" :loading="saving" @click="handleSave">保存</el-button>
        </div>

        <el-alert v-if="loadError" :title="loadError" type="error" show-icon :closable="false" />

        <div v-if="items.length" class="summary-preview">
          <div v-for="item in previewItems" :key="item.key" class="summary-preview-item">
            <strong>{{ formatStatValue(item.value) }}</strong>
            <span>{{ item.title }}</span>
          </div>
        </div>

        <el-form label-width="100px">
          <div v-for="(item, index) in items" :key="index" class="stat-form-row">
            <el-row :gutter="16">
              <el-col :span="6">
                <el-form-item label="键名">
                  <el-input v-model="item.key" placeholder="aed_count" />
                </el-form-item>
              </el-col>
              <el-col :span="6">
                <el-form-item label="标题">
                  <el-input v-model="item.title" placeholder="AED数量" />
                </el-form-item>
              </el-col>
              <el-col :span="5">
                <el-form-item label="数值">
                  <el-input-number v-model="item.value" :precision="1" :step="1" controls-position="right" style="width:100%" />
                </el-form-item>
              </el-col>
              <el-col :span="4">
                <el-form-item label="排序">
                  <el-input-number v-model="item.sortOrder" :min="0" controls-position="right" style="width:100%" />
                </el-form-item>
              </el-col>
              <el-col :span="3">
                <el-button style="margin-top:6px" type="danger" text @click="removeItem(index)">删除</el-button>
              </el-col>
            </el-row>
          </div>
        </el-form>

        <el-button text type="primary" @click="addItem">+ 添加统计项</el-button>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { getApiErrorMessage } from '@/api/client'
import adminLayout from '@/components/admin-layout.vue'
import { getKnowledgeSummary, updateKnowledgeSummary } from '@/api/classroom'
import type { ClassroomStatItem } from '@/types/classroom'

const items = ref<ClassroomStatItem[]>([])
const saving = ref(false)
const loadError = ref('')

onMounted(async () => {
  try {
    const res = await getKnowledgeSummary()
    items.value = [...(res.data.data?.items ?? [])]
  } catch (error) {
    loadError.value = getApiErrorMessage(error, '课堂统计横幅加载失败')
    ElMessage.error(loadError.value)
  }
})

const previewItems = computed(() => [...items.value].sort((a, b) => a.sortOrder - b.sortOrder))

function formatStatValue(value: number) {
  return Number.isInteger(value) ? String(value) : value.toFixed(1)
}

function addItem() {
  items.value.push({ key: '', title: '', value: 0, sortOrder: items.value.length + 1 })
}

function removeItem(index: number) {
  items.value.splice(index, 1)
}

async function handleSave() {
  const invalidItem = items.value.find(item => !item.key.trim() || !item.title.trim())
  if (invalidItem) {
    ElMessage.error('键名和标题不能为空')
    return
  }

  saving.value = true
  try {
    await updateKnowledgeSummary({ items: items.value.map((i) => ({ ...i })) })
    ElMessage.success('保存成功')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.stat-form-row {
  margin-bottom: 8px;
}

.summary-preview {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0;
  margin: 16px 0 20px;
  padding: 16px 12px;
  border-radius: 8px;
  background: #1e9af2;
  box-shadow: 0 8px 18px rgba(30, 154, 242, 0.24);
}

.summary-preview-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 0;
  color: #fff;
}

.summary-preview-item strong {
  font-size: 24px;
  line-height: 30px;
}

.summary-preview-item span {
  margin-top: 4px;
  font-size: 13px;
  line-height: 18px;
}
</style>
