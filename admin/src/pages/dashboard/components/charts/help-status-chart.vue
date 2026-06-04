<template>
  <v-chart class="echart" :option="option" autoresize />
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

const option = computed(() => buildStatusOption(props.slices))
</script>

<style scoped>
.echart {
  height: 100%;
  width: 100%;
}
</style>
