<template>
  <view
    v-if="visible"
    class="demo-badge"
    :class="`demo-badge--${modeClass}`"
    @tap="openRoleSwitcher"
  >
    <text class="demo-badge__dot" />
    <text class="demo-badge__text">{{ MODE_LABEL }}</text>
    <text v-if="roleLabel" class="demo-badge__role">· {{ roleLabel }}</text>
  </view>
</template>

<script setup lang="ts">
// 演示模式徽标：明确当前档位与演示角色；点按可切换 demo/roles.json 中的固定角色。
// 真实 wechat 档不渲染（IS_DEMO_MODE=false），避免把真实环境误标为演示。
import { onMounted, ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import {
  listDemoRoles,
  setActiveDemoRole,
  getActiveDemoRole,
} from '@/mock/demo-role'
import { useAuthStore } from '@/store/auth'
import { APP_MODE, IS_DEMO_MODE, IS_LOCAL_DEMO, MODE_LABEL } from '@/utils/runtime-env'

const visible = IS_DEMO_MODE
const modeClass = IS_LOCAL_DEMO ? 'local' : 'mock'
const authStore = useAuthStore()
const roleLabel = ref('')

function refreshRoleLabel(): void {
  if (!visible) return
  try {
    roleLabel.value = getActiveDemoRole().displayName
  } catch {
    roleLabel.value = '演示角色无效'
  }
}

onMounted(refreshRoleLabel)
onShow(refreshRoleLabel)

async function switchRole(loginCode: string): Promise<void> {
  setActiveDemoRole(loginCode)
  authStore.logout()
  try {
    await authStore.login()
    uni.showToast({ title: '演示角色已切换', icon: 'success' })
    refreshRoleLabel()
  } catch (error) {
    console.error('[DemoModeBadge] 切换演示角色后登录失败', error)
    uni.showToast({ title: '切换后登录失败，请查看后端日志', icon: 'none' })
  }
}

function openRoleSwitcher(): void {
  if (APP_MODE === 'wechat') return

  const roles = listDemoRoles()
  uni.showActionSheet({
    itemList: roles.map((role) => `${role.displayName}（${role.loginCode}）`),
    success: (result) => {
      const role = roles[result.tapIndex]
      if (role) void switchRole(role.loginCode)
    },
  })
}
</script>

<style lang="scss" scoped>
.demo-badge {
  position: fixed;
  left: $spacing-md;
  bottom: 220rpx;
  z-index: 20;
  display: flex;
  align-items: center;
  gap: 8rpx;
  padding: 8rpx $spacing-sm;
  border-radius: 999rpx;
  background: rgba(38, 50, 56, 0.82);
  color: #ffffff;
  font-size: 22rpx;
  line-height: 1.4;
}

.demo-badge--local {
  background: rgba(21, 101, 192, 0.86);
}

.demo-badge__dot {
  width: 12rpx;
  height: 12rpx;
  border-radius: 50%;
  background: $status-warning;
}

.demo-badge__text {
  color: #ffffff;
  font-size: 22rpx;
}

.demo-badge__role {
  color: rgba(255, 255, 255, 0.86);
  font-size: 22rpx;
}
</style>
