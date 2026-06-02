<template>
  <el-tag class="status-tag" :style="tagStyle" size="small">{{ resolvedTag.label }}</el-tag>
</template>

<script setup lang="ts">
import { computed } from 'vue'

type StatusCategory = 'help' | 'channel' | 'account' | 'responder' | 'admin' | 'device' | 'volunteerApplication'

interface StatusConfig {
  label: string
  background: string
  color: string
}

const props = defineProps<{
  status: string
  category?: StatusCategory
}>()

const fallbackTag: StatusConfig = {
  label: props.status,
  background: '#f4f4f5',
  color: '#909399',
}

const statusMaps: Record<StatusCategory, Record<string, StatusConfig>> = {
  help: {
    published: { label: '已发布', background: '#ecf5ff', color: '#409eff' },
    responding: { label: '响应中', background: '#fdf6ec', color: '#e6a23c' },
    pending_review: { label: '待审核', background: '#fef0f0', color: '#f56c6c' },
    closed: { label: '已关闭', background: '#f0f9eb', color: '#67c23a' },
    false_alarm: { label: '误报', background: '#f4f4f5', color: '#909399' },
  },
  channel: {
    user: { label: '用户', background: '#ecf5ff', color: '#409eff' },
    guest: { label: '游客', background: '#f4f4f5', color: '#909399' },
  },
  account: {
    active: { label: '正常', background: '#f0f9eb', color: '#67c23a' },
    disabled: { label: '禁用', background: '#fef0f0', color: '#f56c6c' },
  },
  responder: {
    active: { label: '已激活', background: '#f0f9eb', color: '#67c23a' },
    inactive: { label: '未激活', background: '#f4f4f5', color: '#909399' },
  },
  admin: {
    super_admin: { label: '超级管理员', background: '#ecf5ff', color: '#409eff' },
    admin: { label: '管理员', background: '#f4f4f5', color: '#909399' },
  },
  device: {
    online: { label: '在线', background: '#f0f9eb', color: '#67c23a' },
    offline: { label: '离线', background: '#fef0f0', color: '#f56c6c' },
  },
  volunteerApplication: {
    submitted: { label: '待审核', background: '#fdf6ec', color: '#e6a23c' },
    approved: { label: '已通过', background: '#f0f9eb', color: '#67c23a' },
    rejected: { label: '已驳回', background: '#fef0f0', color: '#f56c6c' },
  },
}

const resolvedTag = computed(() => {
  const category = props.category ?? 'help'
  return statusMaps[category][props.status] ?? fallbackTag
})

const tagStyle = computed(() => {
  return {
    '--el-tag-bg-color': resolvedTag.value.background,
    '--el-tag-border-color': resolvedTag.value.background,
    '--el-tag-text-color': resolvedTag.value.color,
  }
})
</script>

<style scoped>
.status-tag {
  border-radius: 3px;
  font-size: 12px;
}
</style>
