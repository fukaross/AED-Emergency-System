<template>
  <Modal
    v-model="showPrompt"
    title="接收通知"
    :show-close="false"
    :show-footer="false"
  >
    <view class="subscribe-prompt">
      <text class="subscribe-prompt__text">有人求助时提醒你</text>
    </view>
    <template #footer>
      <view class="subscribe-prompt__actions">
        <button
          class="subscribe-prompt__btn subscribe-prompt__btn--ghost"
          @tap="close"
        >
          稍后
        </button>
        <button
          class="subscribe-prompt__btn subscribe-prompt__btn--primary"
          :loading="isRequesting"
          @tap="enable"
        >
          开启
        </button>
      </view>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue'
import { requestAlertSubscribe } from '@/api/wechat-subscribe'
import Modal from '@/components/modal/index.vue'
import { WECHAT_ALERT_TEMPLATE_ID } from '@/utils/constants'
import { USE_REAL_WECHAT_FLOW } from '@/utils/runtime-env'

const props = defineProps<{
  readonly active: boolean
  readonly loggedIn: boolean
}>()

const PROMPT_DELAY_MS = 800
let hasPromptedThisSession = false

const showPrompt = ref(false)
const isRequesting = ref(false)
let promptTimer: ReturnType<typeof setTimeout> | null = null

watch(
  () => props.active,
  (active) => {
    if (active) schedule()
    else clearTimer()
  },
  { immediate: true },
)

onUnmounted(clearTimer)

function shouldShow(): boolean {
  return USE_REAL_WECHAT_FLOW
    && Boolean(WECHAT_ALERT_TEMPLATE_ID)
    && !hasPromptedThisSession
    && props.loggedIn
}

function schedule(): void {
  clearTimer()
  if (!shouldShow()) return
  promptTimer = setTimeout(() => {
    promptTimer = null
    if (!shouldShow()) return
    hasPromptedThisSession = true
    showPrompt.value = true
  }, PROMPT_DELAY_MS)
}

function clearTimer(): void {
  if (!promptTimer) return
  clearTimeout(promptTimer)
  promptTimer = null
}

function close(): void {
  showPrompt.value = false
}

async function enable(): Promise<void> {
  if (isRequesting.value) return
  isRequesting.value = true
  try {
    const status = await requestAlertSubscribe()
    const accepted = status === 'accept' || status === 'acceptWithAudio'
    uni.showToast({
      title: accepted ? '已开启' : '未开启',
      icon: accepted ? 'success' : 'none',
    })
    showPrompt.value = false
  } catch {
    console.warn('[MapHome] subscribe-authorize failed')
    uni.showToast({ title: '未开启', icon: 'none' })
  } finally {
    isRequesting.value = false
  }
}
</script>

<style lang="scss" scoped>
.subscribe-prompt {
  padding: $spacing-xs $spacing-md $spacing-sm;
}

.subscribe-prompt__text {
  color: $text-secondary;
  font-size: $font-size-base;
  line-height: 1.6;
}

.subscribe-prompt__actions {
  display: flex;
  gap: $spacing-sm;
  width: 100%;
}

.subscribe-prompt__btn {
  flex: 1;
  height: 88rpx;
  border-radius: $radius-md;
  font-size: $font-size-base;
  line-height: 88rpx;
}

.subscribe-prompt__btn::after {
  border: 0;
}

.subscribe-prompt__btn--ghost {
  background: $background;
  color: $text-secondary;
}

.subscribe-prompt__btn--primary {
  background: $primary;
  color: $text-white;
}
</style>
