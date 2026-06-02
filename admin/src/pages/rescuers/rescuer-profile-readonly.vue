<template>
  <div>
    <div class="admin-card">
      <div class="card-header-bar">
        <h3 class="card-header-title">档案信息</h3>
      </div>
      <el-form :model="profile" label-width="100px" class="detail-form readonly-form">
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="档案编号">
              <el-input :model-value="profile.recordId" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="性别">
              <el-input :model-value="profile.gender" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="出生日期">
              <el-input :model-value="profile.birthDate" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="身份证号">
              <el-input :model-value="profile.idNumberMasked" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="脱敏标记">
              <el-input :model-value="`电话${profile.phoneMaskedFlag ? '已' : '未'}脱敏, 身份证${profile.idMaskedFlag ? '已' : '未'}脱敏`" disabled />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </div>

    <div class="admin-card">
      <div class="card-header-bar">
        <h3 class="card-header-title">培训与考核</h3>
      </div>
      <el-form :model="profile" label-width="100px" class="detail-form readonly-form">
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="培训年份">
              <el-input :model-value="String(profile.trainingYear)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="理论分数">
              <el-input :model-value="String(profile.theoryScore)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="培训班次">
              <el-input :model-value="profile.trainingSessionName" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="培训项目">
          <el-input :model-value="profile.trainingProject" disabled />
        </el-form-item>
        <el-form-item v-if="profile.remark" label="备注">
          <el-input :model-value="profile.remark" disabled />
        </el-form-item>
      </el-form>
    </div>

    <div class="admin-card">
      <div class="card-header-bar">
        <h3 class="card-header-title">来源信息</h3>
      </div>
      <el-form :model="profile" label-width="100px" class="detail-form readonly-form">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="来源类型">
              <el-input :model-value="sourceLabel" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="绑定用户">
              <el-input :model-value="profile.boundUserName || profile.boundOpenId || '-'" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item v-if="profile.source === 'volunteer_application' && profile.sourceApplicationId" label="来源申请">
          <el-button text type="primary" @click="goVolunteerApplication">
            #{{ profile.sourceApplicationId }}
          </el-button>
        </el-form-item>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="来源文件">
              <el-input :model-value="profile.sourceFile" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="来源表">
              <el-input :model-value="profile.sourceSheet" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="行号">
              <el-input :model-value="String(profile.sourceRowNo)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="序号">
              <el-input :model-value="profile.sourceSeqNo" disabled />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import type { AdminRescuerDetail } from '@/types/rescuer'

const props = defineProps<{
  profile: AdminRescuerDetail
}>()

const router = useRouter()
const SOURCE_LABELS: Record<string, string> = {
  seed: '学校导入',
  volunteer_application: '志愿者申请',
  admin_manual: '手工维护',
}
const sourceLabel = computed(() => SOURCE_LABELS[props.profile.source] || props.profile.source)

function goVolunteerApplication() {
  if (!props.profile.sourceApplicationId) return
  router.push(`/volunteer-applications/${props.profile.sourceApplicationId}`)
}
</script>

<style scoped>
.detail-form {
  max-width: 720px;
}

.readonly-form :deep(.el-input.is-disabled .el-input__wrapper) {
  background-color: #fafafa;
  box-shadow: none;
}

.readonly-form :deep(.el-input__inner) {
  color: #606266;
  -webkit-text-fill-color: #606266;
}
</style>
