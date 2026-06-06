import { ref, computed, nextTick } from 'vue'
import type { FormInstance, UploadRequestOptions, UploadRawFile } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { getAedDeviceDetail, createAedDevice, updateAedDevice, deleteAedDevice } from '@/api/devices'
import { getApiErrorMessage } from '@/api/client'
import { createRequestSequencer } from '@/utils/request-sequencer'
import type { AdminAedDeviceItem } from '@/types/device'

export interface DeviceForm {
  id: string
  name: string
  lat: number
  lng: number
  image: string
  address: string
  status: string
  statusNote: string | null
  lastMaintenance: string
  nextMaintenance: string
  maintainer: string
}

export function useDeviceDetail() {
  const route = useRoute()
  const router = useRouter()
  const formRef = ref<FormInstance>()
  const loading = ref(false)
  const saveLoading = ref(false)
  const deleteLoading = ref(false)
  const formDirty = ref(false)
  const sequencer = createRequestSequencer()

  const deviceId = computed(() => route.params.id as string)
  const isCreate = computed(() => deviceId.value === 'new')

  const form = ref<DeviceForm>({
    id: '',
    name: '',
    lat: 0,
    lng: 0,
    image: '',
    address: '',
    status: 'online',
    statusNote: null,
    lastMaintenance: '',
    nextMaintenance: '',
    maintainer: '',
  })

  const originalForm = ref<DeviceForm | null>(null)
  const currentStatus = computed(() => (form.value.status as 'online' | 'offline') || 'online')

  function populateForm(device: AdminAedDeviceItem) {
    const data: DeviceForm = {
      id: device.id,
      name: device.name,
      lat: device.lat,
      lng: device.lng,
      image: device.image,
      address: device.address,
      status: device.status,
      statusNote: device.statusNote,
      lastMaintenance: device.lastMaintenance,
      nextMaintenance: device.nextMaintenance,
      maintainer: device.maintainer,
    }
    form.value = { ...data }
    originalForm.value = { ...data }
    nextTick(() => {
      formDirty.value = false
    })
  }

  async function loadDetail() {
    if (isCreate.value) return
    if (formDirty.value) return
    loading.value = true
    const seq = sequencer.next()
    try {
      const res = await getAedDeviceDetail(deviceId.value)
      if (!sequencer.isCurrent(seq)) return
      populateForm(res.data.data)
    } catch (error) {
      if (!sequencer.isCurrent(seq)) return
      ElMessage.error(getApiErrorMessage(error, '加载设备详情失败'))
      router.replace('/devices')
    } finally {
      if (sequencer.isCurrent(seq)) {
        loading.value = false
      }
    }
  }

  function buildSavePayload(): Record<string, unknown> {
    if (isCreate.value) {
      return {
        name: form.value.name,
        lat: form.value.lat,
        lng: form.value.lng,
        image: form.value.image || undefined,
        address: form.value.address || undefined,
        status: form.value.status as 'online' | 'offline' | undefined,
        statusNote: form.value.statusNote || undefined,
        lastMaintenance: form.value.lastMaintenance || undefined,
        nextMaintenance: form.value.nextMaintenance || undefined,
        maintainer: form.value.maintainer || undefined,
      }
    }

    const payload: Record<string, unknown> = {}
    for (const key of Object.keys(form.value) as (keyof DeviceForm)[]) {
      if (key === 'id') continue
      const current = form.value[key]
      const original = originalForm.value?.[key]
      if (current !== original && key !== 'statusNote') {
        payload[key] = current
      }
      if (key === 'statusNote' && current !== original) {
        payload.statusNote = current === '' ? null : current
      }
    }
    return payload
  }

  async function handleSave(valid: boolean) {
    if (!valid) return

    saveLoading.value = true
    try {
      const payload = buildSavePayload()
      if (!isCreate.value && Object.keys(payload).length === 0) {
        ElMessage.info('没有需要保存的更改')
        saveLoading.value = false
        return
      }
      if (isCreate.value) {
        await createAedDevice(payload as unknown as Parameters<typeof createAedDevice>[0])
        ElMessage.success('设备创建成功')
        router.replace('/devices')
      } else {
        const res = await updateAedDevice(deviceId.value, payload)
        ElMessage.success('设备信息已更新')
        populateForm(res.data.data)
      }
    } catch (error) {
      ElMessage.error(getApiErrorMessage(error, '保存失败'))
    } finally {
      saveLoading.value = false
    }
  }

  async function handleSetOffline() {
    try {
      const { value } = await ElMessageBox.prompt('请输入离线原因（必填）', '标记为离线', {
        confirmButtonText: '确认离线',
        cancelButtonText: '取消',
        inputType: 'textarea',
        inputValidator: (val: string) => {
          if (!val || !val.trim()) return '离线原因不能为空'
          return true
        },
        inputErrorMessage: '离线原因不能为空',
      })

      if (!value || !value.trim()) return

      const res = await updateAedDevice(deviceId.value, {
        status: 'offline',
        statusNote: value.trim(),
      })
      ElMessage.success('设备已标记为离线')
      populateForm(res.data.data)
    } catch (error) {
      if (error !== 'cancel' && error !== 'close') {
        ElMessage.error(getApiErrorMessage(error, '状态变更失败'))
      }
    }
  }

  async function handleSetOnline() {
    try {
      await ElMessageBox.confirm('确认恢复该设备为在线状态？', '恢复在线', {
        confirmButtonText: '确认恢复',
        cancelButtonText: '取消',
        type: 'info',
      })

      const res = await updateAedDevice(deviceId.value, {
        status: 'online',
        statusNote: null,
      })
      ElMessage.success('设备已恢复在线')
      populateForm(res.data.data)
    } catch (error) {
      if (error !== 'cancel' && error !== 'close') {
        ElMessage.error(getApiErrorMessage(error, '状态变更失败'))
      }
    }
  }

  async function handleDelete() {
    try {
      await ElMessageBox.confirm(
        `确定删除设备 "${form.value.name}"？此操作不可撤销。`,
        '删除确认',
        {
          confirmButtonText: '确认删除',
          cancelButtonText: '取消',
          type: 'warning',
        },
      )
      deleteLoading.value = true
      await deleteAedDevice(deviceId.value)
      ElMessage.success('设备已删除')
      router.replace('/devices')
    } catch (error) {
      if (error === 'cancel' || error === 'close') return
      ElMessage.error(getApiErrorMessage(error, '删除失败'))
    } finally {
      deleteLoading.value = false
    }
  }

  async function handleUpload(options: UploadRequestOptions) {
    const file = options.file as UploadRawFile
    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('scene', 'device')

      const response = await fetch('/api/upload/image', {
        method: 'POST',
        headers: { 'X-Aed-Admin-Key': localStorage.getItem('adminKey') || '' },
        body: formData,
      })
      const result = await response.json()
      if (result.code === 0 && result.data?.url) {
        form.value.image = result.data.url
        ElMessage.success('图片上传成功')
      } else {
        ElMessage.error(result.message || '图片上传失败')
      }
    } catch {
      ElMessage.error('图片上传失败，请检查网络或手动输入 URL')
    }
  }

  const pollTimer = ref<ReturnType<typeof setInterval> | null>(null)

  function startPolling() {
    stopPolling()
    pollTimer.value = setInterval(loadDetail, 10000)
  }

  function stopPolling() {
    if (pollTimer.value) {
      clearInterval(pollTimer.value)
      pollTimer.value = null
    }
  }

  function goBack() {
    router.back()
  }

  return {
    formRef,
    form,
    originalForm,
    formDirty,
    loading,
    saveLoading,
    deleteLoading,
    deviceId,
    isCreate,
    currentStatus,
    populateForm,
    loadDetail,
    handleSave,
    handleSetOffline,
    handleSetOnline,
    handleDelete,
    handleUpload,
    startPolling,
    stopPolling,
    goBack,
  }
}
