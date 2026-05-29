<template>
  <view class="course-card" @click="$emit('click')">
    <view class="course-card-thumb">
      <image
        class="course-card-img"
        :src="thumbnail"
        mode="aspectFill"
        lazy-load
      />
    </view>
    <view class="course-card-title-area">
      <text class="course-card-title">{{ title }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
// 课程卡片 —— 2列网格，缩略图 16:10，标题最多两行省略
interface Props {
  title: string
  thumbnail: string
}

defineProps<Props>()

defineEmits<{
  click: []
}>()
</script>

<style lang="scss" scoped>
.course-card {
  @include card-base;
  overflow: hidden;
}

.course-card-thumb {
  width: 100%;
  // 16:10 ratio
  padding-bottom: 62.5%;
  position: relative;
  overflow: hidden;
  border-radius: $radius-lg $radius-lg 0 0;
}

.course-card-img {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
}

.course-card-title-area {
  padding: $spacing-md;
}

.course-card-title {
  font-size: $font-size-base;
  color: $text-primary;
  line-height: $line-height-normal;
  @include multi-line-ellipsis(2);
}
</style>
