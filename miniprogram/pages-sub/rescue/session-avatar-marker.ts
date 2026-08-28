// 会话地图头像 marker 合成：canvas 绘制圆形头像 + 粗色环，导出临时文件（带缓存）
import type { ComponentInternalInstance } from 'vue'

export const AVATAR_MARKER_CANVAS_ID = 'session-avatar-marker-canvas'
const DEFAULT_AVATAR = '/static/images/avatar-default.png'
const CANVAS_SIZE = 100
const RING_RADIUS = 48
const AVATAR_RADIUS = 42

const iconCache = new Map<string, Promise<string>>()
let renderQueue = Promise.resolve()

export function getAvatarMarkerIcon(
  avatarUrl: string,
  accent: string,
  instance: ComponentInternalInstance | null,
): Promise<string> {
  const key = `${avatarUrl || DEFAULT_AVATAR}|${accent}`
  const cached = iconCache.get(key)
  if (cached) return cached
  const pending = renderQueue.then(() => renderAvatarMarker(avatarUrl, accent, instance))
  renderQueue = pending.then(
    () => undefined,
    () => undefined,
  )
  iconCache.set(key, pending)
  void pending.catch(() => {
    if (iconCache.get(key) === pending) iconCache.delete(key)
  })
  return pending
}

function renderAvatarMarker(
  avatarUrl: string,
  accent: string,
  instance: ComponentInternalInstance | null,
): Promise<string> {
  return new Promise((resolve, reject) => {
    const ctx = uni.createCanvasContext(AVATAR_MARKER_CANVAS_ID, instance ?? undefined)
    const source = avatarUrl || DEFAULT_AVATAR
    uni.getImageInfo({
      src: source,
      success: (info) => {
        drawMarker(ctx, info.path, accent)
        exportMarker(ctx, instance, resolve, reject)
      },
      fail: () => {
        if (source !== DEFAULT_AVATAR) {
          uni.getImageInfo({
            src: DEFAULT_AVATAR,
            success: (info) => {
              drawMarker(ctx, info.path, accent)
              exportMarker(ctx, instance, resolve, reject)
            },
            fail: reject,
          })
          return
        }
        reject(new Error('头像加载失败'))
      },
    })
  })
}

function drawMarker(ctx: UniApp.CanvasContext, imagePath: string, accent: string): void {
  const size = CANVAS_SIZE
  const center = size / 2
  ctx.clearRect(0, 0, size, size)
  // 粗色环
  ctx.beginPath()
  ctx.arc(center, center, RING_RADIUS, 0, Math.PI * 2)
  // 微信小程序旧版 Canvas 不识别 hsl()，无效颜色会回退成黑色。
  ctx.setFillStyle(toCanvasColor(accent))
  ctx.fill()
  // 圆形头像
  ctx.save()
  ctx.beginPath()
  ctx.arc(center, center, AVATAR_RADIUS, 0, Math.PI * 2)
  ctx.clip()
  ctx.drawImage(
    imagePath,
    size / 2 - AVATAR_RADIUS,
    size / 2 - AVATAR_RADIUS,
    AVATAR_RADIUS * 2,
    AVATAR_RADIUS * 2,
  )
  ctx.restore()
}

function toCanvasColor(color: string): string {
  const match = color
    .trim()
    .match(/^hsl\(\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)%\s*,\s*(\d+(?:\.\d+)?)%\s*\)$/i)
  if (!match) return color

  const hue = (((Number(match[1]) % 360) + 360) % 360) / 360
  const saturation = Math.min(100, Math.max(0, Number(match[2]))) / 100
  const lightness = Math.min(100, Math.max(0, Number(match[3]))) / 100
  const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation
  const segment = hue * 6
  const x = chroma * (1 - Math.abs((segment % 2) - 1))
  const matchRgb =
    segment < 1
      ? [chroma, x, 0]
      : segment < 2
        ? [x, chroma, 0]
        : segment < 3
          ? [0, chroma, x]
          : segment < 4
            ? [0, x, chroma]
            : segment < 5
              ? [x, 0, chroma]
              : [chroma, 0, x]
  const offset = lightness - chroma / 2
  const channels = matchRgb.map((channel) => Math.round((channel + offset) * 255))
  return `#${channels.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`
}

function exportMarker(
  ctx: UniApp.CanvasContext,
  instance: ComponentInternalInstance | null,
  resolve: (path: string) => void,
  reject: (reason?: unknown) => void,
): void {
  ctx.draw(false, () => {
    uni.canvasToTempFilePath(
      {
        canvasId: AVATAR_MARKER_CANVAS_ID,
        destWidth: CANVAS_SIZE,
        destHeight: CANVAS_SIZE,
        fileType: 'png',
        quality: 1,
        success: (result) => resolve(result.tempFilePath),
        fail: reject,
      },
      instance ?? undefined,
    )
  })
}
