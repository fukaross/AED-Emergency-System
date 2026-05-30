<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card">
        <div class="card-header-bar">
          <h3 class="card-header-title">课堂统计横幅</h3>
          <el-button type="primary" size="small" :loading="saving" @click="handleSave">保存</el-button>
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
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import { getKnowledgeSummary, updateKnowledgeSummary } from '@/api/classroom'
import type { ClassroomStatItem } from '@/types/classroom'

const items = ref<ClassroomStatItem[]>([])
const saving = ref(false)

onMounted(async () => {
  try {
    const res = await getKnowledgeSummary()
    items.value = [...(res.data.data?.items ?? [])]
  } catch {
    ElMessage.error('加载失败')
  }
})

function addItem() {
  items.value.push({ key: '', title: '', value: 0, sortOrder: items.value.length + 1 })
}

function removeItem(index: number) {
  items.value.splice(index, 1)
}

async function handleSave() {
  saving.value = true
  try {
    await updateKnowledgeSummary({ items: items.value.map((i) => ({ ...i })) })
    ElMessage.success('保存成功')
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.stat-form-row {
  margin-bottom: 8px;
}
</style>
