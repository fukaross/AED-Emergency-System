<template>
  <div class="status-chart">
    <v-chart class="echart" :option="option" autoresize />
    <!-- 中心文案用 DOM 覆盖层定位到饼图圆心（38% / 50%），避免 ECharts title 在带图例布局下偏移 -->
    <div class="status-chart__center">
      <span class="status-chart__total">{{ total }}</span>
      <span class="status-chart__label">总求助</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { ensureEchartsRegistered, buildStatusOption } from '@/utils/chart-theme'
import type { AdminDashboardStatusSlice } from '@/types/dashboard'

ensureEchartsRegistered()

const props = defineProps<{
  slices: AdminDashboardStatusSlice[]
}>()

const total = computed(() => props.slices.reduce((sum, slice) => sum + slice.count, 0))
const option = computed(() => buildStatusOption(props.slices))
</script>

<style scoped>
.status-chart {
  position: relative;
  height: 100%;
  width: 100%;
}

.echart {
  height: 100%;
  width: 100%;
}

.status-chart__center {
  position: absolute;
  left: 38%;
  top: 50%;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  pointer-events: none;
}

.status-chart__total {
  font-size: 28px;
  font-weight: 600;
  line-height: 1.1;
  color: var(--el-text-color-primary, #303133);
}

.status-chart__label {
  font-size: 12px;
  color: var(--el-text-color-secondary, #909399);
}
</style>
