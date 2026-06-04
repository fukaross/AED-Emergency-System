<template>
  <section class="chart-card" :style="enterStyle">
    <header class="chart-card-head">
      <h3 class="chart-card-title">{{ title }}</h3>
      <span v-if="subtitle" class="chart-card-sub">{{ subtitle }}</span>
    </header>
    <div class="chart-card-body">
      <div v-if="loading" class="chart-state">
        <el-skeleton :rows="4" animated />
      </div>
      <el-empty v-else-if="isEmpty" class="chart-state" description="暂无数据" :image-size="54" />
      <slot v-else />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    loading?: boolean
    isEmpty?: boolean
    enterDelay?: number
  }>(),
  { subtitle: '', loading: false, isEmpty: false, enterDelay: 0 },
)

const enterStyle = computed(() => ({ animationDelay: `${props.enterDelay}ms` }))
</script>

<style scoped lang="scss">
@use '../../../styles/variables' as *;

.chart-card {
  background-color: $card-bg;
  border: $card-border;
  border-radius: $radius-card;
  box-shadow: $card-shadow;
  padding: $card-padding;
  display: flex;
  flex-direction: column;
  animation: chart-fade-in 0.4s ease both;
}

@keyframes chart-fade-in {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: none; }
}

.chart-card-head {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 8px;
}

.chart-card-title {
  margin: 0;
  font-size: $fs-md;
  font-weight: 500;
  color: $text-primary;
}

.chart-card-sub {
  font-size: $fs-sm;
  color: $text-secondary;
}

.chart-card-body {
  height: 300px;

  @media (max-width: ($breakpoint-tablet - 1px)) {
    height: 220px;
  }
}

.chart-state {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}
</style>
