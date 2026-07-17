<template>
  <map
    class="session-map"
    :latitude="center.lat"
    :longitude="center.lng"
    :scale="17"
    :markers="markers"
    :include-points="includePoints"
    :show-location="true"
  />
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import type { GeoPoint } from '@/types/common'
import type { RescueParticipant, RescueSos } from '@/types/rescue-session'
import {
  buildParticipantMarker,
  buildSosMarker,
  interpolateGeo,
  type SessionMapMarker,
} from '@/utils/rescue-markers'

const ANIMATION_DURATION_MS = 900
const ANIMATION_STEPS = 9

const props = defineProps<{
  sos: RescueSos | null
  participants: readonly RescueParticipant[]
}>()

/** 已渲染的参与者位置（openId → 当前地图坐标），插值动画的起点 */
const renderedPositions = ref<Record<string, GeoPoint>>({})
const animationTimers = new Set<ReturnType<typeof setInterval>>()

const center = computed(() => ({
  lat: props.sos?.lat ?? 0,
  lng: props.sos?.lng ?? 0,
}))

const markers = computed<SessionMapMarker[]>(() => {
  const result: SessionMapMarker[] = []
  if (props.sos) result.push(buildSosMarker(props.sos))
  props.participants.forEach((participant, index) => {
    const marker = buildParticipantMarker(participant, index)
    if (!marker) return
    const rendered = renderedPositions.value[participant.openId]
    result.push(
      rendered
        ? { ...marker, latitude: rendered.lat, longitude: rendered.lng }
        : marker,
    )
  })
  return result
})

const includePoints = computed(() => {
  const points: Array<{ latitude: number; longitude: number }> = []
  if (props.sos) points.push({ latitude: props.sos.lat, longitude: props.sos.lng })
  for (const participant of props.participants) {
    if (participant.location) {
      points.push({
        latitude: participant.location.lat,
        longitude: participant.location.lng,
      })
    }
  }
  return points
})

watch(
  () => props.participants,
  (next) => animateToTargets(next),
  { deep: true },
)

function animateToTargets(participants: readonly RescueParticipant[]): void {
  for (const participant of participants) {
    if (!participant.location) {
      delete renderedPositions.value[participant.openId]
      continue
    }
    const from = renderedPositions.value[participant.openId]
    const to: GeoPoint = { lat: participant.location.lat, lng: participant.location.lng }
    if (!from) {
      renderedPositions.value[participant.openId] = to
      continue
    }
    if (from.lat === to.lat && from.lng === to.lng) continue
    startInterpolation(participant.openId, from, to)
  }
}

/** 平滑过渡：900ms 内分 9 步从 from 插值到 to（契约：marker 移动插值动画） */
function startInterpolation(openId: string, from: GeoPoint, to: GeoPoint): void {
  let step = 0
  const timer = setInterval(() => {
    step += 1
    renderedPositions.value = {
      ...renderedPositions.value,
      [openId]: interpolateGeo(from, to, step / ANIMATION_STEPS),
    }
    if (step >= ANIMATION_STEPS) {
      clearInterval(timer)
      animationTimers.delete(timer)
    }
  }, ANIMATION_DURATION_MS / ANIMATION_STEPS)
  animationTimers.add(timer)
}

onUnmounted(() => {
  for (const timer of animationTimers) clearInterval(timer)
  animationTimers.clear()
})
</script>

<style lang="scss" scoped>
.session-map {
  width: 100%;
  height: 100%;
}
</style>
