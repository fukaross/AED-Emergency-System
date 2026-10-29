<template>
  <div class="rescue-map-wrap">
    <div :id="mapId" class="rescue-map" />
    <div v-if="loading" class="map-state">地图加载中...</div>
    <div v-else-if="error" class="map-state map-state--error">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'
import type { AdminRescueParticipant, RescueSos } from '@/types/rescue-session'
import type { ParticipantColor } from '@/utils/rescue-session-color'
import { useRescueMap } from '@/composables/use-rescue-map'

const props = defineProps<{
  sos: RescueSos | null
  participants: readonly AdminRescueParticipant[]
  colorMap: ReadonlyMap<string, ParticipantColor>
}>()

const mapId = `rescue-map-${Math.random().toString(36).slice(2, 8)}`
const { loading, error, initMap, syncMarkers, destroyMap } = useRescueMap()

onMounted(async () => {
  const center: [number, number] | undefined = props.sos
    ? [props.sos.lng, props.sos.lat]
    : undefined
  await initMap(mapId, center)
  if (props.sos) syncMarkers(props.sos, props.participants, props.colorMap)
})

watch(
  () => [props.sos, props.participants, props.colorMap] as const,
  ([sos, participants, colorMap]) => {
    if (sos) syncMarkers(sos, participants, colorMap)
  },
  { deep: true },
)

onUnmounted(() => {
  destroyMap()
})
</script>

<style scoped lang="scss">
.rescue-map-wrap {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 320px;
}

.rescue-map {
  width: 100%;
  height: 100%;
  min-height: 320px;
  border-radius: 8px;
  overflow: hidden;
}

.map-state {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #909399;
  font-size: 14px;
  background: #f5f7fa;
  border-radius: 8px;
}

.map-state--error {
  color: #f56c6c;
}
</style>
