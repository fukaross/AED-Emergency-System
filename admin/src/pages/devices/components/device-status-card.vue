<template>
  <div class="admin-card">
    <div class="card-header-bar">
      <h3 class="card-header-title">设备状态</h3>
    </div>

    <div class="status-section">
      <div class="status-row">
        <span class="status-label">当前状态：</span>
        <status-tag :status="currentStatus" category="device" />
      </div>

      <div v-if="currentStatus === 'online'" class="status-action">
        <el-button type="danger" plain @click="$emit('setOffline')">
          标记为离线
        </el-button>
      </div>
      <div v-else class="status-action">
        <el-button type="success" @click="$emit('setOnline')">
          恢复在线
        </el-button>
        <div v-if="statusNote" class="offline-note">
          <span class="note-label">离线原因：</span>
          <span class="note-text">{{ statusNote }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import statusTag from '@/components/status-tag.vue'

defineProps<{
  currentStatus: 'online' | 'offline'
  statusNote: string | null
}>()

defineEmits<{
  setOffline: []
  setOnline: []
}>()
</script>

<style scoped>
.status-section {
  padding: 16px 24px 24px;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.status-label {
  font-size: 14px;
  color: #606266;
}

.status-action {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.offline-note {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 10px 14px;
  background-color: #fef0f0;
  border-radius: 4px;
  border: 1px solid #fde2e2;
}

.note-label {
  font-size: 13px;
  color: #909399;
  flex-shrink: 0;
}

.note-text {
  font-size: 14px;
  color: #f56c6c;
}
</style>
