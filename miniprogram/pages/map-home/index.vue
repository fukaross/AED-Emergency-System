<template>
  <view class="map-home">
    <!-- ① 自定义沉浸式导航栏 -->
    <CustomNavbar
      title="REDACTED地图"
      :transparent="true"
      title-align="left"
    />

    <!-- ② 全屏地图 -->
    <map
      id="aedMap"
      class="map-canvas"
      :latitude="mapCenter.latitude"
      :longitude="mapCenter.longitude"
      :scale="mapZoom"
      :min-scale="12"
      :max-scale="20"
      :markers="markers"
      :polyline="polylines"
      :show-location="hasRealLocation"
      @markertap="handleMarkerTap"
      @tap="handleMapTap"
      @error="handleMapError"
    />

    <!-- ③ 寻求帮助浮动按钮（右上）— 红色圆形，图标+文字均在按钮内 -->
    <view
      class="fab-help"
      :class="{ 'is-pressed': helpPressed }"
      :style="fabHelpStyle"
      @click="openHelpPage"
      @touchstart.passive="helpPressed = true"
      @touchend="helpPressed = false"
      @touchcancel="helpPressed = false"
    >
      <image
        class="fab-help__icon"
        :src="EMERGENCY_CALL_ICON"
        mode="aspectFit"
      />
      <text class="fab-help__text">寻求帮助</text>
    </view>

    <!-- ④ 附近 AED 按钮（左下）— 白色胶囊 + 列表图标 -->
    <view
      class="fab-nearby"
      :class="{ 'is-pressed': nearbyPressed }"
      @click="toggleNearbyPanel"
      @touchstart.passive="nearbyPressed = true"
      @touchend="nearbyPressed = false"
      @touchcancel="nearbyPressed = false"
    >
      <image
        class="fab-nearby__icon"
        :src="LIST_ICON"
        mode="aspectFit"
      />
      <text class="fab-nearby__text">附近AED</text>
    </view>

    <!-- ⑤ 定位按钮（右下偏上）— 白色圆形 + 导航箭头 -->
    <view
      class="fab-locate"
      :class="{ 'is-pressed': locatePressed }"
      @click="handleRelocate"
      @touchstart.passive="locatePressed = true"
      @touchend="locatePressed = false"
      @touchcancel="locatePressed = false"
    >
      <image
        class="fab-locate__icon"
        :src="LOCATION_ICON"
        mode="aspectFit"
      />
    </view>

    <!-- ⑥ 最近 AED 按钮（右下）— 红色圆角方形，图标+文字均在按钮内 -->
    <view
      class="fab-nearest"
      :class="{ 'is-pressed': nearestPressed }"
      @click="handleNearest"
      @touchstart.passive="nearestPressed = true"
      @touchend="nearestPressed = false"
      @touchcancel="nearestPressed = false"
    >
      <image
        class="fab-nearest__icon"
        :src="NEAR_AED_ICON"
        mode="aspectFit"
      />
      <text class="fab-nearest__text">最近AED</text>
    </view>

    <!-- ⑦ 状态提示条（定位错误/加载中） -->
    <view v-if="statusMessage && !statusBarDismissed" class="status-bar">
      <text class="status-bar__text">{{ statusMessage }}</text>
      <view class="status-bar__close" @click="statusBarDismissed = true">
        <text class="status-bar__close-icon">✕</text>
      </view>
    </view>

    <!-- ⑧ 加载提示 -->
    <view v-if="isLocating || aedStore.loading" class="loading-tip">
      <text class="loading-tip__text">定位与设备加载中...</text>
    </view>

    <!-- ⑨ 附近 AED 底部面板 -->
    <BottomSheet
      v-model="showNearbyPanel"
      title=""
      max-height="50vh"
      :respect-tab-bar="true"
    >
      <!-- 面板标题栏 -->
      <view class="nearby-header">
        <text class="nearby-header__title">
          附近AED设备 (共{{ aedStore.aedList.length }}台)
        </text>
      </view>
      <!-- 设备列表 -->
      <view
        v-for="aed in aedStore.sortedAedList"
        :key="aed.id"
        class="nearby-item"
      >
        <view class="nearby-item__left">
          <view
            class="nearby-item__dot"
            :class="aed.status === 'online' ? 'dot--online' : 'dot--offline'"
          />
          <view class="nearby-item__info">
            <text class="nearby-item__name">{{ aed.name }}</text>
            <text class="nearby-item__distance">
              {{ (aed.distance ?? 0) > 0 ? (aed.distance ?? 0) + 'm' : '距离未知' }}
            </text>
          </view>
        </view>
        <view class="nearby-item__right">
          <text
            class="nearby-item__status"
            :class="aed.status === 'online' ? 'status--online' : 'status--offline'"
          >
            {{ aed.status === 'online' ? '可用' : '不可用' }}
          </text>
          <view class="nearby-item__nav" @click="navigateToAed(aed)">
            <text class="nearby-item__nav-text">导航</text>
          </view>
        </view>
      </view>
    </BottomSheet>

    <!-- ⑩ AED 设备信息弹窗（替代 uni.showModal，支持地址展示） -->
    <Modal
      v-model="showAedModal"
      title="AED 设备信息"
      cancel-text="关闭"
      confirm-text="导航前往"
      @confirm="handleModalNavigate"
    >
      <view v-if="selectedAed" class="aed-detail">
        <image
          v-if="selectedAed.image"
          class="aed-detail__image"
          :src="selectedAed.image"
          mode="aspectFill"
          @tap="handlePreviewAedImage"
        />
        <text class="aed-detail__name">{{ selectedAed.name }}</text>
        <view v-if="selectedAed.address" class="aed-detail__address">
          <view class="aed-detail__address-icon" />
          <text class="aed-detail__address-text">{{ selectedAed.address }}</text>
        </view>
        <view class="aed-detail__status-row">
          <view
            class="aed-detail__dot"
            :class="selectedAed.status === 'online' ? 'dot--online' : 'dot--offline'"
          />
          <text class="aed-detail__status-text">
            {{ selectedAed.status === 'online' ? '在线可用' : '离线不可用' }}
          </text>
          <text class="aed-detail__distance">
            {{ formatDistance(selectedAed.distance ?? 0) }}
          </text>
        </view>
      </view>
    </Modal>

    <AppTabBar current-path="/pages/map-home/index" />

  </view>
