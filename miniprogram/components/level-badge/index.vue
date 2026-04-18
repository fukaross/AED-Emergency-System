<template>
  <view class="level-badge" :class="`level-badge--${level}`">
    <text class="level-badge-text">{{ label }}</text>
  </view>
</template>

<script setup lang="ts">
/**
 * 急救员级别标签组件
 *
 * 四级：荣誉(金) / 预备(灰) / 正式(绿) / 骨干(蓝)
 *
 * 详细设计见 CLAUDE.md §4.4.2 + GEMINI.md §3.5
 */
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

const label = computed(() => LEVEL_LABELS[props.level])
</script>

<style lang="scss" scoped>
.level-badge {
  display: inline-flex;
  align-items: center;
  height: 36rpx;
  padding: 0 12rpx;
  border-radius: $radius-sm;
}

.level-badge-text {
  font-size: $font-size-xs;
  color: $text-white;
  line-height: $line-height-tight;
}

.level-badge--honor  { background-color: $level-honor; }
.level-badge--trainee { background-color: $level-trainee; }
.level-badge--official { background-color: $level-official; }
.level-badge--core   { background-color: $level-core; }
</style>
