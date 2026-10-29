<template>
  <view class="status-tag" :class="statusClass">
    <view class="status-tag-dot" />
    <text class="status-tag-text">{{ label }}</text>
  </view>
</template>

<script setup lang="ts">
// 状态标签 —— 在线=绿点+可用，离线=灰点+不可用，颜色不作为唯一区分手段
import { computed } from 'vue'

interface Props {
  online: boolean
}

const props = defineProps<Props>()

const statusClass = computed(() =>
  props.online ? 'status-tag--online' : 'status-tag--offline'
)

const label = computed(() =>
  props.online ? '可用' : '不可用'
)
</script>

<style lang="scss" scoped>
.status-tag {
  display: inline-flex;
  align-items: center;
  height: 36rpx;
  padding: 0 12rpx;
  border-radius: $radius-sm;
  gap: 6rpx;
}

.status-tag-dot {
  width: 12rpx;
  height: 12rpx;
  border-radius: $radius-full;
}

.status-tag-text {
  font-size: $font-size-xs;
  line-height: $line-height-tight;
}

.status-tag--online {
  background-color: rgba(76, 175, 80, 0.1);

  .status-tag-dot { background-color: $status-online; }
  .status-tag-text { color: $status-online; }
}

.status-tag--offline {
  background-color: rgba(158, 158, 158, 0.1);

  .status-tag-dot { background-color: $status-offline; }
  .status-tag-text { color: $status-offline; }
}
</style>
