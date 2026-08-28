<template>
  <map
    class="session-map"
    :latitude="center.lat"
    :longitude="center.lng"
    :scale="17"
    :markers="markers"
    :include-points="includePoints"
    :show-location="true"
    @markertap="handleMarkerTap"
  />
  <canvas
    :id="AVATAR_MARKER_CANVAS_ID"
    :canvas-id="AVATAR_MARKER_CANVAS_ID"
    class="avatar-marker-canvas"
  />
</template>

<script setup lang="ts">
import { computed, getCurrentInstance, onMounted, onUnmounted, ref, watch } from 'vue'
import type { GeoPoint } from '@/types/common'
import type { RescueParticipant, RescueSos } from '@/types/rescue-session'
import {
  buildParticipantMarker,
  buildSosMarker,
  SOS_MARKER_ID,
  type SessionMapMarker,
} from '@/utils/rescue-markers'
import type { ParticipantColor } from '../session-color'
import {
  AVATAR_MARKER_CANVAS_ID,
  getAvatarMarkerIcon,
} from '../session-avatar-marker'
import { createSessionMarkerAnimation } from './session-marker-animation'

const ANIMATION_DURATION_MS = 900
const ANIMATION_STEPS = 9

const props = defineProps<{
  sos: RescueSos | null
  participants: readonly RescueParticipant[]
  colorMap: ReadonlyMap<string, ParticipantColor>
}>()

const emit = defineEmits<{
  (event: 'sos-tap'): void
}>()

const instance = getCurrentInstance()
const avatarIcons = ref<Record<string, string>>({})
const NORMAL_AVATAR_MARKER_SIZE = 44
const AED_AVATAR_MARKER_SIZE = 52
const avatarCanvasReady = ref(false)
const disposed = ref(false)

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
    const avatarPath = avatarIcons.value[participant.openId]
    result.push({
      ...(rendered
        ? { ...marker, latitude: rendered.lat, longitude: rendered.lng }
        : marker),
      iconPath: avatarPath || marker.iconPath,
      width: participant.takenAed ? AED_AVATAR_MARKER_SIZE : NORMAL_AVATAR_MARKER_SIZE,
      height: participant.takenAed ? AED_AVATAR_MARKER_SIZE : NORMAL_AVATAR_MARKER_SIZE,
    })
  })
  return result
})

function handleMarkerTap(event: { markerId?: number; detail?: { markerId?: number } }): void {
  const markerId = event.markerId ?? event.detail?.markerId
  if (markerId === SOS_MARKER_ID) emit('sos-tap')
}

function renderAvatarIcons(participants: readonly RescueParticipant[]): void {
  if (!avatarCanvasReady.value || disposed.value) return
  for (const participant of participants) {
    const color = props.colorMap.get(participant.openId)
    if (!color || participant.signal === 'gone' || !participant.location) continue
      getAvatarMarkerIcon(participant.avatarUrl, color.accent, instance)
        .then((path) => {
        if (disposed.value || avatarIcons.value[participant.openId] === path) return
        avatarIcons.value = { ...avatarIcons.value, [participant.openId]: path }
      })
      .catch(() => {
        // 头像合成失败时保留默认 marker 图标
      })
  }
}

watch(
  () => [props.participants, props.colorMap] as const,
  ([participants]) => renderAvatarIcons(participants),
  { immediate: true, deep: true },
)

onMounted(() => {
  avatarCanvasReady.value = true
  renderAvatarIcons(props.participants)
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
  disposed.value = true
  markerAnimation.dispose()
  renderedPositions.value = {}
})
</script>

<style lang="scss" scoped>
.session-map {
  width: 100%;
  height: 100%;
}

.avatar-marker-canvas {
  position: fixed;
  left: -9999px;
  top: 0;
  width: 100px;
  height: 100px;
}
</style>
