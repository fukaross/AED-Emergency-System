<template>
  <view class="device-center">
    <!-- 加载态 -->
    <view v-if="aedStore.loading" class="dc-loading">
      <text class="dc-loading-text">正在获取附近设备...</text>
    </view>

    <!-- 空态 / 错误态 -->
    <view v-else-if="showEmpty" class="dc-empty">
      <image class="dc-empty-icon" :src="NO_AED_ICON" mode="aspectFit" />
      <text class="dc-empty-title">附近暂无可用的AED设备</text>
      <view class="dc-empty-tips">
        <text class="dc-tip">· 请检查定位权限是否开启</text>
        <text class="dc-tip">· 请检查网络连接</text>
        <text class="dc-tip">· 尝试刷新页面</text>
      </view>
      <text class="dc-empty-warn">⚠️ 如遇紧急情况，请立即拨打</text>
      <text class="dc-empty-phone" @click="call120">120 急救电话</text>
      <text class="dc-empty-link" @click="goGuide">点击查看 急救教程 →</text>
    </view>

    <!-- 正常态 — AED 卡片列表 -->
    <view v-else class="dc-list">
      <aed-card
        v-for="item in aedStore.sortedAedList"
        :id="item.id"
        :key="item.id"
        :name="item.name"
        :distance="item.distance"
        :status="item.status"
        :image="item.image"
        @detail="openDetail"
        @navigate="goMapFocus"
      />
      <view class="dc-spacer" />
    </view>

    <!-- 问题报告浮动按钮 -->
    <view class="fab-report" @click="showReport = true">
      <image class="fab-report-icon" :src="REPORT_ICON" mode="aspectFit" />
    </view>

    <!-- AED 详情弹窗 -->
    <Modal v-model="showDetail" title="AED 设备详情">
      <view v-if="detailItem" class="dt-body">
        <view class="dt-row">
          <text class="dt-label">位置</text>
          <text class="dt-val">{{ detailItem.address }}</text>
        </view>
        <view class="dt-row">
          <text class="dt-label">状态</text>
          <status-tag :online="detailItem.status === 'online'" />
        </view>
        <view class="dt-sep" />
        <text class="dt-section">检修记录</text>
        <view class="dt-row">
          <text class="dt-label">上次检修</text>
          <text class="dt-val">{{ formatDate(detailItem.lastMaintenance) }}</text>
        </view>
        <view class="dt-row">
          <text class="dt-label">下次检修</text>
          <text class="dt-val">{{ formatDate(detailItem.nextMaintenance) }}</text>
        </view>
        <view class="dt-row">
          <text class="dt-label">负责人</text>
          <text class="dt-val">{{ detailItem.maintainer }}</text>
        </view>
      </view>
      <template #footer>
        <view class="dt-footer">
          <view class="dt-btn dt-btn--close" @click="showDetail = false">
            <text>关闭</text>
          </view>
          <view class="dt-btn dt-btn--nav" @click="goMapFocus(detailItem!.id)">
            <text>导航前往</text>
          </view>
        </view>
      </template>
    </Modal>

    <!-- 问题报告底部弹窗 -->
    <AedReportSheet
      v-model="showReport"
      :devices="reportDevices"
      :require-contact="!authStore.isLoggedIn"
      @submit="handleReport"
    />

    <AppTabBar current-path="/pages/device-center/index" />

  </view>
</template>

<script setup lang="ts">
/**
 * 设备中心页面
 *
 * 正常态：按距离排序的 AED 卡片列表（在线优先）
 * 错误态：碎裂心形 + 排错建议 + 急救教程链接
 * 浮动按钮：黄色问题报告按钮 + 浮动动画
 *
 * 设计来源：CLAUDE.md §4.2 + page-Instrument.md
 */
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useAedStore } from '@/store/aed'
import { useAuthStore } from '@/store/auth'
import { submitAedReport } from '@/api/aed'
import { uploadImage } from '@/api/common'
import { formatDate } from '@/utils/format'
import { MAP_CENTER } from '@/utils/constants'
import type { AedItem, AedReportType } from '@/types/aed'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import AedCard from '@/components/aed-card/index.vue'
import StatusTag from '@/components/status-tag/index.vue'
import Modal from '@/components/modal/index.vue'
import AedReportSheet from '@/components/aed-report-sheet/index.vue'

const NO_AED_ICON = '/static/icons/NO_AED.svg'
const REPORT_ICON = '/static/icons/report.svg'

const aedStore = useAedStore()
const authStore = useAuthStore()

const showEmpty = computed(() =>
  !aedStore.loading && aedStore.aedList.length === 0,
)

// ===== 详情弹窗 =====
const showDetail = ref(false)
const detailItem = ref<AedItem | null>(null)

