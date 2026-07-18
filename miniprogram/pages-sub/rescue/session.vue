<template>
  <view class="session-page" :class="themeClass">
    <view v-if="errorMessage" class="state-page">
      <text class="state-text">{{ errorMessage }}</text>
      <view class="back-btn" @click="goBack">
        <text class="back-btn-text">返回</text>
      </view>
    </view>
    <view v-else-if="joining" class="state-page">
      <text class="state-text">正在加入会话...</text>
    </view>
    <template v-else>
      <view class="map-wrap" :style="{ height: `${mapHeightVh}vh` }">
        <SessionMap :sos="snapshot?.sos ?? null" :participants="snapshot?.participants ?? []" />
        <SessionLocationHealth :health="locationHealth" @open-settings="openLocationSettings" />
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
import { ref, watch } from 'vue'
import { onHide, onLoad, onShow, onUnload } from '@dcloudio/uni-app'
import { storeToRefs } from 'pinia'
import { useTheme } from '@/composables/useTheme'
import { useSessionLocationReporter } from '@/composables/useSessionLocationReporter'
import { useSessionRealtime } from '@/composables/useSessionRealtime'
import { classifyRescueSessionTerminalError, useRescueSessionStore } from '@/store/rescue-session'
import { useAuthStore } from '@/store/auth'
import type { GeoPoint } from '@/types/common'
import type { RescueMutableEventType } from '@/types/rescue-session'
import SessionMap from './components/session-map.vue'
import SessionLocationHealth from './components/session-location-health.vue'
import SessionMessages from './components/session-messages.vue'
import SessionParticipants from './components/session-participants.vue'
import SessionStatusBar from './components/session-status-bar.vue'
import { SessionResumeController } from './session-resume-controller'
import { useSessionMapResize } from './useSessionMapResize'
const sessionStore = useRescueSessionStore()
const authStore = useAuthStore()
const { snapshot, sessionClosed, terminalHinted } = storeToRefs(sessionStore)
const { isLoggedIn } = storeToRefs(authStore)
const { themeClass } = useTheme()
const helpId = ref('')
const errorMessage = ref('')
const joining = ref(true)
const statusPending = ref(false)
const { mapHeightVh, onDragStart, onDragMove } = useSessionMapResize()
let lifecycleEpoch = 0
let pageVisible = true
let unloaded = false
let accessTerminated = false
let locationReporting = false
let joinPromise: Promise<boolean> | null = null
const reporter = useSessionLocationReporter({
  report: async (point: GeoPoint) => {
    if (sessionClosed.value || accessTerminated) return
    try {
      await sessionStore.reportLocation(point)
    } catch (error) {
      if (!handleTerminalApiError(error, helpId.value)) throw error
    }
  },
  onPermissionDenied: () => {
    uni.showToast({ title: '请开启定位权限', icon: 'none' })
  },
})
const { health: locationHealth } = reporter
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
const resumeController = new SessionResumeController({
  refreshSignals: (now) => sessionStore.refreshParticipantSignals(now),
  refreshSession: (id) => sessionStore.refreshSession(id),
  canResume: (id) => isActivePage(lifecycleEpoch, id) && !accessTerminated,
  startRuntime: () => startSessionRuntime(),
  handleTerminalError: (error, id) => handleTerminalApiError(error, id),
  handleRecoverableError: (error) => {
    uni.showToast({ title: error instanceof Error ? error.message : '会话同步失败', icon: 'none' })
  },
})
watch(isLoggedIn, (loggedIn) => {
  if (loggedIn || !helpId.value || unloaded) return
  terminateSessionAccess(helpId.value, '登录状态已失效，请重新登录')
}, { flush: 'sync' })
onLoad((query = {}) => {
  const id = String(query.id || '')
  if (!id) {
    errorMessage.value = '缺少求助编号'
    joining.value = false
    return
  }
  accessTerminated = false
  helpId.value = id
  void joinAndStart(lifecycleEpoch, id)
})
async function joinAndStart(epoch: number, id: string): Promise<void> {
  joining.value = true
  try {
    joinPromise ??= sessionStore.joinSession(id).finally(() => {
      joinPromise = null
    })
    const joined = await joinPromise
    if (!joined || !isActivePage(epoch, id)) return
    errorMessage.value = ''
    startSessionRuntime()
  } catch (error) {
    if (!isActivePage(epoch, id)) return
    if (handleTerminalApiError(error, id)) return
    errorMessage.value = error instanceof Error ? error.message : '无法进入急救会话'
  } finally {
    if (isActivePage(epoch, id)) joining.value = false
  }
}
onShow(() => {
  pageVisible = true
  const id = helpId.value
  if (!id || accessTerminated) return
  if (!snapshot.value) void joinAndStart(lifecycleEpoch, id)
  else void resumeController.resume(id)
})
onHide(() => {
  pageVisible = false
  lifecycleEpoch += 1
  resumeController.invalidate()
  stopSessionRuntime()
})
onUnload(() => {
  unloaded = true
  pageVisible = false
  lifecycleEpoch += 1
  resumeController.invalidate()
  stopSessionRuntime()
  sessionStore.resetSession(helpId.value)
})
function startSessionRuntime(): void {
  if (accessTerminated) return
  if (sessionClosed.value) {
    closeSessionRuntime()
    return
  }
  realtime.start()
  if (!terminalHinted.value) startLocationReporting()
}
function startLocationReporting(): void {
  if (
    locationReporting ||
    !pageVisible ||
    sessionClosed.value ||
    terminalHinted.value ||
    accessTerminated
  )
    return
  const intervalSeconds = snapshot.value?.timing.locationReportIntervalSeconds
  if (!intervalSeconds) return
  locationReporting = true
  reporter.start(intervalSeconds)
}
function stopSessionRuntime(): void {
  realtime.stop()
  reporter.stop()
  locationReporting = false
}
function closeSessionRuntime(): void {
  resumeController.invalidate()
  stopSessionRuntime()
  sessionStore.clearParticipantLocations()
}
function openLocationSettings(): void {
  uni.openSetting({
    success: () => reporter.retry(),
    fail: () => {
      uni.showToast({ title: '无法打开设置', icon: 'none' })
    },
  })
}
function handleTerminalApiError(error: unknown, id: string): boolean {
  const terminal = classifyRescueSessionTerminalError(error)
  if (!terminal) return false
  if (terminal === 'closed') {
    const applied = sessionStore.confirmSessionClosed(id)
    closeSessionRuntime()
    if (!applied) terminateSessionAccess(id, '求助已结束，急救会话已关闭')
    return true
  }
  terminateSessionAccess(id, '你已无权访问该急救会话')
  return true
}
function terminateSessionAccess(id: string, message: string): void {
  if (accessTerminated) return
  accessTerminated = true
  lifecycleEpoch += 1
  resumeController.invalidate()
  stopSessionRuntime()
  sessionStore.resetSession(id)
  errorMessage.value = message
}
function isActivePage(epoch: number, id: string): boolean {
  return !unloaded && pageVisible && lifecycleEpoch === epoch && helpId.value === id
}
async function handleStatusToggle(eventType: RescueMutableEventType): Promise<void> {
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
function confirmRevoke(eventType: RescueMutableEventType): Promise<boolean> {
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
function goBack(): void {
  uni.navigateBack()
}
</script>
<style lang="scss" scoped src="./session.scss"></style>
