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
  type SessionMapMarker,
} from '@/utils/rescue-markers'
import { createSessionMarkerAnimation } from './session-marker-animation'

const ANIMATION_DURATION_MS = 900
const ANIMATION_STEPS = 9

const props = defineProps<{
  sos: RescueSos | null
  participants: readonly RescueParticipant[]
}>()

/** 已渲染的参与者位置（openId → 当前地图坐标），插值动画的起点 */
const renderedPositions = ref<Record<string, GeoPoint>>({})
const markerAnimation = createSessionMarkerAnimation({
  durationMs: ANIMATION_DURATION_MS,
  steps: ANIMATION_STEPS,
  onPosition: (openId, point) => {
    renderedPositions.value = { ...renderedPositions.value, [openId]: point }
  },
})

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
  const activeOpenIds = new Set(participants.map((participant) => participant.openId))
  for (const openId of Object.keys(renderedPositions.value)) {
    if (!activeOpenIds.has(openId)) removeRenderedPosition(openId)
  }
  for (const participant of participants) {
    if (!participant.location) {
      removeRenderedPosition(participant.openId)
      continue
    }
    const from = renderedPositions.value[participant.openId]
    const to: GeoPoint = { lat: participant.location.lat, lng: participant.location.lng }
    if (!from) {
      markerAnimation.remove(participant.openId)
      renderedPositions.value[participant.openId] = to
      continue
    }
    if (from.lat === to.lat && from.lng === to.lng) continue
    markerAnimation.move(participant.openId, from, to)
  }
}

function removeRenderedPosition(openId: string): void {
  markerAnimation.remove(openId)
  renderedPositions.value = Object.fromEntries(
    Object.entries(renderedPositions.value).filter(([key]) => key !== openId),
  )
}

onUnmounted(() => {
  markerAnimation.dispose()
  renderedPositions.value = {}
})
</script>

<style lang="scss" scoped>
.session-map {
  width: 100%;
  height: 100%;
}
</style>