function openDetail(id: string) {
  const found = aedStore.aedList.find(a => a.id === id)
  if (!found) return
  detailItem.value = found
  showDetail.value = true
}

// ===== 问题报告 =====
const showReport = ref(false)
const reportDevices = computed(() =>
  aedStore.aedList.map(a => ({ id: a.id, name: a.name })),
)

interface ReportPayload {
  deviceId: string
  type: AedReportType
  description: string
  images: string[]
  contactPhone?: string
  contactEmail?: string
}

async function handleReport(payload: ReportPayload) {
  try {
    const images = await Promise.all(
      payload.images.map((path) => uploadImage(path, 'report').then((file) => file.url)),
    )

    await submitAedReport(payload.deviceId, {
      type: payload.type,
      description: payload.description,
      images,
      contactPhone: payload.contactPhone,
      contactEmail: payload.contactEmail,
    })
    uni.showToast({ title: '提交成功', icon: 'success' })
    showReport.value = false
  } catch {
    uni.showToast({ title: '提交失败，请重试', icon: 'none' })
  }
}

// ===== 导航 =====
function goMapFocus(id: string) {
  showDetail.value = false
  aedStore.setPendingFocusAed(id)
  uni.switchTab({ url: '/pages/map-home/index' })
}

function call120() {
  uni.makePhoneCall({ phoneNumber: '120' })
}

function goGuide() {
  uni.navigateTo({ url: '/pages-sub/emergency-guide/index' })
}

// ===== 生命周期 =====
onShow(() => {
  const loc = aedStore.userLocation
  aedStore.loadNearbyAed(
    loc?.lat ?? MAP_CENTER.latitude,
    loc?.lng ?? MAP_CENTER.longitude,
  )
})
</script>

<style lang="scss" scoped>
.device-center {
  min-height: 100vh;
  background: $background;
}

// ===== 加载态 =====
.dc-loading { @include flex-center; min-height: 60vh; }
.dc-loading-text { font-size: $font-size-base; color: $text-tertiary; }

// ===== 空态 =====
.dc-empty {
  @include flex-column;
  align-items: center;
  padding: $spacing-xxl $spacing-xl 0;
}

.dc-empty-icon { width: 400rpx; height: 295rpx; margin-bottom: $spacing-lg; }

.dc-empty-title {
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  color: $text-primary;
  margin-bottom: $spacing-md;
}

.dc-empty-tips {
  margin-bottom: $spacing-lg;
  align-self: flex-start;
  padding-left: $spacing-xl;
}

.dc-tip {
  display: block;
  font-size: $font-size-sm;
  color: $text-secondary;
  line-height: $line-height-relaxed;
}

.dc-empty-warn { font-size: $font-size-sm; color: $text-secondary; }

.dc-empty-phone {
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
  color: $primary;
  margin: $spacing-xs 0 $spacing-lg;
}

.dc-empty-link {
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
  color: $primary;
  text-decoration: underline;
}

// ===== 列表 =====
.dc-list { padding-top: $spacing-sm; }

.dc-spacer {
  height: calc(#{$tab-bar-height} + env(safe-area-inset-bottom) + #{$spacing-xl});
}

// ===== 报告浮动按钮 =====
.fab-report {
  position: fixed;
  right: $spacing-lg;
  bottom: calc(#{$tab-bar-height} + env(safe-area-inset-bottom) + #{$spacing-lg});
  width: 96rpx;
  height: 96rpx;
  border-radius: $radius-full;
  box-shadow: $shadow-warning;
  z-index: 100;
  animation: float-updown $duration-cycle $ease-in-out infinite;
}

.fab-report-icon { width: 100%; height: 100%; }

@keyframes float-updown {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10rpx); }
}

// ===== 详情弹窗 =====
.dt-row { @include flex-between; padding: $spacing-xs 0; }
.dt-label { font-size: $font-size-base; color: $text-tertiary; }

.dt-val {
  font-size: $font-size-base;
  color: $text-primary;
  text-align: right;
  flex: 1;
  margin-left: $spacing-sm;
}

.dt-sep { height: 1rpx; background: $divider-light; margin: $spacing-sm 0; }

.dt-section {
  display: block;
  font-size: $font-size-base;
  font-weight: $font-weight-semibold;
  color: $text-primary;
  margin-bottom: $spacing-xs;
}

.dt-footer { @include flex-between; gap: $spacing-sm; padding: $spacing-sm 0; }

.dt-btn {
  flex: 1;
  height: 80rpx;
  border-radius: 40rpx;
  @include flex-center;
  font-size: $font-size-lg;
}

.dt-btn--close { background: $background; color: $text-secondary; }

.dt-btn--nav {
  background: $primary;
  color: $text-white;
  font-weight: $font-weight-bold;
}
</style>
