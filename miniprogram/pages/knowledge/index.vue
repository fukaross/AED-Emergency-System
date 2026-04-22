<template>
  <view class="knowledge">
    <!-- 轮播横幅 -->
    <view class="kn-banner-wrap">
      <swiper
        class="kn-banner"
        :autoplay="true"
        :interval="SWIPER_INTERVAL"
        :circular="true"
        indicator-dots
        indicator-color="rgba(255,255,255,0.4)"
        indicator-active-color="#FFFFFF"
      >
        <swiper-item
          v-for="banner in courseStore.banners"
          :key="banner.id"
          @click="handleBannerClick(banner)"
        >
          <image
            class="kn-banner-img"
            :src="banner.image"
            mode="aspectFill"
          />
        </swiper-item>
      </swiper>
    </view>

    <!-- 关键步骤标题 -->
    <view class="kn-section">
      <text class="kn-section-title">关键步骤</text>
    </view>

    <!-- 快速操作入口 -->
    <view class="kn-quick">
      <view
        v-for="item in QUICK_ACTIONS"
        :key="item.type"
        class="kn-quick-card"
        @click="handleQuickAction(item.type)"
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
/**
 * 急救课堂页面
 *
 * 轮播横幅 + 快速操作入口（CPR/海姆立克）+ 课程网格
 * 前两格固定：AED使用教程、心肺复苏教程
 *
 * 设计来源：CLAUDE.md §4.3 + page-Instrument.md
 */
import { onShow } from '@dcloudio/uni-app'
import { useCourseStore } from '@/store/course'
import { fetchCourseByType } from '@/api/course'
import type { BannerItem } from '@/types/common'
import type { CourseTypeKey } from '@/types/user'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import CourseCard from '@/components/course-card/index.vue'

const SWIPER_INTERVAL = 5000

const QUICK_ACTIONS: readonly {
  type: CourseTypeKey
  title: string
  icon: string
}[] = [
  { type: 'cpr', title: '心肺复苏', icon: '/static/images/icon-cpr.png' },
  { type: 'heimlich', title: '海姆立克急救法', icon: '/static/images/icon-heimlich.jpg' },
]

const courseStore = useCourseStore()

// ===== 轮播点击 =====
function handleBannerClick(banner: BannerItem) {
  switch (banner.linkType) {
    case 'course':
      uni.navigateTo({ url: `/pages-sub/knowledge/detail?id=${banner.linkTarget}` })
      break
    case 'url':
      uni.navigateTo({
        url: `/pages-sub/webview/index?url=${encodeURIComponent(banner.linkTarget)}`,
      })
      break
    case 'page':
      uni.navigateTo({ url: banner.linkTarget })
      break
    case 'none':
    default:
      break
  }
}

// ===== 快速操作 =====
async function handleQuickAction(type: CourseTypeKey) {
  try {
    const res = await fetchCourseByType(type)
    if (res.entryMode === 'detail') {
      uni.navigateTo({ url: `/pages-sub/knowledge/detail?id=${res.id}` })
    } else {
      uni.navigateTo({ url: `/pages-sub/knowledge/steps?type=${type}` })
    }
  } catch {
    // API 未就绪时回退到步骤页
    uni.navigateTo({ url: `/pages-sub/knowledge/steps?type=${type}` })
  }
}

// ===== 课程详情 =====
function goDetail(id: string) {
  uni.navigateTo({ url: `/pages-sub/knowledge/detail?id=${id}` })
}

// ===== 生命周期 =====
onShow(() => {
  courseStore.loadBanners()
  courseStore.loadCourseList()
})
</script>

<style lang="scss" scoped>
.knowledge {
  min-height: 100vh;
  background: $background;
}

// ===== 轮播横幅 =====
.kn-banner-wrap { padding: $spacing-sm $spacing-md 0; }

.kn-banner {
  height: 300rpx;
  border-radius: $radius-lg;
  overflow: hidden;
}

.kn-banner-img {
  width: 100%;
  height: 100%;
  border-radius: $radius-lg;
}

// ===== 快速操作入口 =====
.kn-quick {
  display: flex;
  gap: $spacing-sm;
  padding: $spacing-sm $spacing-md 0;
}

.kn-quick-card {
  flex: 1;
  height: 120rpx;
  background: #FFF5F5;
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
  font-size: $font-size-base;
  font-weight: $font-weight-semibold;
  color: $text-primary;
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