</template>

<script setup lang="ts">
/**
 * AED 急救地图主页
 *
 * 全屏地图 + 4 个浮动控件 + 附近 AED 底部面板
 * 帮助能力为独立页面 pages-sub/help/request，不在本页实现弹窗
 *
 * 设计规格：CLAUDE.md §4.1 + page-Instrument.md
 */
import { computed, ref, onMounted } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import AppTabBar from '@/components/app-tab-bar/index.vue'
import CustomNavbar from '@/components/custom-navbar/index.vue'
import BottomSheet from '@/components/bottom-sheet/index.vue'
import Modal from '@/components/modal/index.vue'
import { useAedMap } from '@/composables/useAedMap'
import { useAedStore } from '@/store/aed'
import {
  createMapCtx,
  openNativeNavigation,
  formatDistance,
  RIPPLE_ID_BASE,
} from '@/services/map-service'
import type { AedItem } from '@/types/aed'
import { MAP_CENTER } from '@/utils/constants'

const EMERGENCY_CALL_ICON = '/static/images/emergency-call.png'
const LIST_ICON = '/static/icons/list.svg'
const LOCATION_ICON = '/static/icons/location.svg'
const NEAR_AED_ICON = '/static/icons/near_AED.svg'

// ===== Store =====
const aedStore = useAedStore()

// ===== Composable（已在 onMounted 中自动 init） =====
const {
  mapCenter,
  mapZoom,
  markers,
  polylines,
  locationError,
  isLocating,
  hasRealLocation,
  isNearestActive,
  focusAedById,
  relocate,
  goToNearestAed,
  clearRoute,
  getActiveNearestAed,
} = useAedMap()

// ===== 本页展示状态 =====
const mapErrorMessage = ref('')
const showNearbyPanel = ref(false)
const navbarBottom = ref(0)
const statusBarDismissed = ref(false)

// ===== AED 信息弹窗状态 =====
const showAedModal = ref(false)
const selectedAed = ref<AedItem | null>(null)

// ===== 按钮按压反馈（:active 在 uni-app 不可靠，用 touch 事件替代） =====
const helpPressed = ref(false)
const nearbyPressed = ref(false)
const locatePressed = ref(false)
const nearestPressed = ref(false)

