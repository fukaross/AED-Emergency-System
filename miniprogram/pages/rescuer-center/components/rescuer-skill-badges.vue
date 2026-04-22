<template>
  <view class="skill-badges">
    <view
      v-for="tag in visibleTags"
      :key="tag.key"
      class="skill-tag"
      :class="tag.certified ? 'skill-tag--yes' : 'skill-tag--no'"
    >
      <image
        class="skill-icon"
        :src="tag.certified ? VERIFIED_ICON : UNVERIFIED_ICON"
        mode="aspectFit"
      />
      <text class="skill-text">{{ tag.label }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { RescuerSkillFlags, RescuerSkillTag } from '@/types/rescuer'

const VERIFIED_ICON = '/static/icons/verified-rescuer.svg'
const UNVERIFIED_ICON = '/static/icons/unverified-rescuer.svg'

interface Props {
  skills: RescuerSkillFlags
}

const props = defineProps<Props>()

interface TagItem {
  readonly key: RescuerSkillTag
  readonly label: string
  readonly certified: boolean
}

const visibleTags = computed<TagItem[]>(() => [
  { key: 'cpr', label: 'CPR', certified: props.skills.cpr },
  { key: 'aed', label: 'AED', certified: props.skills.aed },
  { key: 'stb', label: 'STB', certified: props.skills.stb },
])
</script>

<style lang="scss" scoped>
.skill-badges {
  display: flex;
  align-items: center;
  gap: $spacing-xs;
}

.skill-tag {
  display: inline-flex;
  align-items: center;
  gap: 4rpx;
  height: 32rpx;
  padding: 0 10rpx;
  border-radius: 16rpx;
  font-size: $font-size-xs;
}

.skill-tag--yes {
  background: #E8F5E9;
  color: $status-online;
}

.skill-tag--no {
  background: #F5F5F5;
  color: $status-offline;
}

.skill-icon {
  width: 20rpx;
  height: 20rpx;
}

.skill-text {
  line-height: $line-height-tight;
}
</style>
