<template>
  <admin-layout>
    <div class="page-content-padding">
      <div class="admin-card">
        <el-button text @click="router.back()">← 返回列表</el-button>
        <h3 class="section-title">{{ isCreate ? '新增课程' : '编辑课程' }}</h3>

        <el-form label-width="120px" :model="form">
          <el-row :gutter="16">
            <el-col :span="16">
              <el-form-item label="标题" required>
                <el-input v-model="form.title" placeholder="AED 使用教程" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="类型">
                <el-select v-model="form.type" style="width:100%">
                  <el-option label="图文" value="article" />
                  <el-option label="视频" value="video" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="封面图URL">
            <el-input v-model="form.thumbnail" placeholder="/static/images/xxx.jpg" />
          </el-form-item>
          <el-form-item label="分类">
            <el-input v-model="form.category" placeholder="emergency" required />
          </el-form-item>
          <el-form-item v-if="form.type === 'video'" label="视频URL">
            <el-input v-model="form.videoUrl" placeholder="https://..." />
          </el-form-item>

          <el-row :gutter="16">
            <el-col :span="6"><el-form-item label="置顶"><el-switch v-model="form.pinned" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="排序"><el-input-number v-model="form.sortOrder" :min="0" controls-position="right" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="学习时间"><el-input v-model="form.estimatedTime" placeholder="约5分钟" /></el-form-item></el-col>
          </el-row>

          <h4 class="section-title">正文内容</h4>
          <tiptap-editor v-model="form.content" />

          <h4 class="section-title" style="margin-top:24px">步骤列表</h4>
          <div v-for="(step, idx) in form.steps" :key="idx" class="step-edit-card">
            <div class="step-header-row">
              <span class="step-label">步骤 {{ idx + 1 }}</span>
              <el-button text type="danger" size="small" @click="removeStep(idx)">删除此步</el-button>
            </div>
            <el-form-item label="标题"><el-input v-model="step.title" /></el-form-item>
            <el-form-item label="描述"><el-input v-model="step.description" type="textarea" :rows="2" /></el-form-item>
            <el-form-item label="图片URL"><el-input v-model="step.image" /></el-form-item>
            <el-form-item label="要点(每行一个)"><el-input v-model="step.keyPointsStr" type="textarea" :rows="2" /></el-form-item>
          </div>
          <el-button text type="primary" @click="addStep">+ 添加步骤</el-button>

          <h4 class="section-title" style="margin-top:24px">免责声明</h4>
          <el-input v-model="form.disclaimer" type="textarea" :rows="2" placeholder="急救内容仅用于校园培训与现场参考。" />

          <div style="margin-top:32px">
            <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
            <el-button v-if="!isCreate" type="danger" text @click="handleDelete">删除</el-button>
          </div>
        </el-form>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import adminLayout from '@/components/admin-layout.vue'
import TiptapEditor from '@/components/tiptap-editor/index.vue'
import { getCourseDetail, createCourse, updateCourse, deleteCourse } from '@/api/classroom'
import type { CourseStep } from '@/types/classroom'

const route = useRoute()
const router = useRouter()
const saving = ref(false)
const id = computed(() => route.params.id as string)
const isCreate = computed(() => id.value === 'new')

interface StepEdit extends CourseStep { keyPointsStr: string }
const form = ref({
  title: '',
  thumbnail: '',
  type: 'article' as 'article' | 'video',
  category: '',
  content: '<p></p>',
  videoUrl: '',
  disclaimer: '',
  pinned: false,
  sortOrder: 0,
  isEnabled: true,
  estimatedTime: '',
  steps: [] as StepEdit[],
})

onMounted(async () => {
  if (!isCreate.value) {
    try {
      const res = await getCourseDetail(id.value)
      const d = res.data.data!
      form.value = {
        title: d.title,
        thumbnail: d.thumbnail,
        type: d.type,
        category: d.category,
        content: d.content,
        videoUrl: d.videoUrl ?? '',
        disclaimer: d.disclaimer ?? '',
        pinned: d.pinned,
        sortOrder: d.sortOrder,
        isEnabled: d.isEnabled,
        estimatedTime: d.estimatedTime ?? '',
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

function mapSteps(): CourseStep[] {
  return form.value.steps.map((s, idx) => ({
    order: idx + 1,
    title: s.title,
    description: s.description,
    image: s.image,
    keyPoints: s.keyPointsStr.split('\n').filter(Boolean),
  }))
}

async function handleSave() {
  saving.value = true
  const payload: Record<string, unknown> = {
    title: form.value.title,
    type: form.value.type,
    category: form.value.category,
    content: form.value.content,
    thumbnail: form.value.thumbnail,
    disclaimer: form.value.disclaimer,
    pinned: form.value.pinned,
    sortOrder: form.value.sortOrder,
    isEnabled: form.value.isEnabled,
    estimatedTime: form.value.estimatedTime,
    steps: mapSteps(),
    videoUrl: form.value.type === 'video' ? form.value.videoUrl : null,
  }
  try {
    if (isCreate.value) {
      await createCourse(payload as never)
    } else {
      await updateCourse(id.value, payload as never)
    }
    ElMessage.success('保存成功')
    router.push('/classroom/courses')
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  try {
    await deleteCourse(id.value)
    ElMessage.success('已删除')
    router.push('/classroom/courses')
  } catch {
    ElMessage.error('删除失败')
  }
}
</script>

<style scoped>
.section-title { margin: 20px 0 12px; font-size: 16px; }
.step-edit-card { border: 1px solid #ebeef5; border-radius: 8px; padding: 16px; margin-bottom: 12px; }
.step-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.step-label { font-weight: 500; }
</style>
