<template>
  <view class="login-lock">
    <image
      class="login-lock-icon"
      :src="LOCK_ICON"
      mode="aspectFit"
    />
    <text class="login-lock-text">登录后查看附近急救员</text>
    <view class="login-lock-btn" @click="emit('login')">
      <text>立即登录</text>
    </view>
  </view>
</template>

<script setup lang="ts">
const LOCK_ICON = '/static/icons/unverified-rescuer.svg'

const emit = defineEmits<{
  login: []
}>()
</script>

<style lang="scss" scoped>
.login-lock {
  @include flex-column;
  align-items: center;
  padding-top: 200rpx;
}

.login-lock-icon {
  width: 200rpx;
  height: 200rpx;
  margin-bottom: $spacing-lg;
}

.login-lock-text {
  font-size: $font-size-lg;
  color: $text-secondary;
  margin-bottom: $spacing-xl;
}

.login-lock-btn {
  width: 320rpx;
  height: 88rpx;
  border-radius: 44rpx;
  background: $primary;
  color: $text-white;
  font-size: $font-size-lg;
  font-weight: $font-weight-bold;
  @include flex-center;
  box-shadow: $shadow-primary;

  &:active {
    background: $primary-dark;
  }
}
</style>