// ===== 地图上下文 =====
let lastMarkerTapAt = 0

onMounted(() => {
  createMapCtx('aedMap')

  const windowInfo =
    typeof uni.getWindowInfo === 'function'
      ? uni.getWindowInfo()
      : uni.getSystemInfoSync()
  const statusBarHeight = windowInfo.statusBarHeight ?? 0

  let navBarHeight = 44
  // #ifdef MP-WEIXIN
  try {
    const menuBtn = uni.getMenuButtonBoundingClientRect()
    navBarHeight = (menuBtn.top - statusBarHeight) * 2 + menuBtn.height
  } catch { /* fallback */ }
  // #endif

  navbarBottom.value = statusBarHeight + navBarHeight
})

onShow(() => {
  const loc = aedStore.userLocation
  aedStore.loadNearbyAed(
    loc?.lat ?? MAP_CENTER.latitude,
    loc?.lng ?? MAP_CENTER.longitude,
  )
})

// ===== 计算属性 =====
const statusMessage = computed(() => {
  if (mapErrorMessage.value) return mapErrorMessage.value
  if (locationError.value) return locationError.value
  if (aedStore.error) return aedStore.error
  return ''
})

const fabHelpStyle = computed(() => ({
  top: (navbarBottom.value + 10) + 'px',  // 导航栏底部 + 10px 间距
}))

// ===== 事件处理 =====

/** 打开寻求帮助页 */
function openHelpPage(): void {
  uni.navigateTo({ url: '/pages-sub/help/request' })
}

/** 切换附近 AED 面板 */
function toggleNearbyPanel(): void {
  showNearbyPanel.value = !showNearbyPanel.value
}

/** 重新定位 */
async function handleRelocate(): Promise<void> {
  statusBarDismissed.value = false
  await relocate()
  if (hasRealLocation.value) {
    uni.showToast({ title: '已更新当前位置', icon: 'success' })
  }
}

/** 找最近 AED */
async function handleNearest(): Promise<void> {
  await goToNearestAed()
  if (!aedStore.nearestAed) {
    uni.showToast({ title: '未找到可用 AED', icon: 'none' })
  }
}

/** 导航到指定 AED（附近列表项点击） */
function navigateToAed(aed: AedItem): void {
  openNativeNavigation(aed.lat, aed.lng, aed.name, aed.address)
}

/** Marker 点击 — 展示 AED 设备信息弹窗（兼容 H5 callout 不工作的场景） */
function handleMarkerTap(e: { markerId?: number; detail?: { markerId?: number } }): void {
  lastMarkerTapAt = Date.now()
  const markerId = e.markerId ?? e.detail?.markerId
  if (markerId == null) return

  // 涟漪 marker（ID >= RIPPLE_ID_BASE）→ 映射到当前动画对应的 AED
  let aed: AedItem | undefined
  if (markerId >= RIPPLE_ID_BASE) {
    aed = getActiveNearestAed() ?? undefined
  } else {
    aed = aedStore.aedList[markerId]
  }
  if (!aed) return
  const targetAed = aed

  // 自动聚焦缩放到选中的 AED
  focusAedById(targetAed.id)

  // 打开自定义 AED 信息弹窗（展示名称 + 地址 + 状态 + 距离）
  selectedAed.value = targetAed
  showAedModal.value = true
}

/** AED 弹窗点击"导航前往" */
function handleModalNavigate(): void {
  const aed = selectedAed.value
  if (!aed) return
  showAedModal.value = false
  navigateToAed(aed)
}

/** AED 弹窗内图片点击放大预览 */
function handlePreviewAedImage(): void {
  const imageUrl = selectedAed.value?.image
  if (!imageUrl) return
  uni.previewImage({ current: imageUrl, urls: [imageUrl] })
}

/** 点击地图空白区域时，取消最近 AED 高亮与路线 */
function handleMapTap(): void {
  const isMarkerFollowTap = Date.now() - lastMarkerTapAt < 250
  if (isMarkerFollowTap || !isNearestActive.value) {
    return
  }

  clearRoute()
}

/** 地图加载错误 */
function handleMapError(event: Event): void {
  const payload = event as unknown as { detail?: { errMsg?: string } }
  mapErrorMessage.value =
    payload.detail?.errMsg || '地图加载失败，请检查网络后重试'
}
</script>

