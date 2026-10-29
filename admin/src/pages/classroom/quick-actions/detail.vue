<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card">
        <el-button text @click="router.back()">← 返回列表</el-button>
        <h3 class="section-title">{{ isCreate ? '新增关键步骤' : '编辑关键步骤' }}</h3>

        <el-form label-width="100px" :model="form">
          <el-form-item label="标题" required>
            <el-input v-model="form.title" placeholder="心肺复苏" />
          </el-form-item>
          <el-form-item label="图标" required>
            <classroom-image-field
              v-model="form.icon"
              scene="quick-action"
              placeholder="图片 URL 或上传图标"
            />
          </el-form-item>
          <el-form-item label="简介">
            <el-input v-model="form.summary" type="textarea" :rows="2" placeholder="简短描述" />
          </el-form-item>
          <el-form-item label="排序">
            <el-input-number v-model="form.sortOrder" :min="0" />
          </el-form-item>
          <el-form-item label="启用">
            <el-switch v-model="form.isEnabled" />
          </el-form-item>
        </el-form>

        <h4 class="section-title">滚动步骤内容</h4>
        <div v-for="(step, idx) in form.steps" :key="idx" class="step-edit-card">
          <div class="step-header-row">
            <span class="step-label">滚动段落 {{ idx + 1 }}</span>
            <div class="step-actions">
              <el-button text type="primary" size="small" :disabled="idx === 0" @click="moveStep(idx, -1)">上移</el-button>
              <el-button text type="primary" size="small" :disabled="idx === form.steps.length - 1" @click="moveStep(idx, 1)">下移</el-button>
              <el-button text type="danger" size="small" @click="removeStep(idx)">删除此段</el-button>
            </div>
          </div>
          <el-form-item label="标题">
            <el-input v-model="step.title" placeholder="步骤标题" />
          </el-form-item>
          <el-form-item label="描述">
            <el-input v-model="step.description" type="textarea" :rows="2" />
          </el-form-item>
          <el-form-item label="图片">
            <classroom-image-field
              v-model="step.image"
              scene="quick-action"
              placeholder="图片 URL 或上传步骤图"
            />
          </el-form-item>
          <el-form-item label="要点(每行一个)">
            <el-input v-model="step.keyPointsStr" type="textarea" :rows="3" placeholder="轻拍重唤&#10;观察胸部起伏" />
          </el-form-item>
        </div>
        <el-button text type="primary" @click="addStep">+ 添加步骤</el-button>

        <div style="margin-top:24px">
          <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
          <el-button v-if="!isCreate" type="danger" text @click="handleDelete">删除</el-button>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import ClassroomImageField from '@/components/classroom-image-field.vue'
import { getQuickActionDetail, createQuickAction, updateQuickAction, deleteQuickAction } from '@/api/classroom'
import { getApiErrorMessage } from '@/api/client'
import type { QuickActionStep } from '@/types/classroom'

const route = useRoute()
const router = useRouter()
const saving = ref(false)
const id = computed(() => route.params.id as string)
const isCreate = computed(() => id.value === 'new')

interface StepEdit extends QuickActionStep { keyPointsStr: string }
const form = ref({
  title: '',
  icon: '',
  summary: '',
  sortOrder: 0,
  isEnabled: true,
  steps: [] as StepEdit[],
})

onMounted(async () => {
  if (!isCreate.value) {
    try {
      const res = await getQuickActionDetail(id.value)
      const d = res.data.data!
      form.value = {
        title: d.title,
        icon: d.icon,
        summary: d.summary,
        sortOrder: d.sortOrder,
        isEnabled: d.isEnabled,
        steps: (d.steps ?? []).map(s => ({ ...s, keyPointsStr: (s.keyPoints ?? []).join('\n') })),
      }
    } catch {
      ElMessage.error('加载失败')
    }
  }
})

function addStep() {
  form.value.steps.push({ order: form.value.steps.length + 1, title: '', description: '', image: '', keyPoints: [], keyPointsStr: '' })
}

function removeStep(idx: number) {
  form.value.steps.splice(idx, 1)
}

function moveStep(idx: number, direction: -1 | 1) {
  const nextIndex = idx + direction
  if (nextIndex < 0 || nextIndex >= form.value.steps.length) return
  const [step] = form.value.steps.splice(idx, 1)
  form.value.steps.splice(nextIndex, 0, step)
}

function mapSteps(): QuickActionStep[] {
  return form.value.steps.map((s, idx) => ({
    order: idx + 1,
    title: s.title,
    description: s.description,
    image: s.image,
    keyPoints: s.keyPointsStr.split('\n').filter(Boolean),
  }))
}

async function handleSave() {
  if (!form.value.title.trim()) {
    ElMessage.error('关键步骤标题不能为空')
    return
  }
  if (!form.value.icon.trim()) {
    ElMessage.error('关键步骤图标不能为空')
    return
  }

  saving.value = true
  const payload = {
    title: form.value.title.trim(),
    icon: form.value.icon.trim(),
    summary: form.value.summary,
    sortOrder: form.value.sortOrder,
    isEnabled: form.value.isEnabled,
    steps: mapSteps(),
  }
  try {
    if (isCreate.value) {
      await createQuickAction(payload)
    } else {
      await updateQuickAction(id.value, payload)
    }
    ElMessage.success('保存成功')
    router.push('/classroom/quick-actions')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  try {
    await deleteQuickAction(id.value)
    ElMessage.success('已删除')
    router.push('/classroom/quick-actions')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '删除失败'))
  }
}
</script>

<style scoped>
.section-title { margin: 16px 0 12px; font-size: 16px; }
.step-edit-card { border: 1px solid #ebeef5; border-radius: 8px; padding: 16px; margin-bottom: 12px; }
.step-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.step-actions { display: flex; gap: 4px; align-items: center; }
.step-label { font-weight: 500; }
</style>
