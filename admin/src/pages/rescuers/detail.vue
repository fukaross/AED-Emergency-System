<template>
  <admin-layout>
    <div class="page-content-padding">
      <el-button class="back-link" text @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
        返回列表
      </el-button>

      <div v-if="profile" class="detail-container">
        <rescuer-edit-form
          v-model="form"
          :rescuer-id="profile.id"
          :certificate-no-masked="profile.certificateNoMasked"
        />

        <rescuer-profile-readonly :profile="profile!" />

        <div class="action-bar">
          <el-button
            type="primary"
            :disabled="!dirty || saving"
            :loading="saving"
            @click="handleSave"
          >
            保存变更
          </el-button>
          <span v-if="dirty" class="dirty-hint">有未保存的变更</span>
        </div>
      </div>

      <el-empty v-else-if="!loading" description="急救员不存在" />
    </div>
  </admin-layout>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import adminLayout from '@/components/admin-layout.vue'
import rescuerEditForm from './rescuer-edit-form.vue'
import rescuerProfileReadonly from './rescuer-profile-readonly.vue'
import { getRescuerDetail, updateRescuer } from '@/api/rescuers'
import { getApiErrorMessage } from '@/api/client'
import { validateIdNumber } from '@/utils/id-number'
import type { AdminRescuerDetail } from '@/types/rescuer'
import type { RescuerForm } from './use-rescuer-form'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const profile = ref<AdminRescuerDetail | null>(null)
const saving = ref(false)
const dirty = ref(false)

const ids = computed(() => route.params.id as string)

const form = ref<RescuerForm>({
  name: '', avatarUrl: '', phone: '',
  campus: 'guilinyang', level: null, qualificationStatus: 'active',
  lat: 0, lng: 0,
  certificateNo: null, issuingAuthority: null, issueDate: null, workUnit: null,
  skillCpr: false, skillAed: false, skillStb: false,
  idNumber: '',
  cprAssessmentResult: null, aedAssessmentResult: null,
  traumaAssessmentResult: null, cprAedCombinedResult: null,
})

const originalForm = ref<string>('')

function captureOriginal() { originalForm.value = JSON.stringify(form.value) }
function checkDirty() { dirty.value = originalForm.value !== JSON.stringify(form.value) }

function fillForm(p: AdminRescuerDetail) {
  form.value = {
    name: p.name, avatarUrl: p.avatarUrl, phone: p.phone,
    campus: p.campus, level: p.level, qualificationStatus: p.qualificationStatus,
    lat: p.lat, lng: p.lng,
    certificateNo: p.certificateNo, issuingAuthority: p.issuingAuthority,
    issueDate: p.issueDate, workUnit: p.workUnit,
    skillCpr: p.skillCpr, skillAed: p.skillAed, skillStb: p.skillStb,
    idNumber: p.idNumber,
    cprAssessmentResult: p.cprAssessmentResult,
    aedAssessmentResult: p.aedAssessmentResult,
    traumaAssessmentResult: p.traumaAssessmentResult,
    cprAedCombinedResult: p.cprAedCombinedResult,
  }
  captureOriginal()
  dirty.value = false
}

async function loadDetail() {
  loading.value = true
  try {
    const res = await getRescuerDetail(ids.value)
    profile.value = res.data.data
    fillForm(profile.value)
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '加载急救员详情失败'))
  } finally { loading.value = false }
}

async function handleSave() {
  if (!dirty.value) return
  const idNumberError = validateIdNumber(form.value.idNumber)
  if (idNumberError) {
    ElMessage.error(idNumberError)
    return
  }

  saving.value = true
  try {
    const diff = buildDiff()
    const res = await updateRescuer(ids.value, diff)
    profile.value = res.data.data
    fillForm(profile.value)
    ElMessage.success('保存成功')
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '保存失败'))
  } finally { saving.value = false }
}

function buildDiff(): Record<string, unknown> {
  const original = JSON.parse(originalForm.value) as RescuerForm
  const current = form.value
  const diff: Record<string, unknown> = {}
  for (const key of Object.keys(current) as (keyof RescuerForm)[]) {
    if (JSON.stringify(current[key]) !== JSON.stringify(original[key])) diff[key] = current[key]
  }
  return diff
}

function goBack() { router.push('/rescuers') }

const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)
function startPolling() {
  stopPolling()
  pollTimer.value = setInterval(() => { if (!dirty.value) loadDetail() }, 10000)
}
function stopPolling() {
  if (pollTimer.value) { clearInterval(pollTimer.value); pollTimer.value = null }
}
function onVisibilityChange() {
  if (document.hidden) { stopPolling() }
  else { if (!dirty.value) loadDetail(); startPolling() }
}

watch(() => form.value, () => { if (originalForm.value) checkDirty() }, { deep: true })

onMounted(() => {
  loadDetail(); startPolling()
  document.addEventListener('visibilitychange', onVisibilityChange)
})
onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<style scoped>
.back-link { margin-bottom: 16px; }
.detail-container { display: flex; flex-direction: column; gap: 16px; }
.action-bar { display: flex; align-items: center; gap: 12px; padding: 16px 0; }
.dirty-hint { font-size: 13px; color: #e6a23c; }
</style>
