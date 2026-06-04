<template>
  <div class="admin-card logs-card no-padding">
    <div class="logs-head">
      <h3 class="logs-title">最近操作日志</h3>
    </div>

    <el-skeleton :loading="loading" :rows="8" animated>
      <template #default>
        <div v-if="logs.length > 0" class="log-list">
          <div v-for="log in logs" :key="log.id" class="log-item">
            <div class="log-time">{{ formatTime(log.createdAt) }}</div>
            <div class="log-actor">{{ log.actorName || '系统' }}</div>
            <div class="log-event">
              <span :class="['event-tag', getEventTagClass(log.eventType)]">
                {{ getEventNameCn(log.eventName) }}
              </span>
            </div>
            <div class="log-summary">{{ log.summary }}</div>
          </div>
        </div>
        <el-empty v-else description="暂无最近操作日志" />
      </template>
    </el-skeleton>

    <div v-if="isSuperAdmin && !loading" class="logs-footer">
      <router-link to="/audit-logs" class="all-logs-link">查看全部日志 →</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { AdminDashboardRecentLogItem } from '@/types/dashboard'

defineProps<{
  logs: AdminDashboardRecentLogItem[]
  loading: boolean
  isSuperAdmin: boolean
}>()

const eventNameMap: Record<string, string> = {
  'admin.bootstrap_created': '系统初始化',
  'admin.login': '管理员登录',
  'admin.logout': '管理员登出',
  'admin.session_revoked': '会话撤销',
  'admin.super_admin_reset': '超管重置',
  'admin.password_reset': '管理员密码重置',
  'admin.admin_created': '创建管理员',
  'admin.admin_deleted': '删除管理员',
  'admin.created': '创建管理员',
  'admin.deleted': '删除管理员',
  'help.create': '发布求助',
  'help.report_submitted': '提交报告',
  'help.close': '关闭求助',
  'help.false_alarm': '标记误报',
  'user.updated': '用户更新',
}

function getEventNameCn(name: string) {
  return eventNameMap[name] || name
}

function getEventTagClass(type: string) {
  if (type === 'admin') return 'tag-admin'
  if (type === 'help') return 'tag-help'
  if (type === 'user') return 'tag-user'
  return ''
}

function formatTime(dateStr: string) {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return dateStr
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  const hh = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${mm}-${dd} ${hh}:${min}`
}
</script>

<style scoped lang="scss">
@use '../../../styles/variables' as *;

.logs-card {
  margin-top: 0;
}

.logs-head {
  padding: 16px 24px;
  border-bottom: 1px solid #ebeef5;
}

.logs-title {
  margin: 0;
  font-size: 15px;
  font-weight: 500;
  color: $text-primary;
}

.log-list {
  display: flex;
  flex-direction: column;
}

.log-item {
  display: flex;
  align-items: center;
  padding: 12px 24px;
  border-bottom: 1px solid #ebeef5;
  font-size: $fs-md;

  &:last-child {
    border-bottom: none;
  }

  &:hover {
    background-color: #f7f9fb;
  }

  @media (max-width: 767px) {
    flex-wrap: wrap;
    padding: 10px 16px;
    gap: 4px 8px;
  }
}

.log-time {
  width: 110px;
  font-size: $fs-base;
  color: $text-secondary;
  flex-shrink: 0;
}

.log-actor {
  width: 120px;
  color: $text-primary;
  font-weight: 500;
  flex-shrink: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-event {
  width: 140px;
  flex-shrink: 0;
}

.event-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: $radius-tag;
  font-size: $fs-sm;
  line-height: 1.5;

  &.tag-admin { background-color: #ecf5ff; color: #409eff; }
  &.tag-help { background-color: #fdf6ec; color: #e6a23c; }
  &.tag-user { background-color: #f0f9eb; color: #67c23a; }
}

.log-summary {
  flex: 1;
  color: $text-regular;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.logs-footer {
  display: flex;
  justify-content: flex-end;
  padding: 16px 24px;
  border-top: 1px solid #ebeef5;
}

.all-logs-link {
  font-size: $fs-base;
  color: $color-primary;
  text-decoration: none;
  cursor: pointer;

  &:hover {
    color: #73b8ff;
    text-decoration: underline;
  }
}
</style>
