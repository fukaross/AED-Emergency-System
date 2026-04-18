<script setup lang="ts">
/**
 * AED 急救小程序 — 应用入口
 * REDACTED AED Emergency Mini-Program
 */
import { onLaunch, onShow, onHide } from '@dcloudio/uni-app'

onLaunch(() => {
  console.log('[AED] App Launch')
})

onShow(() => {
  console.log('[AED] App Show')
})

onHide(() => {
  console.log('[AED] App Hide')
})
</script>

<style lang="scss">
@import './common/styles/reset.scss';
</style>
