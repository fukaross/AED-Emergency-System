<template>
  <v-chart class="echart" :option="option" autoresize />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { ensureEchartsRegistered, buildGrowthLineOption } from '@/utils/chart-theme'
import type { AdminDashboardTrendPoint } from '@/types/dashboard'

ensureEchartsRegistered()

const props = defineProps<{
  points: AdminDashboardTrendPoint[]
  color: string
  seriesName: string
}>()

const option = computed(() => buildGrowthLineOption(props.points, props.color, props.seriesName))
</script>

<style scoped>
.echart {
  height: 100%;
  width: 100%;
}
</style>