<style lang="scss" scoped>
// ===== 页面容器 =====
.map-home {
  position: relative;
  width: 100%;
  height: 100vh;
}

// ===== 全屏地图 =====
.map-canvas {
  width: 100%;
  height: 100%;
}

// ===== ③ 寻求帮助浮动按钮（右上） =====
// 设计稿：红色圆形 + 白色电话图标 + 下方"寻求帮助"文字
.fab-help {
  position: fixed;
  right: $spacing-md;
  z-index: 100;
  width: 120rpx;
  height: 120rpx;
  border-radius: $radius-full;
  background-color: $help-button-bg;
  box-shadow: 0 4rpx 16rpx rgba(229, 57, 53, 0.4);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  transition: transform 150ms ease;

  &.is-pressed {
    transform: scale(0.88);
  }

  &__icon {
    width: 56rpx;
    height: 56rpx;
  }

  &__text {
    margin-top: 2rpx;
    font-size: $font-size-xs;
    color: $text-white;
    font-weight: $font-weight-medium;
  }
}

// ===== ④ 附近 AED 按钮（左下） =====
// 设计稿位置：left: 24rpx，bottom: TabBar顶部 + 24rpx
.fab-nearby {
  position: fixed;
  left: $spacing-md; // 24rpx
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom));
  height: 72rpx;
  padding: 0 $spacing-md;
  border-radius: $radius-pill; // 36rpx
  background-color: $background-white;
  box-shadow: 0 2rpx 12rpx rgba(0, 0, 0, 0.15);
  z-index: 100;
  transition: transform 150ms ease;
  @include flex-align-center;

  &.is-pressed {
    transform: scale(0.92);
  }

  &__icon {
    width: 32rpx;
    height: 32rpx;
    margin-right: $spacing-xs; // 8rpx
  }

  &__text {
    font-size: 26rpx;
    color: $text-primary;
    font-weight: $font-weight-medium;
  }
}

// ===== ⑤ 定位按钮（右下偏上） =====
// 设计稿：白色圆形按钮 + 深色导航箭头（location.svg），放大版
// 位于最近AED按钮上方（按钮120rpx + 间距16rpx）
.fab-locate {
  position: fixed;
  right: $spacing-md;
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom) + 120rpx + #{$spacing-sm});
  width: 100rpx;
  height: 100rpx;
  border-radius: $radius-full;
  background-color: $background-white;
  box-shadow: 0 2rpx 12rpx rgba(0, 0, 0, 0.15);
  z-index: 100;
  transition: transform 150ms ease;
  @include flex-center;

  &.is-pressed {
    transform: scale(0.88);
  }

  &__icon {
    width: 64rpx;
    height: 64rpx;
  }
}

// ===== ⑥ 最近 AED 按钮（右下） =====
// 设计稿：红色圆角方形按钮，内含白色AED心形图标 + 白色"最近AED"文字
.fab-nearest {
  position: fixed;
  right: $spacing-md;
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom));
  width: 120rpx;
  height: 120rpx;
  border-radius: $radius-lg; // 16rpx 圆角方形
  background-color: $primary;
  box-shadow: $shadow-primary;
  z-index: 100;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  transition: transform 150ms ease;

  &.is-pressed {
    transform: scale(0.88);
  }

  &__icon {
    width: 52rpx;
    height: 52rpx;
  }

  &__text {
    margin-top: 4rpx;
    font-size: $font-size-xs;
    color: $text-white;
    font-weight: $font-weight-medium;
  }
}

// ===== ⑦ 状态提示条 =====
.status-bar {
  position: fixed;
  right: $spacing-md;
  // 定位按钮上方：tab-bar + nearest(120rpx) + gap + locate(100rpx) + gap
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom) + 120rpx + #{$spacing-sm} + 100rpx + #{$spacing-sm});
  max-width: 480rpx;
  padding: $spacing-sm $spacing-md;
  border-radius: $radius-md;
  background-color: $background-white;
  box-shadow: $shadow-sm;
  z-index: 99;
  display: flex;
  align-items: center;
  gap: $spacing-sm;

  &__text {
    flex: 1;
    font-size: $font-size-sm; // 24rpx
    color: $status-error;
  }

  &__close {
    flex-shrink: 0;
    width: 40rpx;
    height: 40rpx;
    @include flex-center;
  }

  &__close-icon {
    font-size: $font-size-sm;
    color: $text-tertiary;
  }
}

