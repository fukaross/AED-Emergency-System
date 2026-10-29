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
          <el-form-item label="封面图">
            <classroom-image-field
              v-model="form.thumbnail"
              scene="course"
              placeholder="图片 URL 或上传封面图"
            />
          </el-form-item>
          <el-form-item label="分类">
            <el-input v-model="form.category" placeholder="emergency" required />
          </el-form-item>
          <template v-if="form.type === 'video'">
            <h4 class="section-title">课程视频</h4>
            <course-video-list-field v-model="form.videos" />
          </template>

          <el-row :gutter="16">
            <el-col :span="6"><el-form-item label="置顶"><el-switch v-model="form.pinned" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="排序"><el-input-number v-model="form.sortOrder" :min="0" controls-position="right" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item></el-col>
            <el-col :span="6"><el-form-item label="学习时间"><el-input v-model="form.estimatedTime" placeholder="约5分钟" /></el-form-item></el-col>
          </el-row>

          <h4 class="section-title">正文内容</h4>
          <tiptap-editor v-model="form.content" />

          <h4 class="section-title" style="margin-top:24px">课程步骤内容</h4>
          <div v-for="(step, idx) in form.steps" :key="idx" class="step-edit-card">
            <div class="step-header-row">
              <span class="step-label">步骤 {{ idx + 1 }}</span>
              <div class="step-actions">
                <el-button text type="primary" size="small" :disabled="idx === 0" @click="moveStep(idx, -1)">上移</el-button>
                <el-button text type="primary" size="small" :disabled="idx === form.steps.length - 1" @click="moveStep(idx, 1)">下移</el-button>
                <el-button text type="danger" size="small" @click="removeStep(idx)">删除此步</el-button>
              </div>
            </div>
            <el-form-item label="标题"><el-input v-model="step.title" /></el-form-item>
            <el-form-item label="描述"><el-input v-model="step.description" type="textarea" :rows="2" /></el-form-item>
            <el-form-item label="图片">
              <classroom-image-field
                v-model="step.image"
                scene="course"
                placeholder="图片 URL 或上传步骤图"
              />
            </el-form-item>
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
import ClassroomImageField from '@/components/classroom-image-field.vue'
import CourseVideoListField from '@/components/course-video-list-field.vue'
import TiptapEditor from '@/components/tiptap-editor/index.vue'
import { getCourseDetail, createCourse, updateCourse, deleteCourse } from '@/api/classroom'
import { getApiErrorMessage } from '@/api/client'
import type { CourseStep, CourseVideo, CreateCoursePayload, UpdateCoursePayload } from '@/types/classroom'

const route = useRoute()
const router = useRouter()
const saving = ref(false)
const id = computed(() => route.params.id as string)
const isCreate = computed(() => id.value === 'new')

interface StepEdit extends CourseStep { keyPointsStr: string }
type VideoEdit = CourseVideo
const form = ref({
  title: '',
  thumbnail: '',
  type: 'article' as 'article' | 'video',
  category: '',
  content: '<p></p>',
  videos: [] as VideoEdit[],
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
        videos: normalizeVideos(d.videos, d.videoUrl),
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

function moveStep(idx: number, direction: -1 | 1) {
  const nextIndex = idx + direction
  if (nextIndex < 0 || nextIndex >= form.value.steps.length) return
  const [step] = form.value.steps.splice(idx, 1)
  form.value.steps.splice(nextIndex, 0, step)
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

function mapVideos(): CourseVideo[] {
  return form.value.videos
    .map((video, idx) => ({
      order: idx + 1,
      title: video.title.trim() || `视频 ${idx + 1}`,
      url: video.url.trim(),
    }))
    .filter(video => video.url)
}

function normalizeVideos(videos: CourseVideo[] | undefined, videoUrl: string | null): CourseVideo[] {
  if (videos?.length) {
    return videos.map((video, idx) => ({
      order: idx + 1,
      title: video.title || `视频 ${idx + 1}`,
      url: video.url,
    }))
  }
  return videoUrl ? [{ order: 1, title: '视频 1', url: videoUrl }] : []
}

async function handleSave() {
  if (!form.value.title.trim()) {
    ElMessage.error('课程标题不能为空')
    return
  }
  if (!form.value.category.trim()) {
    ElMessage.error('课程分类不能为空')
    return
  }
  const videos = form.value.type === 'video' ? mapVideos() : []
  if (form.value.type === 'video' && videos.length === 0) {
    ElMessage.error('视频课程至少需要一个视频')
    return
  }

  saving.value = true
  const payload: CreateCoursePayload | UpdateCoursePayload = {
    title: form.value.title.trim(),
    type: form.value.type,
    category: form.value.category.trim(),
    content: form.value.content,
    thumbnail: form.value.thumbnail.trim(),
    disclaimer: form.value.disclaimer,
    pinned: form.value.pinned,
    sortOrder: form.value.sortOrder,
    isEnabled: form.value.isEnabled,
    estimatedTime: form.value.estimatedTime.trim(),
    steps: mapSteps(),
    videoUrl: form.value.type === 'video' ? videos[0].url : null,
    videos,
  }
  try {
    if (isCreate.value) {
      await createCourse(payload as CreateCoursePayload)
    } else {
      await updateCourse(id.value, payload as UpdateCoursePayload)
    }
    ElMessage.success('保存成功')
    router.push('/classroom/courses')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  try {
    await deleteCourse(id.value)
    ElMessage.success('已删除')
    router.push('/classroom/courses')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '删除失败'))
  }
}
</script>

<style scoped>
.section-title { margin: 20px 0 12px; font-size: 16px; }
.step-edit-card { border: 1px solid #ebeef5; border-radius: 8px; padding: 16px; margin-bottom: 12px; }
.step-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.step-actions { display: flex; gap: 4px; align-items: center; }
.step-label { font-weight: 500; }
</style>
