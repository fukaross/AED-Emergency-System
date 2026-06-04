<template>
  <v-chart class="echart" :option="option" autoresize />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { ensureEchartsRegistered, buildProcessingOption } from '@/utils/chart-theme'
import type { AdminDashboardProcessingPoint } from '@/types/dashboard'

ensureEchartsRegistered()

const props = defineProps<{
  points: AdminDashboardProcessingPoint[]
}>()

const option = computed(() => buildProcessingOption(props.points))
</script>

<style scoped>
.echart {
  height: 100%;
  width: 100%;
}
</style>
