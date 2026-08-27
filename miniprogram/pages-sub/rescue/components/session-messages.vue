<template>
  <view class="messages-area">
    <scroll-view
      class="message-list"
      scroll-y
      :scroll-into-view="scrollIntoViewTarget"
      :scroll-top="scrollTopTarget ?? undefined"
      :scroll-with-animation="true"
      @scroll="handleScroll"
    >
      <view
        v-for="message in messages"
        :id="`msg-${message.seq}`"
        :key="message.id"
        class="message-row"
        :class="{
          'message-row--mine': message.senderOpenId === myOpenId,
          'message-row--event': message.kind === 'status_event',
        }"
      >
        <view v-if="message.kind === 'status_event'" class="event-line">
          <text
            class="event-text"
            :style="{ color: senderAccent(message.senderOpenId) }"
          >{{ formatRescueMessageText(message) }}</text>
          <text class="event-time">{{ formatMessageTime(message.createdAt) }}</text>
        </view>
        <view v-else class="bubble" :style="bubbleStyle(message.senderOpenId)">
          <text
            v-if="message.senderOpenId !== myOpenId"
            class="bubble-sender"
            :style="{ color: senderAccent(message.senderOpenId) }"
          >{{ message.senderName }}</text>
          <text
            v-else
            class="bubble-sender bubble-sender--me"
            :style="{ color: senderAccent(message.senderOpenId) }"
          >我</text>
          <text class="bubble-text">{{ message.text }}</text>
          <text class="bubble-time">{{ formatMessageTime(message.createdAt) }}</text>
        </view>
      </view>
      <view :id="bottomAnchor" class="message-bottom" />
    </scroll-view>

    <view v-if="showTopButton || showLatestButton" class="scroll-action-bar">
      <view
        v-if="showTopButton"
        class="scroll-action"
        @click="scrollToTop"
      >
        <text class="scroll-action-text">回顶部</text>
      </view>
      <view
        v-if="showLatestButton"
        class="scroll-action"
        @click="scrollToLatest"
      >
        <text class="scroll-action-text">最新</text>
      </view>
    </view>

    <view class="input-bar">
      <input
        v-model="draft"
        class="message-input"
        :maxlength="MESSAGE_TEXT_MAX_LENGTH"
        :disabled="disabled || sending"
        :placeholder="disabled ? '会话已结束' : '发送消息给同行急救员'"
        confirm-type="send"
        @confirm="handleSend"
      />
      <view
        class="send-btn"
        :class="{ 'is-disabled': disabled || sending || !draft.trim() }"
        @click="handleSend"
      >
        <text class="send-btn-text">{{ sending ? '发送中' : '发送' }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { RescueMessage } from '@/types/rescue-session'
import { formatMessageTime } from '@/utils/format'
import { formatRescueMessageText } from '@/utils/rescue-format'
import { MESSAGE_TEXT_MAX_LENGTH } from '@/utils/rescue-session-constants'
import type { ParticipantColor } from '../session-color'
import {
  getMessageScrollAnchor,
  prepareMessageDraft,
  shouldShowLatestButton,
  shouldShowScrollTopButton,
} from './session-message-state'

const props = defineProps<{
  messages: readonly RescueMessage[]
  myOpenId: string
  colorMap: ReadonlyMap<string, ParticipantColor>
  disabled: boolean
  sendMessage: (text: string) => Promise<boolean>
}>()

const draft = ref('')
const sending = ref(false)
const bottomAnchor = computed(() => getMessageScrollAnchor(props.messages))

const SCROLL_BUTTON_THRESHOLD_PX = 80
const scrollIntoViewTarget = ref('')
const scrollTopTarget = ref<number | null>(null)
const scrollTop = ref(0)
const scrollHeight = ref(0)
const clientHeight = ref(0)
const showTopButton = computed(() =>
  shouldShowScrollTopButton(scrollTop.value, SCROLL_BUTTON_THRESHOLD_PX),
)
const showLatestButton = computed(() =>
  shouldShowLatestButton(
    scrollTop.value,
    scrollHeight.value,
    clientHeight.value,
    SCROLL_BUTTON_THRESHOLD_PX,
  ),
)

watch(bottomAnchor, (anchor) => {
  scrollIntoViewTarget.value = anchor
}, { immediate: true })

interface ScrollViewScrollEvent {
  readonly detail: {
    readonly scrollTop?: number
    readonly scrollHeight?: number
    readonly clientHeight?: number
  }
}

function handleScroll(event: ScrollViewScrollEvent): void {
  scrollTop.value = event.detail.scrollTop ?? 0
  scrollHeight.value = event.detail.scrollHeight ?? 0
  clientHeight.value = event.detail.clientHeight ?? 0
}

function scrollToTop(): void {
  scrollTopTarget.value = 0
  scrollIntoViewTarget.value = ''
  setTimeout(() => {
    scrollTopTarget.value = null
  }, 300)
}

function scrollToLatest(): void {
  scrollTopTarget.value = null
  scrollIntoViewTarget.value = bottomAnchor.value
}

const FALLBACK_COLOR: ParticipantColor = {
  background: '#f5f5f5',
  accent: '#9e9e9e',
}

function senderColor(openId: string): ParticipantColor {
  return props.colorMap.get(openId) ?? FALLBACK_COLOR
}

function senderAccent(openId: string): string {
  return senderColor(openId).accent
}

function bubbleStyle(openId: string): Record<string, string> {
  const color = senderColor(openId)
  return {
    backgroundColor: color.background,
    borderLeft: `6rpx solid ${color.accent}`,
  }
}

async function handleSend(): Promise<void> {
  const text = prepareMessageDraft(draft.value)
  if (props.disabled || sending.value || !text) return
  sending.value = true
  try {
    const sent = await props.sendMessage(text)
    if (sent && draft.value.trim() === text) draft.value = ''
  } finally {
    sending.value = false
  }
}
</script>

<style lang="scss" scoped>
.messages-area {
  position: relative;
  display: flex;
  flex-direction: column;
  height: 100%;
  background: $background;
}

.message-list {
  flex: 1;
  height: 0;
  padding: $spacing-sm $spacing-md;
  box-sizing: border-box;
}

.message-row {
  display: flex;
  margin-bottom: $spacing-sm;
}

.message-row--mine {
  justify-content: flex-end;
}

.message-row--event {
  justify-content: center;
}

.event-line {
  display: flex;
  align-items: baseline;
  gap: $spacing-xs;
  max-width: 90%;
}

.event-text {
  color: $text-secondary;
  font-size: $font-size-sm;
}

.event-time {
  color: $text-tertiary;
  font-size: $font-size-xs;
}

.bubble {
  display: flex;
  flex-direction: column;
  max-width: 75%;
  padding: $spacing-sm;
  background: $background-white;
  border-radius: $radius-lg;
  box-shadow: $shadow-sm;
}

.message-row--mine .bubble {
  background: $primary-light;
}

.bubble-sender {
  color: $text-secondary;
  font-size: $font-size-xs;
  margin-bottom: $spacing-xs;
}

.bubble-sender--me {
  font-weight: $font-weight-bold;
}

.bubble-text {
  color: $text-primary;
  font-size: $font-size-base;
  word-break: break-all;
}

.bubble-time {
  align-self: flex-end;
  margin-top: $spacing-xs;
  color: $text-tertiary;
  font-size: $font-size-xs;
}

.scroll-action-bar {
  display: flex;
  justify-content: flex-end;
  gap: $spacing-xs;
  padding: $spacing-xs $spacing-md;
  background: $background-white;
  border-top: 1rpx solid $divider;
}

.scroll-action {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 112rpx;
  height: 48rpx;
  padding: 0 $spacing-sm;
  background: $background;
  border-radius: $radius-pill;
}

.scroll-action-text {
  color: $text-primary;
  font-size: 22rpx;
  font-weight: $font-weight-medium;
}

.input-bar {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  padding: $spacing-sm $spacing-md;
  padding-bottom: calc(#{$spacing-sm} + env(safe-area-inset-bottom));
  background: $background-white;
  border-top: 1rpx solid $divider;
}

.message-input {
  flex: 1;
  height: 72rpx;
  padding: 0 $spacing-sm;
  background: $background;
  border-radius: $radius-pill;
  font-size: $font-size-base;
}

.send-btn {
  @include btn-primary;
  padding-left: $spacing-lg;
  padding-right: $spacing-lg;
}

.send-btn.is-disabled {
  background-color: $background-card-disabled;
  box-shadow: none;
  pointer-events: none;
}

.send-btn-text {
  color: $text-white;
  font-size: $font-size-base;
  font-weight: $font-weight-bold;
}
</style>
