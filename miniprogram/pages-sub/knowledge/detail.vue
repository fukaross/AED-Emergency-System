<template>
  <view v-if="loading" class="detail-page" :class="themeClass">
    <view class="loading-placeholder">
      <text class="loading-text">加载中...</text>
    </view>
  </view>

  <view v-else-if="errorText" class="detail-page" :class="themeClass">
    <view class="error-placeholder">
      <text class="error-text">{{ errorText }}</text>
      <view class="retry-btn" @click="loadCourse">
        <text class="retry-btn-text">重试</text>
      </view>
    </view>
  </view>

  <view v-else-if="course" class="detail-page" :class="themeClass">
    <view v-if="course.type === 'video' && currentVideo" class="video-section">
      <view class="video-wrap">
        <video
          class="video-player"
          :src="currentVideo.url"
          :controls="true"
          :show-fullscreen-btn="true"
          :playsinline="true"
          :show-center-play-btn="true"
          object-fit="contain"
        />
      </view>
      <view v-if="normalizedVideos.length > 1" class="video-list">
        <view
          v-for="video in normalizedVideos"
          :key="video.order"
          class="video-item"
          :class="{ active: video.order === currentVideo.order }"
          @click="selectVideo(video.order)"
        >
          <text class="video-index">{{ video.order }}</text>
          <text class="video-title">{{ video.title }}</text>
        </view>
      </view>
    </view>

    <view v-else-if="course.type === 'video' && course.videoUrl" class="video-wrap">
      <video
        class="video-player"
        :src="course.videoUrl"
        :controls="true"
        :show-fullscreen-btn="true"
        :playsinline="true"
        :show-center-play-btn="true"
        object-fit="contain"
      />
    </view>

    <view v-else class="cover-wrap">
      <image
        class="cover-image"
        :src="course.thumbnail"
        mode="widthFix"
        lazy-load
      />
    </view>

    <view class="info-bar">
      <text class="info-title">{{ course.title }}</text>
      <view class="info-meta">
        <text class="info-date">{{ formatDate(course.createdAt) }}</text>
        <text class="info-divider">|</text>
        <text class="info-views">阅读 {{ formatViews(course.viewCount) }}</text>
      </view>
    </view>

    <view class="content-section">
      <rich-text
        class="rich-content"
        :nodes="course.content"
      />
    </view>

    <view v-if="course.steps.length > 0" class="steps-section">
      <text class="steps-section-title">操作步骤</text>
      <view
        v-for="step in course.steps"
        :key="step.order"
        class="step-item"
      >
        <view class="step-number">{{ step.order }}</view>
        <image
          class="step-image"
          :src="step.image"
          mode="widthFix"
          lazy-load
          @click="previewImage(step.image)"
        />
        <text class="step-title">{{ step.title }}</text>
        <text class="step-desc">{{ step.description }}</text>
        <view v-if="step.keyPoints && step.keyPoints.length" class="step-points">
          <text
            v-for="point in step.keyPoints"
            :key="point"
            class="step-point"
          >{{ point }}</text>
        </view>
      </view>
    </view>

    <view v-if="course.disclaimer" class="disclaimer-bar">
      <text class="disclaimer-text">{{ course.disclaimer }}</text>
    </view>

    <view class="bottom-spacer" />
  </view>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { useTheme } from '@/composables/useTheme'
import { fetchCourseDetail } from '@/api/course'
import { formatDate, formatViews } from '@/utils/format-course'
import type { CourseDetail } from '@/types/user'

const { themeClass } = useTheme()

const course = ref<CourseDetail | null>(null)
const currentVideoOrder = ref(1)
const loading = ref(true)
const errorText = ref<string | null>(null)

const normalizedVideos = computed(() => {
  if (!course.value) return []
  if (course.value.videos.length > 0) return course.value.videos
  if (!course.value.videoUrl) return []
  return [{ order: 1, title: '视频 1', url: course.value.videoUrl }]
})

const currentVideo = computed(() => {
  return normalizedVideos.value.find(v => v.order === currentVideoOrder.value)
    ?? normalizedVideos.value[0]
    ?? null
})

async function loadCourse() {
  const pages = getCurrentPages()
  const current = pages[pages.length - 1]
  const id = (current as { options?: { id?: string } }).options?.id ?? ''

  if (!id) {
    errorText.value = '课程不存在'
    loading.value = false
    return
  }

  loading.value = true
  errorText.value = null
  try {
    const detail = await fetchCourseDetail(id)
    course.value = detail
    currentVideoOrder.value = normalizedVideos.value[0]?.order ?? 1
    uni.setNavigationBarTitle({ title: detail.title })
  } catch (err) {
    errorText.value = (err as Error).message || '加载失败'
  } finally {
    loading.value = false
  }
}

function previewImage(url: string) {
  uni.previewImage({ urls: [url], current: url })
}

function selectVideo(order: number) {
  currentVideoOrder.value = order
}

onLoad(() => {
  loadCourse()
})
</script>

<style src="./detail.scss" lang="scss" scoped></style>
