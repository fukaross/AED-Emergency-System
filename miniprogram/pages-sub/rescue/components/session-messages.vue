<template>
  <view class="messages-area">
    <scroll-view class="message-list" scroll-y :scroll-into-view="lastMessageAnchor">
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
          <text class="event-text">{{ formatRescueMessageText(message) }}</text>
          <text class="event-time">{{ formatRelativeTime(message.createdAt) }}</text>
        </view>
        <view v-else class="bubble">
          <text v-if="message.senderOpenId !== myOpenId" class="bubble-sender">
            {{ message.senderName }}
          </text>
          <text class="bubble-text">{{ message.text }}</text>
          <text class="bubble-time">{{ formatRelativeTime(message.createdAt) }}</text>
        </view>
      </view>
      <view :id="bottomAnchor" class="message-bottom" />
    </scroll-view>

    <view class="input-bar">
      <input
        v-model="draft"
        class="message-input"
        :disabled="disabled"
        :placeholder="disabled ? '会话已结束' : '发送消息给同行急救员'"
        confirm-type="send"
        @confirm="handleSend"
      >
      <view class="send-btn" :class="{ 'is-disabled': disabled || !draft.trim() }" @click="handleSend">
        <text class="send-btn-text">发送</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import type { RescueMessage } from '@/types/rescue-session'
import { formatRelativeTime } from '@/utils/format'
import { formatRescueMessageText } from '@/utils/rescue-format'

const props = defineProps<{
  messages: readonly RescueMessage[]
  myOpenId: string
  disabled: boolean
}>()

const emit = defineEmits<{
  send: [text: string]
}>()

const draft = ref('')
const bottomAnchor = 'msg-bottom'
const lastMessageAnchor = ref('')

watch(
  () => props.messages.length,
  async () => {
    await nextTick()
    lastMessageAnchor.value = bottomAnchor
  },
  { immediate: true },
)

function handleSend(): void {
  const text = draft.value.trim()
  if (props.disabled || !text) return
  emit('send', text)
  draft.value = ''
}
</script>

<style lang="scss" scoped>
.messages-area {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: $background;
}

.message-list {
  flex: 1;
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
