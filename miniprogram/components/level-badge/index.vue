<template>
  <view class="level-badge" :class="`level-badge--${level}`">
    <image class="level-badge-icon" :src="iconPath" mode="aspectFit" />
    <text class="level-badge-text">{{ label }}</text>
  </view>
</template>

<script setup lang="ts">
// 急救员级别标签 —— 四级：金(荣誉) / 灰(预备) / 绿(正式) / 蓝(骨干)
import { computed } from 'vue'

interface Props {
  level: 'honor' | 'trainee' | 'official' | 'core'
}

const props = defineProps<Props>()

const LEVEL_LABELS: Record<Props['level'], string> = {
  honor: '荣誉急救员',
  trainee: '预备急救员',
  official: '正式急救员',
  core: '骨干急救员',
} as const

const LEVEL_ICONS: Record<Props['level'], string> = {
  honor: '/static/icons/volunteer-honor.svg',
  trainee: '/static/icons/volunteer-prepare.svg',
  official: '/static/icons/volunteer-official.svg',
  core: '/static/icons/volunteer-backbone.svg',
} as const

const label = computed(() => LEVEL_LABELS[props.level])
const iconPath = computed(() => LEVEL_ICONS[props.level])
</script>

<style lang="scss" scoped>
.level-badge {
  display: inline-flex;
  align-items: center;
  height: 40rpx;
  padding: 0 14rpx;
  border-radius: 20rpx;
  background: var(--tag-light-bg, rgba(255, 255, 255, 0.9));
  gap: 6rpx;
}

.level-badge-icon {
  width: 24rpx;
  height: 24rpx;
  flex-shrink: 0;
}

.level-badge-text {
  font-size: $font-size-xs;
  color: $text-primary;
  line-height: $line-height-tight;
  white-space: nowrap;
}

.level-badge--honor  { border: 2rpx solid $level-honor; background: var(--badge-honor-bg, #FFFBE6); }
.level-badge--trainee { border: 2rpx solid $level-trainee; background: $background-card-disabled; }
.level-badge--official { border: 2rpx solid $level-official; background: var(--badge-official-bg, #F6FFED); }
.level-badge--core   { border: 2rpx solid $level-core; background: var(--badge-core-bg, #E6F4FF); }
</style>
