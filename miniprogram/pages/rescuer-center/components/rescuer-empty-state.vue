<template>
  <view class="empty-state">
    <image
      class="empty-icon"
      :src="NO_RESCUER_ICON"
      mode="aspectFit"
    />
    <text class="empty-title">附近无急救员</text>
    <view class="empty-tips">
      <text class="empty-tip">请检查定位是否打开和正常</text>
      <text class="empty-tip">如遇紧急情况，请立即拨打120</text>
      <text class="empty-tip">并按照急救教程进行处理</text>
    </view>
    <text class="empty-link" @click="emit('guide')">
      点击查看 急救教程 →
    </text>
    <view class="empty-call" @click="emit('call120')">
      <text class="empty-call-text">拨120</text>
    </view>
  </view>
</template>

<script setup lang="ts">
const NO_RESCUER_ICON = '/static/icons/no-rescuer.svg'

const emit = defineEmits<{
  guide: []
  call120: []
}>()
</script>

<style lang="scss" scoped>
.empty-state {
  @include flex-column;
  align-items: center;
  padding: 120rpx $spacing-xl 0;
}

.empty-icon {
  width: 400rpx;
  height: 300rpx;
  margin-bottom: $spacing-lg;
}

.empty-title {
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  color: $text-primary;
  margin-bottom: $spacing-md;
}

.empty-tips {
  margin-bottom: $spacing-lg;
  align-self: flex-start;
  padding-left: $spacing-xl;
}

.empty-tip {
  display: block;
  font-size: $font-size-sm;
  color: $text-secondary;
  line-height: $line-height-relaxed;
}

.empty-link {
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
  color: $primary;
  text-decoration: underline;
  margin-bottom: $spacing-xl;
}

.empty-call {
  width: 320rpx;
  height: 88rpx;
  border-radius: 44rpx;
  background: $primary;
  @include flex-center;
  box-shadow: $shadow-primary;

  &:active {
    background: $primary-dark;
  }
}

.empty-call-text {
  color: $text-white;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
}
</style>
