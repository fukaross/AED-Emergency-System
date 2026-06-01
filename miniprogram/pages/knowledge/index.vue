<template>
  <view class="knowledge" :class="themeClass">
    <!-- 数据展示横幅 -->
    <view class="kn-stats-banner">
      <view
        v-for="item in knowledgeStore.summaryItems"
        :key="item.key"
        class="kn-stat-item"
      >
        <text class="kn-stat-value">{{ item.value }}</text>
        <text class="kn-stat-title">{{ item.title }}</text>
      </view>
    </view>

    <!-- 关键步骤标题 -->
    <view class="kn-section">
      <text class="kn-section-title">关键步骤</text>
    </view>

    <!-- 快速操作入口 -->
    <view class="kn-quick">
      <view
        v-for="item in quickActions"
        :key="item.id"
        class="kn-quick-card"
        @click="handleQuickAction(item)"
      >
        <image class="kn-quick-icon" :src="item.icon" mode="aspectFit" />
        <text class="kn-quick-title">{{ item.title }}</text>
      </view>
    </view>

    <!-- 课程网格 -->
    <view class="kn-section">
      <text class="kn-section-title">课程推荐</text>
    </view>
    <view class="kn-grid">
      <course-card
        v-for="course in courseStore.courseList"
        :key="course.id"
        :title="course.title"
        :thumbnail="course.thumbnail"
        @click="goDetail(course.id)"
      />
    </view>

    <view class="kn-spacer" />

    <AppTabBar current-path="/pages/knowledge/index" />
  </view>
</template>

<script setup lang="ts">
// 急救课堂 —— 统计横幅 + 关键步骤快速操作入口 + 课程网格
import { onShow } from '@dcloudio/uni-app'
import { ref } from 'vue'
import { useTheme } from '@/composables/useTheme'
import { useCourseStore } from '@/store/course'
import { useKnowledgeStore } from '@/store/knowledge'
import { fetchQuickActions } from '@/api/course'
import type { QuickAction } from '@/types/knowledge'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import CourseCard from '@/components/course-card/index.vue'

const courseStore = useCourseStore()
const knowledgeStore = useKnowledgeStore()
const { themeClass } = useTheme()
const quickActions = ref<QuickAction[]>([])

// ===== 快速操作 =====
function handleQuickAction(action: QuickAction) {
  uni.navigateTo({ url: `/pages-sub/knowledge/steps?actionId=${action.id}` })
}

// ===== 课程详情 =====
function goDetail(id: string) {
  uni.navigateTo({ url: `/pages-sub/knowledge/detail?id=${id}` })
}

async function loadQuickActions() {
  try {
    quickActions.value = await fetchQuickActions()
  } catch {
    quickActions.value = []
  }
}

// ===== 生命周期 =====
onShow(() => {
  knowledgeStore.loadSummary()
  courseStore.loadCourseList()
  loadQuickActions()
})
</script>

<style lang="scss" scoped>
.knowledge {
  min-height: 100vh;
  background: $background;
}

// ===== 数据展示横幅 =====
.kn-stats-banner {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  min-height: 164rpx;
  margin: $spacing-sm $spacing-md 0;
  padding: $spacing-lg $spacing-sm;
  border-radius: $radius-lg;
  background: linear-gradient(180deg, $status-info 0%, $level-core 100%);
  box-shadow: 0 8rpx 24rpx rgba(33, 150, 243, 0.24);
}

.kn-stat-item {
  min-width: 0;
  @include flex-column;
  align-items: center;
  justify-content: center;
  gap: $spacing-xs;
}

.kn-stat-value {
  max-width: 100%;
  font-size: $font-size-xxl;
  font-weight: $font-weight-bold;
  line-height: $line-height-tight;
  color: $text-white;
}

.kn-stat-title {
  max-width: 100%;
  font-size: $font-size-sm;
  font-weight: $font-weight-medium;
  line-height: $line-height-tight;
  color: rgba(255, 255, 255, 0.92);
  text-align: center;
}

// ===== 快速操作入口 =====
$quick-card-min-height: 128rpx;
$quick-title-lines: 2;

.kn-quick {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: $spacing-sm;
  padding: $spacing-sm $spacing-md 0;
}

.kn-quick-card {
  min-width: 0;
  min-height: $quick-card-min-height;
  box-sizing: border-box;
  overflow: hidden;
  background: var(--card-red-bg, #FFF5F5);
  border-radius: $radius-md;
  border-left: 4rpx solid $primary;
  @include flex-align-center;
  padding: 0 $spacing-md;
  gap: $spacing-sm;
}

.kn-quick-icon {
  width: 56rpx;
  height: 56rpx;
  flex-shrink: 0;
}

.kn-quick-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  font-size: $font-size-base;
  font-weight: $font-weight-semibold;
  line-height: $line-height-tight;
  color: $text-primary;
  word-break: break-all;
  display: -webkit-box;
  -webkit-line-clamp: $quick-title-lines;
  -webkit-box-orient: vertical;
}

// ===== 课程区块 =====
.kn-section {
  padding: $spacing-md $spacing-md $spacing-xs;
}

.kn-section-title {
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
  color: $text-primary;
}

// ===== 课程网格 =====
.kn-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: $spacing-sm;
  padding: 0 $spacing-md;
}

// ===== 底部占位 =====
.kn-spacer {
  height: calc(#{$tab-bar-height} + env(safe-area-inset-bottom) + #{$spacing-xl});
}
</style>
