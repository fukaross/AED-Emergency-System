<template>
  <div class="map-picker">
    <div class="picker-search-bar">
      <el-input
        v-model="keyword"
        :placeholder="placeholder"
        size="small"
        clearable
        :disabled="disabled"
        @keyup.enter="onSearch"
        @clear="suggestions = []"
      >
        <template #append>
          <el-button size="small" :disabled="disabled" @click="onSearch">搜索</el-button>
        </template>
      </el-input>
      <el-button
        size="small"
        :icon="FullScreen"
        circle
        class="expand-btn"
        :disabled="disabled"
        @click="dialogVisible = true"
      />
    </div>

    <div v-if="suggestions.length > 0" class="suggestion-list">
      <div
        v-for="(tip, idx) in suggestions"
        :key="idx"
        class="suggestion-item"
        @click="onSelect(tip)"
      >
        <span class="suggestion-name">{{ tip.name }}</span>
        <span class="suggestion-addr">{{ tip.district || tip.address }}</span>
      </div>
    </div>

    <div v-loading="loading" class="map-container" :style="{ height: height + 'px' }">
      <div v-if="error" class="map-error">
        <el-alert :title="error" type="error" show-icon :closable="false" />
      </div>
      <div :id="containerId" class="map-canvas" />
    </div>

    <div class="coord-display">
      <span class="coord-label">纬度: {{ lat.toFixed(6) }}</span>
      <span class="coord-label">经度: {{ lng.toFixed(6) }}</span>
    </div>

    <el-dialog
      v-model="dialogVisible"
      title="选择 AED 位置"
      width="80vw"
      destroy-on-close
      @opened="initDialogMap"
      @closed="destroyDialogMap"
    >
      <div class="dialog-picker">
        <el-input
          v-model="dialogKeyword"
          :placeholder="placeholder"
          size="small"
          clearable
          class="dialog-search"
          @keyup.enter="onDialogSearch"
        >
          <template #append>
            <el-button size="small" @click="onDialogSearch">搜索</el-button>
          </template>
        </el-input>
        <div v-if="dialogSuggestions.length > 0" class="suggestion-list">
          <div
            v-for="(tip, idx) in dialogSuggestions"
            :key="idx"
            class="suggestion-item"
            @click="onDialogSelect(tip)"
          >
            <span class="suggestion-name">{{ tip.name }}</span>
            <span class="suggestion-addr">{{ tip.district || tip.address }}</span>
          </div>
        </div>
        <div :id="dialogContainerId" class="dialog-map-canvas" />
        <div class="dialog-footer">
          <div class="dialog-coords">
            <span>纬度: {{ dialogLat.toFixed(6) }}</span>
            <span>经度: {{ dialogLng.toFixed(6) }}</span>
          </div>
          <el-button type="primary" @click="confirmDialog">确定</el-button>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { FullScreen } from '@element-plus/icons-vue'
import { useMapPicker } from '@/composables/use-map-picker'
import type { AutocompleteTip } from '@/types/amap'

const props = withDefaults(defineProps<{
  lat: number
  lng: number
  zoom?: number
  placeholder?: string
  disabled?: boolean
  height?: number
}>(), {
  zoom: 17,
  placeholder: '搜索地址...',
  disabled: false,
  height: 300,
})

const emit = defineEmits<{
  'update:lat': [value: number]
  'update:lng': [value: number]
  'update:address': [value: string]
}>()

const containerId = 'map-picker-container'
const dialogContainerId = 'map-picker-dialog-container'

const {
  loading,
  error,
  suggestions,
  initMap,
  destroyMap,
  setPosition,
  searchKeyword,
  selectSuggestion,
  onCoordChanged,
  onAddressChanged,
} = useMapPicker()

const keyword = ref('')
const dialogKeyword = ref('')
const dialogSuggestions = ref<AutocompleteTip[]>([])
const dialogVisible = ref(false)
const dialogLat = ref(props.lat)
const dialogLng = ref(props.lng)
const dialogAddress = ref('')
let dialogPickerApi: ReturnType<typeof useMapPicker> | null = null

onCoordChanged((latVal, lngVal) => {
  emit('update:lat', latVal)
  emit('update:lng', lngVal)
})

onAddressChanged((address) => {
  emit('update:address', address)
})

watch(() => [props.lat, props.lng], () => {
  setPosition(props.lat, props.lng)
})

async function onSearch() {
  if (!keyword.value.trim()) {
    suggestions.value = []
    return
  }
  await searchKeyword(keyword.value)
}

async function onSelect(tip: AutocompleteTip) {
  await selectSuggestion(tip)
}

async function onDialogSelect(tip: AutocompleteTip) {
  await dialogPickerApi?.selectSuggestion(tip)
  dialogSuggestions.value = []
}

async function initDialogMap() {
  dialogLat.value = props.lat
  dialogLng.value = props.lng
  dialogAddress.value = ''
  await nextTick()
  try {
    const dp = useMapPicker()
    await dp.initMap(dialogContainerId, props.lat, props.lng)
    dp.onCoordChanged((latVal, lngVal) => {
      dialogLat.value = latVal
      dialogLng.value = lngVal
    })
    dp.onAddressChanged((address) => {
      dialogAddress.value = address
    })
    dialogPickerApi = dp
  } catch {
    dialogSuggestions.value = []
  }
}

function destroyDialogMap() {
  dialogPickerApi?.destroyMap()
  dialogPickerApi = null
}

function confirmDialog() {
  emit('update:lat', dialogLat.value)
  emit('update:lng', dialogLng.value)
  if (dialogAddress.value) {
    emit('update:address', dialogAddress.value)
  }
  setPosition(dialogLat.value, dialogLng.value)
  dialogVisible.value = false
}

async function onDialogSearch() {
  if (!dialogPickerApi || !dialogKeyword.value.trim()) {
    dialogSuggestions.value = []
    return
  }
  dialogSuggestions.value = await dialogPickerApi.searchKeyword(dialogKeyword.value)
}

onMounted(async () => {
  await nextTick()
  try {
    await initMap(containerId, props.lat, props.lng, props.disabled)
  } catch {
    // error ref already set by initMap internals
  }
})

onBeforeUnmount(() => {
  destroyMap()
  dialogPickerApi?.destroyMap()
})
</script>

<style scoped src="./map-picker.scss"></style>
