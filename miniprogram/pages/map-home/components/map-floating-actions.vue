<template>
  <view class="map-floating-actions">
    <view
      class="fab-help"
      :class="{ 'is-pressed': pressedStates.help }"
      :style="fabHelpStyle"
      @click="emit('open-help')"
      @touchstart.passive="setPressed('help', true)"
      @touchend="setPressed('help', false)"
      @touchcancel="setPressed('help', false)"
    >
      <image
        class="fab-help__icon"
        :src="EMERGENCY_CALL_ICON"
        mode="aspectFit"
      />
      <text class="fab-help__text">寻求帮助</text>
    </view>

    <view
      class="fab-nearby"
      :class="{ 'is-pressed': pressedStates.nearby }"
      @click="emit('toggle-nearby')"
      @touchstart.passive="setPressed('nearby', true)"
      @touchend="setPressed('nearby', false)"
      @touchcancel="setPressed('nearby', false)"
    >
      <image
        class="fab-nearby__icon"
        :src="LIST_ICON"
        mode="aspectFit"
      />
      <text class="fab-nearby__text">附近AED</text>
    </view>

    <view
      class="fab-locate"
      :class="{ 'is-pressed': pressedStates.locate }"
      @click="emit('relocate')"
      @touchstart.passive="setPressed('locate', true)"
      @touchend="setPressed('locate', false)"
      @touchcancel="setPressed('locate', false)"
    >
      <image
        class="fab-locate__icon"
        :src="LOCATION_ICON"
        mode="aspectFit"
      />
    </view>

    <view
      class="fab-nearest"
      :class="{ 'is-pressed': pressedStates.nearest }"
      @click="emit('nearest')"
      @touchstart.passive="setPressed('nearest', true)"
      @touchend="setPressed('nearest', false)"
      @touchcancel="setPressed('nearest', false)"
    >
      <image
        class="fab-nearest__icon"
        :src="NEAR_AED_ICON"
        mode="aspectFit"
      />
      <text class="fab-nearest__text">最近AED</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

const DEFAULT_NAVBAR_HEIGHT = 44
const HELP_TOP_GAP_PX = 10
const EMERGENCY_CALL_ICON = '/static/images/emergency-call.png'
const LIST_ICON = '/static/icons/list.svg'
const LOCATION_ICON = '/static/icons/location.svg'
const NEAR_AED_ICON = '/static/icons/near_AED.svg'

type PressedKey = 'help' | 'nearby' | 'locate' | 'nearest'

const emit = defineEmits<{
  'open-help': []
  'toggle-nearby': []
  relocate: []
  nearest: []
}>()

const navbarBottom = ref(0)
const pressedStates = reactive<Record<PressedKey, boolean>>({
  help: false,
  nearby: false,
  locate: false,
  nearest: false,
})

const fabHelpStyle = computed(() => ({
  top: `${navbarBottom.value + HELP_TOP_GAP_PX}px`,
}))

onMounted(() => {
  navbarBottom.value = resolveNavbarBottom()
})

function resolveNavbarBottom(): number {
  const windowInfo =
    typeof uni.getWindowInfo === 'function'
      ? uni.getWindowInfo()
      : uni.getSystemInfoSync()
  const statusBarHeight = windowInfo.statusBarHeight ?? 0
  let navBarHeight = DEFAULT_NAVBAR_HEIGHT

  // #ifdef MP-WEIXIN
  try {
    const menuBtn = uni.getMenuButtonBoundingClientRect()
    navBarHeight = (menuBtn.top - statusBarHeight) * 2 + menuBtn.height
  } catch {
    navBarHeight = DEFAULT_NAVBAR_HEIGHT
  }
  // #endif

  return statusBarHeight + navBarHeight
}

function setPressed(key: PressedKey, value: boolean): void {
  pressedStates[key] = value
}
</script>

<style lang="scss" scoped>
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

.fab-nearby {
  position: fixed;
  left: $spacing-md;
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom));
  height: 72rpx;
  padding: 0 $spacing-md;
  border-radius: $radius-pill;
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
    margin-right: $spacing-xs;
  }

  &__text {
    font-size: 26rpx;
    color: $text-primary;
    font-weight: $font-weight-medium;
  }
}

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

.fab-nearest {
  position: fixed;
  right: $spacing-md;
  bottom: calc(#{$tab-bar-height} + #{$spacing-md} + env(safe-area-inset-bottom));
  width: 120rpx;
  height: 120rpx;
  border-radius: $radius-lg;
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
</style>
