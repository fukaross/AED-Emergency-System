<template>
  <view class="session-page" :class="themeClass">
    <view v-if="errorMessage" class="state-page">
      <text class="state-text">{{ errorMessage }}</text>
      <view class="back-btn" @click="goBack">
        <text class="back-btn-text">返回</text>
      </view>
    </view>
    <template v-else>
      <view class="map-wrap" :style="{ height: `${mapHeightVh}vh` }">
        <SessionMap :sos="snapshot?.sos ?? null" :participants="snapshot?.participants ?? []" />
      </view>
      <view class="drag-handle" @touchstart="onDragStart" @touchmove="onDragMove">
        <view class="drag-bar" />
      </view>
      <view class="panel" :style="{ height: `calc(${100 - mapHeightVh}vh - 40rpx)` }">
        <view v-if="sessionClosed" class="closed-banner">
          <text class="closed-banner-text">求助已结束，会话已关闭</text>
        </view>
        <SessionStatusBar
          :me="snapshot?.me ?? null"
          :disabled="sessionClosed"
          :pending="statusPending"
          @toggle="handleStatusToggle"
        />
        <SessionParticipants :participants="snapshot?.participants ?? []" />
        <view class="messages-wrap">
          <SessionMessages
            :messages="snapshot?.messages ?? []"
            :my-open-id="snapshot?.me.openId ?? ''"
            :disabled="sessionClosed"
            :send-message="handleSendMessage"
          />
        </view>
      </view>
    </template>
  </view>
