<template>
  <div class="welcome-banner">
    <p class="welcome-greeting">{{ greeting }}，{{ displayName }}</p>
    <p class="welcome-meta">{{ dateText }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ username: string | null }>()

const MORNING_END = 11
const AFTERNOON_END = 18
const WEEKDAYS = ['日', '一', '二', '三', '四', '五', '六']

const displayName = computed(() => props.username || '管理员')

const greeting = computed(() => {
  const hour = new Date().getHours()
  if (hour < MORNING_END) return '早上好'
  if (hour < AFTERNOON_END) return '下午好'
  return '晚上好'
})

const dateText = computed(() => {
  const now = new Date()
  return `${now.getFullYear()}年${now.getMonth() + 1}月${now.getDate()}日 星期${WEEKDAYS[now.getDay()]}`
})
</script>

<style scoped lang="scss">
@use '../../../styles/variables' as *;

.welcome-banner {
  padding: 4px 4px 20px;
}

.welcome-greeting {
  margin: 0;
  font-size: $fs-lg;
  font-weight: 400;
  color: $text-primary;
}

.welcome-meta {
  margin: 6px 0 0;
  font-size: $fs-base;
  color: $text-secondary;
}
</style>
