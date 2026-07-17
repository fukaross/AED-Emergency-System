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
          @toggle="handleStatusToggle"
        />
        <SessionParticipants :participants="snapshot?.participants ?? []" />
        <view class="messages-wrap">
          <SessionMessages
            :messages="snapshot?.messages ?? []"
            :my-open-id="snapshot?.me.openId ?? ''"
            :disabled="sessionClosed"
            @send="handleSendMessage"
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
import { useRescueSessionStore } from '@/store/rescue-session'
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

let dragStartY = 0
let dragStartHeight = DEFAULT_MAP_HEIGHT_VH
let windowHeight = 0

const realtime = useSessionRealtime({
  getHelpId: () => helpId.value,
  onEnvelope: (envelope) => sessionStore.applySocketEnvelope(envelope),
  onPollRefresh: async () => {
    await sessionStore.refreshSession(helpId.value)
  },
})

const reporter = useSessionLocationReporter(async (point: GeoPoint) => {
  if (!sessionClosed.value) {
    await sessionStore.reportLocation(point)
  }
})

onLoad((query = {}) => {
  const id = String(query.id || '')
  if (!id) {
    errorMessage.value = '缺少求助编号'
    return
  }
  helpId.value = id
  void joinAndStart()
})

async function joinAndStart(): Promise<void> {
  try {
    await sessionStore.joinSession(helpId.value)
    realtime.start()
    reporter.start()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '无法进入急救会话'
  }
}

onShow(() => {
  if (snapshot.value && !sessionClosed.value) {
    realtime.start()
    reporter.start()
  }
})

onHide(() => {
  realtime.stop()
  reporter.stop()
})

onUnload(() => {
  realtime.stop()
  reporter.stop()
  sessionStore.resetSession()
})

async function handleStatusToggle(eventType: RescueEventType): Promise<void> {
  const me = snapshot.value?.me
  if (!me) return
  const active = eventType === 'taken_aed' ? me.takenAed : me.arrived
  try {
    if (active) {
      const confirmed = await confirmRevoke(eventType)
      if (!confirmed) return
      await sessionStore.revokeStatus(eventType)
    } else {
      await sessionStore.markStatus(eventType)
    }
  } catch (error) {
    uni.showToast({
      title: error instanceof Error ? error.message : '状态同步失败',
      icon: 'none',
    })
  }
}

function confirmRevoke(eventType: RescueEventType): Promise<boolean> {
  const label = eventType === 'taken_aed' ? '已拿取 AED' : '已到达现场'
  return new Promise((resolve) => {
    uni.showModal({
      title: '撤销状态',
      content: `确定撤销「${label}」吗？其他参与者会看到一条撤销记录`,
      confirmText: '撤销',
      cancelText: '再想想',
      success: (result) => resolve(result.confirm),
      fail: () => resolve(false),
    })
  })
}

async function handleSendMessage(text: string): Promise<void> {
  try {
    await sessionStore.sendMessage(text)
  } catch (error) {
    uni.showToast({
      title: error instanceof Error ? error.message : '发送失败',
      icon: 'none',
    })
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

<style lang="scss" scoped>
.session-page {
  min-height: 100vh;
  background: $background;
}

.state-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-top: 30vh;
  gap: $spacing-lg;
}

.state-text {
  color: $text-secondary;
  font-size: $font-size-base;
}

.back-btn {
  @include btn-secondary;
}

.back-btn-text {
  color: $primary;
  font-size: $font-size-base;
}

.map-wrap {
  width: 100%;
  overflow: hidden;
}

.drag-handle {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 40rpx;
  background: $background-white;
  touch-action: none;
}

.drag-bar {
  width: 80rpx;
  height: 8rpx;
  border-radius: $radius-pill;
  background: $divider;
}

.panel {
  display: flex;
  flex-direction: column;
  background: $background-white;
}

.closed-banner {
  padding: $spacing-sm $spacing-md;
  background: $primary-light;
}

.closed-banner-text {
  color: $primary-dark;
  font-size: $font-size-sm;
}

.messages-wrap {
  flex: 1;
  min-height: 0;
}
</style>