// ===== ⑧ 加载提示 =====
.loading-tip {
  position: fixed;
  top: calc(var(--navbar-bottom, 180rpx) + 110rpx);
  left: 50%;
  transform: translateX(-50%);
  padding: $spacing-xs $spacing-md;
  border-radius: $radius-pill;
  background-color: $background-white;
  box-shadow: $shadow-sm;
  z-index: 99;

  &__text {
    font-size: $font-size-sm;
    color: $text-secondary;
  }
}

// ===== ⑨ 附近 AED 面板内部样式 =====
.nearby-header {
  padding-bottom: $spacing-sm;

  &__title {
    font-size: $font-size-lg; // 30rpx
    font-weight: $font-weight-semibold;
    color: $text-primary;
  }
}

.nearby-item {
  @include flex-between;
  padding: $spacing-sm 0;
  border-bottom: 1rpx solid $divider-light;

  &:last-child {
    border-bottom: none;
  }

  &__left {
    @include flex-align-center;
    flex: 1;
    min-width: 0; // 防止溢出
  }

  &__dot {
    width: 16rpx;
    height: 16rpx;
    border-radius: $radius-full;
    margin-right: $spacing-sm;
    flex-shrink: 0;
  }

  &__info {
    flex: 1;
    min-width: 0;
  }

  &__name {
    font-size: $font-size-base; // 28rpx
    font-weight: $font-weight-medium;
    color: $text-primary;
    @include text-ellipsis;
    display: block;
  }

  &__distance {
    font-size: $font-size-sm; // 24rpx
    color: $text-tertiary;
    margin-top: 4rpx;
    display: block;
  }

  &__right {
    @include flex-align-center;
    flex-shrink: 0;
    margin-left: $spacing-sm;
  }

  &__status {
    font-size: $font-size-xs; // 20rpx
    padding: 0 12rpx;
    height: 36rpx;
    line-height: 36rpx;
    border-radius: $radius-sm;
    margin-right: $spacing-sm;
  }

  &__nav {
    @include btn-mini;
    background-color: $background-white;
    border: 1rpx solid $navigation-blue;
    color: $navigation-blue;
  }

  &__nav-text {
    font-size: $font-size-sm;
    color: $navigation-blue;
  }
}

// 状态色
.dot--online {
  background-color: $status-online;
}
.dot--offline {
  background-color: $status-offline;
}
.status--online {
  background-color: rgba(76, 175, 80, 0.1);
  color: $status-online;
}
.status--offline {
  background-color: rgba(158, 158, 158, 0.1);
  color: $status-offline;
}

// ===== ⑩ AED 设备信息弹窗内容 =====
.aed-detail {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: $spacing-sm;

  &__image {
    width: 100%;
    height: 300rpx;
    border-radius: $radius-md;
    background-color: $background; // 加载占位底色
  }

  &__name {
    font-size: $font-size-xl; // 36rpx
    font-weight: $font-weight-bold;
    color: $text-primary;
    text-align: center;
  }

  &__address {
    display: flex;
    align-items: flex-start;
    max-width: 100%;
  }

  &__address-icon {
    width: 28rpx;
    height: 28rpx;
    flex-shrink: 0;
    margin-top: 2rpx;
    margin-right: $spacing-xs;
    background-image: url('/static/icons/AED-location@3x.png');
    background-position: center;
    background-repeat: no-repeat;
    background-size: contain;
  }

  &__address-text {
    font-size: $font-size-sm; // 24rpx
    color: $text-secondary; // #666
    line-height: $line-height-normal;
    @include multi-line-ellipsis(2);
  }

  &__status-row {
    @include flex-align-center;
    gap: $spacing-xs;
  }

  &__dot {
    width: 16rpx;
    height: 16rpx;
    border-radius: $radius-full;
    flex-shrink: 0;
  }

  &__status-text {
    font-size: $font-size-base; // 28rpx
    color: $text-primary;
  }

  &__distance {
    font-size: $font-size-base;
    color: $text-tertiary;
    margin-left: $spacing-xs;
  }
}
</style>
