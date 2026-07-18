<template>
  <admin-layout>
    <div class="detail-container">
      <!-- Back button header -->
      <div class="detail-header-action">
        <el-button type="default" @click="goBack">
          ← 返回列表
        </el-button>
      </div>

      <div v-loading="loading">
        <div v-if="detail" class="detail-grid-layout">
          <!-- Left side panels -->
          <div class="left-panels">
            <!-- 1. Basic Info Panel -->
            <div class="admin-card">
              <div class="profile-header">
                <el-avatar
                  v-if="detail.avatarUrl"
                  :size="64"
                  :src="detail.avatarUrl"
                  class="profile-avatar"
                />
                <div v-else class="profile-avatar-placeholder">
                  {{ getInitials(detail.name) }}
                </div>
                <div class="profile-title-row">
                  <h2 class="profile-name">{{ detail.name }}</h2>
                  <el-tag size="small" type="primary">
                    {{ getRoleCn(detail.primaryRole) }}
                  </el-tag>
                </div>
              </div>

              <div class="description-list mt-20">
                <div class="description-item">
                  <span class="desc-label">OpenID</span>
                  <span class="desc-value clickable-openid" title="点击复制 OpenID" @click="copyOpenId(detail.openId)">
                    {{ truncateOpenId(detail.openId) }}
                    <el-icon class="copy-icon"><CopyDocument /></el-icon>
                  </span>
                </div>
                <div class="description-item">
                  <span class="desc-label">手机号</span>
                  <span class="desc-value">{{ maskPhone(detail.phone) }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">注册时间</span>
                  <span class="desc-value">{{ formatTime(detail.createdAt) }}</span>
                </div>
                <div class="description-item">
                  <span class="desc-label">最近登录</span>
                  <span class="desc-value">{{ formatTime(detail.lastLoginAt) }}</span>
                </div>
              </div>
            </div>

            <!-- 2. School Profile Panel -->
            <div class="admin-card">
              <div class="card-header-bar-custom">
                <h3 class="card-title">学校档案</h3>
              </div>
              <div class="card-body-custom">
                <div v-if="detail.schoolProfile" class="description-list">
                  <div class="description-item">
                    <span class="desc-label">学院</span>
                    <span class="desc-value">{{ detail.schoolProfile.college }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">
                      {{ detail.primaryRole === 'student' ? '学号' : '工号' }}
                    </span>
                    <span class="desc-value">
                      {{ detail.schoolProfile.studentId || detail.schoolProfile.staffId || '—' }}
                    </span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">绑定状态</span>
                    <span class="desc-value">
                      <el-tag :type="getBindingTagType(detail.schoolProfile.bindingStatus)" size="small">
                        {{ getBindingStatusCn(detail.schoolProfile.bindingStatus) }}
                      </el-tag>
                    </span>
                  </div>
                </div>
                <div v-else class="empty-section">
                  未绑定学校档案
                </div>
              </div>
            </div>

            <!-- 3. Emergency Rescuer certification Panel -->
            <div class="admin-card">
              <div class="card-header-bar-custom">
                <h3 class="card-title">急救资格</h3>
              </div>
              <div class="card-body-custom">
                <div v-if="detail.rescuerSummary" class="description-list">
                  <div class="description-item">
                    <span class="desc-label">类型</span>
                    <span class="desc-value">
                      {{ detail.rescuerSummary.responderType === 'rescuer' ? '急救员' : '值班校医' }}
                    </span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">等级</span>
                    <span class="desc-value">{{ getLevelCn(detail.rescuerSummary.level) }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">证书号</span>
                    <span class="desc-value">{{ maskCertNo(detail.rescuerSummary.currentCertificateNo) }}</span>
                  </div>
                  <div class="description-item">
                    <span class="desc-label">有效期至</span>
                    <span class="desc-value">{{ formatDateOnly(detail.rescuerSummary.certExpiry) }}</span>
                  </div>
                  <div class="section-action">
                    <el-button
                      type="primary"
                      size="small"
                      plain
                      @click="viewRescuer(detail.rescuerSummary.responderProfileId)"
                    >
                      查看档案
                    </el-button>
                  </div>
                </div>
                <div v-else class="empty-section">
                  未绑定急救资格
                </div>
              </div>
            </div>
          </div>

          <!-- Right side Editor Panel -->
          <div class="right-panel">
            <div class="admin-card editor-card">
              <h3 class="editor-title">编辑信息</h3>
              <el-form :model="editForm" label-position="top" class="edit-form">
                <!-- Primary Role select -->
                <el-form-item label="身份">
                  <el-select v-model="editForm.primaryRole" class="full-width">
                    <el-option label="学生" value="student" />
                    <el-option label="教师" value="teacher" />
                    <el-option label="职工" value="staff" />
                    <el-option label="管理员" value="admin" />
                  </el-select>
                </el-form-item>

                <!-- Account Status Select -->
                <el-form-item label="账号状态">
                  <el-select v-model="editForm.accountStatus" class="full-width">
                    <el-option label="正常" value="active" />
                    <el-option label="禁用" value="disabled" />
                  </el-select>
                </el-form-item>

                <!-- Admin Remark Input -->
                <el-form-item label="备注">
                  <el-input
                    v-model="editForm.adminRemark"
                    type="textarea"
                    :rows="3"
                    placeholder="请输入管理备注信息"
                  />
                </el-form-item>

                <!-- Save Action Button -->
                <el-form-item class="save-form-item">
                  <el-button
                    type="primary"
                    :loading="saveLoading"
                    class="full-width"
                    @click="handleSave"
                  >
                    保存修改
                  </el-button>
                </el-form-item>
              </el-form>
            </div>
          </div>
        </div>
      </div>
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { CopyDocument } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import { useUserDetail } from './use-user-detail'
import {
  formatDateOnly,
  formatTime,
  getBindingStatusCn,
  getBindingTagType,
  getInitials,
  getLevelCn,
  getRoleCn,
  maskCertNo,
  maskPhone,
  truncateOpenId,
} from './user-detail-presentation'

const route = useRoute()
const router = useRouter()
const openIdParam = route.params.openId as string
const {
  copyOpenId,
  destroy,
  detail,
  editForm,
  handleSave,
  initialize,
  loading,
  saveLoading,
} = useUserDetail({ openId: openIdParam })

function goBack() {
  router.push('/users')
}

function viewRescuer(id: string) {
  router.push(`/rescuers/${id}`)
}

onMounted(initialize)
onUnmounted(destroy)
</script>

<style scoped lang="scss" src="./detail.scss"></style>