</template>
<script setup lang="ts">
import { ref } from 'vue'
import { onHide, onLoad, onShow, onUnload } from '@dcloudio/uni-app'
import { storeToRefs } from 'pinia'
import { useTheme } from '@/composables/useTheme'
import { useSessionLocationReporter } from '@/composables/useSessionLocationReporter'
import { useSessionRealtime } from '@/composables/useSessionRealtime'
import {
  classifyRescueSessionTerminalError,
  useRescueSessionStore,
} from '@/store/rescue-session'
import type { GeoPoint } from '@/types/common'
import type { RescueEventType } from '@/types/rescue-session'
import SessionMap from './components/session-map.vue'
import SessionMessages from './components/session-messages.vue'
import SessionParticipants from './components/session-participants.vue'
import SessionStatusBar from './components/session-status-bar.vue'
const DEFAULT_MAP_HEIGHT_VH = 45
const MIN_MAP_HEIGHT_VH = 25
const MAX_MAP_HEIGHT_VH = 70
const sessionStore = useRescueSessionStore()
const { snapshot, sessionClosed } = storeToRefs(sessionStore)
const { themeClass } = useTheme()
const helpId = ref('')
const errorMessage = ref('')
const mapHeightVh = ref(DEFAULT_MAP_HEIGHT_VH)
const statusPending = ref(false)
let dragStartY = 0
let dragStartHeight = DEFAULT_MAP_HEIGHT_VH
let windowHeight = 0
let lifecycleEpoch = 0
let pageVisible = true
let unloaded = false
let accessTerminated = false
let locationReporting = false
let joinPromise: Promise<boolean> | null = null
const reporter = useSessionLocationReporter(async (point: GeoPoint) => {
  if (sessionClosed.value || accessTerminated) return
  try {
    await sessionStore.reportLocation(point)
  } catch (error) {
    if (!handleTerminalApiError(error, helpId.value)) throw error
  }
})
const realtime = useSessionRealtime({
  getHelpId: () => helpId.value,
  onEnvelope: (envelope, socketHelpId) => {
    const applied = sessionStore.applySocketEnvelope(socketHelpId, envelope)
    if (applied && sessionClosed.value) closeSessionRuntime()
  },
  onPollRefresh: async (socketHelpId) => {
    try {
      const applied = await sessionStore.refreshSession(socketHelpId)
      if (!applied) return
      if (sessionClosed.value) closeSessionRuntime()
      else if (pageVisible) startLocationReporting()
    } catch (error) {
      if (!handleTerminalApiError(error, socketHelpId)) throw error
    }
  },
  onSignalRefresh: (now) => sessionStore.refreshParticipantSignals(now),
})
onLoad((query = {}) => {
  const id = String(query.id || '')
  if (!id) {
    errorMessage.value = '缺少求助编号'
    return
  }
  accessTerminated = false
  helpId.value = id
  void joinAndStart(lifecycleEpoch, id)
})
async function joinAndStart(epoch: number, id: string): Promise<void> {
  try {
    joinPromise ??= sessionStore.joinSession(id).finally(() => { joinPromise = null })
    const joined = await joinPromise
    if (!joined || !isActivePage(epoch, id)) return
    errorMessage.value = ''
    startSessionRuntime()
  } catch (error) {
    if (!isActivePage(epoch, id)) return
    if (handleTerminalApiError(error, id)) return
    errorMessage.value = error instanceof Error ? error.message : '无法进入急救会话'
  }
}
onShow(() => {
  pageVisible = true
  const id = helpId.value
  if (!id || accessTerminated) return
  if (!snapshot.value) void joinAndStart(lifecycleEpoch, id)
  else void refreshAndResume(lifecycleEpoch, id)
})
onHide(() => {
  pageVisible = false
  lifecycleEpoch += 1
  stopSessionRuntime()
})
onUnload(() => {
  unloaded = true
  pageVisible = false
  lifecycleEpoch += 1
  stopSessionRuntime()
  sessionStore.resetSession(helpId.value)
})
async function refreshAndResume(epoch: number, id: string): Promise<void> {
  try {
    const refreshed = await sessionStore.refreshSession(id)
    if (!refreshed || !isActivePage(epoch, id)) return
    startSessionRuntime()
  } catch (error) {
    if (!isActivePage(epoch, id)) return
    if (handleTerminalApiError(error, id)) return
    uni.showToast({ title: error instanceof Error ? error.message : '会话同步失败', icon: 'none' })
    startSessionRuntime()
  }
}
function startSessionRuntime(): void {
  if (accessTerminated) return
  if (sessionClosed.value) {
    closeSessionRuntime()
    return
  }
  realtime.start()
  startLocationReporting()
}
function startLocationReporting(): void {
  if (locationReporting || !pageVisible || sessionClosed.value || accessTerminated) return
  locationReporting = true
  reporter.start()
}
function stopSessionRuntime(): void {
  realtime.stop()
  reporter.stop()
  locationReporting = false
}
function closeSessionRuntime(): void {
  stopSessionRuntime()
  sessionStore.clearParticipantLocations()
}
function handleTerminalApiError(error: unknown, id: string): boolean {
  const terminal = classifyRescueSessionTerminalError(error)
  if (!terminal) return false
  if (terminal === 'closed') {
    const applied = sessionStore.applySocketEnvelope(id, {
      type: 'help_status',
      data: { helpStatus: 'closed' },
    })
    closeSessionRuntime()
    if (!applied) terminateSessionAccess(id, '求助已结束，急救会话已关闭')
    return true
  }
  terminateSessionAccess(id, '你已无权访问该急救会话')
  return true
}
function terminateSessionAccess(id: string, message: string): void {
  accessTerminated = true
  lifecycleEpoch += 1
  stopSessionRuntime()
  sessionStore.resetSession(id)
  errorMessage.value = message
}
function isActivePage(epoch: number, id: string): boolean {
  return !unloaded && pageVisible && lifecycleEpoch === epoch && helpId.value === id
}
async function handleStatusToggle(eventType: RescueEventType): Promise<void> {
  if (statusPending.value || sessionClosed.value) return
  const me = snapshot.value?.me
  if (!me) return
  const active = eventType === 'taken_aed' ? me.takenAed : me.arrived
  statusPending.value = true
  try {
    if (active) {
      const confirmed = await confirmRevoke(eventType)
      if (!confirmed) return
      await sessionStore.revokeStatus(eventType)
    } else {
      await sessionStore.markStatus(eventType)
    }
  } catch (error) {
    if (handleTerminalApiError(error, helpId.value)) return
    uni.showToast({
      title: error instanceof Error ? error.message : '状态同步失败',
      icon: 'none',
    })
  } finally {
    statusPending.value = false
  }
}
function confirmRevoke(eventType: RescueEventType): Promise<boolean> {
  const label = eventType === 'taken_aed' ? '已拿取 AED' : '已到达现场'
  return new Promise((resolve, reject) => {
    uni.showModal({
      title: '撤销状态',
      content: `确定撤销「${label}」吗？其他参与者会看到一条撤销记录`,
      confirmText: '撤销',
      cancelText: '再想想',
      success: (result) => resolve(result.confirm),
      fail: () => reject(new Error('无法打开撤销确认')),
    })
  })
}
async function handleSendMessage(text: string): Promise<boolean> {
  try {
    return await sessionStore.sendMessage(text)
  } catch (error) {
    if (handleTerminalApiError(error, helpId.value)) return false
    uni.showToast({
      title: error instanceof Error ? error.message : '发送失败',
      icon: 'none',
    })
    return false
  }
}
function onDragStart(event: TouchEvent): void {
  dragStartY = event.touches[0].clientY
  dragStartHeight = mapHeightVh.value
  windowHeight = uni.getWindowInfo().windowHeight
}
function onDragMove(event: TouchEvent): void {
  if (!windowHeight) return
  const deltaVh = ((event.touches[0].clientY - dragStartY) / windowHeight) * 100
  mapHeightVh.value = Math.min(
    MAX_MAP_HEIGHT_VH,
    Math.max(MIN_MAP_HEIGHT_VH, dragStartHeight + deltaVh),
  )
}
function goBack(): void {
  uni.navigateBack()
}
</script>
<style lang="scss" scoped src="./session.scss"></style>
