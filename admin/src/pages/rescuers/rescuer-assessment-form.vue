<template>
  <div class="admin-card">
    <div class="card-header-bar">
      <h3 class="card-header-title">考核结果</h3>
    </div>
    <el-form :model="modelValue" label-width="100px" class="detail-form">
      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="创伤救护考核">
            <el-select :model-value="modelValue.traumaAssessmentResult" style="width: 100%" clearable @update:model-value="set('traumaAssessmentResult', $event)">
              <el-option label="合格" value="合格" />
              <el-option label="不合格" value="不合格" />
              <el-option label="未考核" value="未考核" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="CPR考核">
            <el-select :model-value="modelValue.cprAssessmentResult" style="width: 100%" clearable @update:model-value="set('cprAssessmentResult', $event)">
              <el-option label="合格" value="合格" />
              <el-option label="不合格" value="不合格" />
              <el-option label="未考核" value="未考核" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>
      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="AED考核">
            <el-select :model-value="modelValue.aedAssessmentResult" style="width: 100%" clearable @update:model-value="set('aedAssessmentResult', $event)">
              <el-option label="合格" value="合格" />
              <el-option label="不合格" value="不合格" />
              <el-option label="未考核" value="未考核" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="综合考核">
            <el-select :model-value="modelValue.cprAedCombinedResult" style="width: 100%" clearable @update:model-value="set('cprAedCombinedResult', $event)">
              <el-option label="合格" value="合格" />
              <el-option label="不合格" value="不合格" />
              <el-option label="未考核" value="未考核" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
  </div>
</template>

<script setup lang="ts">
import type { RescuerForm } from './use-rescuer-form'

const props = defineProps<{ modelValue: RescuerForm }>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: RescuerForm): void
}>()

function set<K extends keyof RescuerForm>(key: K, value: RescuerForm[K]) {
  emit('update:modelValue', { ...props.modelValue, [key]: value })
}
</script>

<style scoped>
.detail-form { max-width: 720px; }
</style>
