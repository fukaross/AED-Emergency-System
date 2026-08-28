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
          <text class="event-text" :style="{ color: senderAccent(message.senderOpenId) }">{{
            formatRescueMessageText(message)
          }}</text>
          <text class="event-time">{{ formatMessageTime(message.createdAt) }}</text>
        </view>
        <view v-else class="bubble" :style="bubbleStyle(message.senderOpenId)">
          <text
            v-if="message.senderOpenId !== myOpenId"
            class="bubble-sender"
            :style="{ color: senderAccent(message.senderOpenId) }"
            >{{ message.senderName }}</text
          >
          <text
            v-else
            class="bubble-sender bubble-sender--me"
            :style="{ color: senderAccent(message.senderOpenId) }"
            >我</text
          >
          <image
            v-if="message.kind === 'image' && message.imageUrl"
            class="message-image"
            :src="message.imageUrl"
            mode="widthFix"
            @click="previewImage(message.imageUrl)"
          />
          <text v-else class="bubble-text">{{ message.text }}</text>
          <text class="bubble-time">{{ formatMessageTime(message.createdAt) }}</text>
        </view>
      </view>
      <view :id="bottomAnchor" class="message-bottom" />
    </scroll-view>

    <view v-if="showTopButton || showLatestButton" class="scroll-action-bar">
      <view v-if="showTopButton" class="scroll-action" @click="scrollToTop">
        <text class="scroll-action-text">回顶部</text>
      </view>
      <view v-if="showLatestButton" class="scroll-action" @click="scrollToLatest">
        <text class="scroll-action-text">最新</text>
      </view>
    </view>

    <view class="input-bar">
      <view class="image-composer">
        <view
          class="image-add-btn"
          :class="{ 'is-disabled': disabled || sendingImages }"
          @click="toggleImageMenu"
        >
          <text class="image-add-text">+</text>
        </view>
        <view v-if="imageMenuVisible" class="image-menu">
          <view class="image-menu-item" @click="chooseFrom('camera')">
            <text class="image-menu-text">拍照</text>
          </view>
          <view class="image-menu-item" @click="chooseFrom('album')">
            <text class="image-menu-text">相册</text>
          </view>
        </view>
      </view>
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
import { uploadImage } from '@/api/common'
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
  sendImage: (imageUrl: string) => Promise<boolean>
}>()

const draft = ref('')
const sending = ref(false)
const imageMenuVisible = ref(false)
const sendingImages = ref(false)
const MAX_IMAGES_PER_SEND = 3
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

watch(
  bottomAnchor,
  (anchor) => {
    scrollIntoViewTarget.value = anchor
  },
  { immediate: true },
)

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

function toggleImageMenu(): void {
  if (props.disabled || sendingImages.value) return
  imageMenuVisible.value = !imageMenuVisible.value
}

async function chooseFrom(sourceType: 'camera' | 'album'): Promise<void> {
  imageMenuVisible.value = false
  if (props.disabled || sendingImages.value) return
  try {
    const result = await uni.chooseImage({
      count: MAX_IMAGES_PER_SEND,
      sourceType: [sourceType],
      sizeType: ['compressed'],
    })
    const paths = Array.isArray(result.tempFilePaths)
      ? result.tempFilePaths
      : [result.tempFilePaths]
    await sendImages(paths.slice(0, MAX_IMAGES_PER_SEND))
  } catch (error) {
    if (!String((error as { errMsg?: string })?.errMsg || error).includes('cancel')) {
      uni.showToast({ title: '选择图片失败', icon: 'none' })
    }
  }
}

async function sendImages(paths: readonly string[]): Promise<void> {
  if (!paths.length || props.disabled) return
  sendingImages.value = true
  try {
    for (const path of paths) {
      try {
        const uploaded = await uploadImage(path, 'session')
        const sent = await props.sendImage(uploaded.url)
        if (!sent) break
      } catch (error) {
        uni.showToast({
          title: error instanceof Error ? error.message : '图片发送失败',
          icon: 'none',
        })
        break
      }
    }
  } finally {
    sendingImages.value = false
  }
}

function previewImage(url: string): void {
  if (!url) return
  uni.previewImage({ urls: [url] })
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

.image-menu {
  position: absolute;
  left: 0;
  bottom: calc(100% + $spacing-sm);
  z-index: 6;
  display: flex;
  flex-direction: column;
  gap: $spacing-xs;
  min-width: 144rpx;
  padding: $spacing-xs;
  background: $background-white;
  border-radius: $radius-lg;
  box-shadow: $shadow-md;
}

.image-composer {
  position: relative;
  flex-shrink: 0;
}

.image-menu-item {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 64rpx;
  padding: $spacing-xs $spacing-md;
  border-radius: $radius-sm;
  background: $background;
  white-space: nowrap;
}

.image-menu-text {
  color: $text-primary;
  font-size: $font-size-sm;
}

.image-add-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 72rpx;
  height: 72rpx;
  border-radius: $radius-full;
  background: $background;
  color: $primary;
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
}

.image-add-btn.is-disabled {
  opacity: 0.5;
  pointer-events: none;
}

.image-add-text {
  color: $primary;
  font-size: $font-size-xl;
  font-weight: $font-weight-bold;
  line-height: 1;
}

.message-image {
  display: block;
  width: 320rpx;
  max-width: 100%;
  margin: $spacing-xs 0;
  border-radius: $radius-md;
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
