/**
 * 自定义导航栏安全区计算
 *
 * 地图页右上浮层需要避开系统状态栏与微信胶囊按钮。
 */
import { onMounted, ref } from 'vue'

const DEFAULT_NAVBAR_HEIGHT = 44

export function useNavbarSafeArea() {
  const navbarBottom = ref(0)

  onMounted(() => {
    navbarBottom.value = resolveNavbarBottom()
  })

  return { navbarBottom }
}

export function resolveNavbarBottom(): number {
  const windowInfo =
    typeof uni.getWindowInfo === 'function'
      ? uni.getWindowInfo()
      : uni.getSystemInfoSync()
  const statusBarHeight = windowInfo.statusBarHeight ?? 0
  return statusBarHeight + resolveNavBarHeight(statusBarHeight)
}

function resolveNavBarHeight(statusBarHeight: number): number {
  let navBarHeight = DEFAULT_NAVBAR_HEIGHT

  // #ifdef MP-WEIXIN
  try {
    const menuBtn = uni.getMenuButtonBoundingClientRect()
    navBarHeight = (menuBtn.top - statusBarHeight) * 2 + menuBtn.height
  } catch {
    navBarHeight = DEFAULT_NAVBAR_HEIGHT
  }
  // #endif

  return navBarHeight
}
