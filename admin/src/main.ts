import { createApp } from 'vue'
import { createPinia } from 'pinia'
import {
  ElAlert,
  ElAvatar,
  ElButton,
  ElCheckbox,
  ElCol,
  ElDescriptions,
  ElDescriptionsItem,
  ElDrawer,
  ElEmpty,
  ElForm,
  ElFormItem,
  ElIcon,
  ElImage,
  ElInput,
  ElInputNumber,
  ElMenu,
  ElMenuItem,
  ElOption,
  ElPagination,
  ElPopconfirm,
  ElRow,
  ElSelect,
  ElSkeleton,
  ElSkeletonItem,
  ElSubMenu,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTimeline,
  ElTimelineItem,
  ElUpload,
  ElLoading,
} from 'element-plus'
import 'element-plus/dist/index.css'
import './styles/global.scss'
import App from './App.vue'
import router from './router'

const app = createApp(App)
const elementComponents = [
  ElAlert,
  ElAvatar,
  ElButton,
  ElCheckbox,
  ElCol,
  ElDescriptions,
  ElDescriptionsItem,
  ElDrawer,
  ElEmpty,
  ElForm,
  ElFormItem,
  ElIcon,
  ElImage,
  ElInput,
  ElInputNumber,
  ElMenu,
  ElMenuItem,
  ElOption,
  ElPagination,
  ElPopconfirm,
  ElRow,
  ElSelect,
  ElSkeleton,
  ElSkeletonItem,
  ElSubMenu,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTimeline,
  ElTimelineItem,
  ElUpload,
]

for (const component of elementComponents) {
  app.use(component)
}

app.use(createPinia())
app.use(router)
app.use(ElLoading)

app.mount('#app')
