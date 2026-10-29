<template>
  <view class="step-navigator">
    <view class="step-counter">
      <text class="step-counter-text">
        步骤 {{ current }} / {{ total }}
      </text>
    </view>
    <view class="step-buttons">
      <view
        class="step-btn step-btn--prev"
        :class="{ 'step-btn--disabled': current <= 1 }"
        @click="prev"
      >
        <text>← 上一步</text>
      </view>
      <view
        class="step-btn step-btn--next"
        :class="{ 'step-btn--disabled': current >= total }"
        @click="next"
      >
        <text>下一步 →</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
// 步骤控制器 —— 上一步 + 下一步（红色主按钮），顶部显示 步骤 1/5
interface Props {
  current: number
  total: number
}

const props = defineProps<Props>()

const emit = defineEmits<{
  prev: []
  next: []
}>()

function prev() {
  if (props.current > 1) {
    emit('prev')
  }
}

function next() {
  if (props.current < props.total) {
    emit('next')
  }
}
</script>

<style lang="scss" scoped>
.step-navigator {
  padding: $spacing-md;
}

.step-counter {
  text-align: center;
  margin-bottom: $spacing-sm;
}

.step-counter-text {
  font-size: $font-size-base;
  color: $text-tertiary;
}

.step-buttons {
  @include flex-between;
  gap: $spacing-sm;
}

.step-btn {
  flex: 1;
  height: 88rpx;
  border-radius: 44rpx;
  @include flex-center;
  font-size: $font-size-lg;
  transition: opacity $duration-fast $ease-default;
}

.step-btn--prev {
  background-color: $background;
  color: $text-secondary;
}

.step-btn--next {
  background-color: $primary;
  color: $text-white;
  font-weight: $font-weight-bold;

  &:active {
    background-color: $primary-dark;
  }
}

.step-btn--disabled {
  opacity: 0.4;
  pointer-events: none;
}
</style>
