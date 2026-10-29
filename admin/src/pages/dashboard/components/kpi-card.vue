<template>
  <div
    :class="['kpi-card', { 'is-clickable': clickable }]"
    :style="accentVars"
    @click="onClick"
  >
    <span class="kpi-bar" />
    <span class="kpi-icon">
      <el-icon><component :is="icon" /></el-icon>
    </span>
    <span class="kpi-label">{{ label }}</span>
    <span class="kpi-value">{{ value }}</span>
    <span class="kpi-sub">{{ sub }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'

const props = withDefaults(
  defineProps<{
    label: string
    value: number | string
    sub?: string
    accent: string
    icon: Component
    clickable?: boolean
  }>(),
  { sub: '', clickable: false },
)

const emit = defineEmits<{ click: [] }>()

const accentVars = computed(() => ({ '--kpi-accent': props.accent }))

function onClick() {
  if (props.clickable) emit('click')
}
</script>

<style scoped lang="scss">
@use '../../../styles/variables' as *;

.kpi-card {
  position: relative;
  background-color: $card-bg;
  border: $card-border;
  border-radius: $radius-card;
  box-shadow: $card-shadow;
  padding: $card-padding;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: transform 0.25s ease, box-shadow 0.25s ease;
}

// 顶部细色条传递语义色
.kpi-bar {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background-color: var(--kpi-accent);
}

.kpi-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
  font-size: 20px;
  color: var(--kpi-accent);
  background-color: color-mix(in srgb, var(--kpi-accent) 14%, #ffffff);
}

.kpi-label {
  font-size: $fs-base;
  color: $text-secondary;
}

.kpi-value {
  margin-top: 8px;
  font-size: $fs-num;
  font-weight: 600;
  line-height: 1;
  color: $text-primary;
}

.kpi-sub {
  margin-top: 10px;
  font-size: $fs-sm;
  color: $text-secondary;
}

.is-clickable {
  cursor: pointer;

  &:hover {
    transform: translateY(-3px);
    box-shadow: $card-shadow-hover;
  }
}
</style>
