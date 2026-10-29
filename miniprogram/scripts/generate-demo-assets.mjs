#!/usr/bin/env node
// 生成自建通用演示素材（PNG），替换未确认授权的照片。
//
// 说明：这些图全部是脚本绘制的几何占位图，不含任何第三方照片、人脸、校徽、二维码
// 或 EXIF 元数据，可以随源码分发。输出固定为 static/images/ 下的实际引用文件名。
// 重新生成：pnpm assets:generate（在 miniprogram/ 下执行）
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { deflateSync } from 'node:zlib'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const miniprogramRoot = resolve(scriptDir, '..')
const outputDir = resolve(miniprogramRoot, 'static/images')

// ===== 最小 PNG 编码器（RGBA8，无第三方依赖）=====
const CRC_TABLE = (() => {
  const table = new Uint32Array(256)
  for (let n = 0; n < 256; n++) {
    let c = n
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    }
    table[n] = c >>> 0
  }
  return table
})()

function crc32(buffer) {
  let c = 0xffffffff
  for (const byte of buffer) {
    c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8)
  }
  return (c ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
  const length = Buffer.alloc(4)
  length.writeUInt32BE(data.length, 0)
  const typeAndData = Buffer.concat([Buffer.from(type, 'ascii'), data])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(typeAndData), 0)
  return Buffer.concat([length, typeAndData, crc])
}

function encodePng(width, height, canvas) {
  const raw = Buffer.alloc((width * 4 + 1) * height)
  for (let y = 0; y < height; y++) {
    const rowStart = y * (width * 4 + 1)
    raw[rowStart] = 0
    canvas.copy(raw, rowStart + 1, y * width * 4, (y + 1) * width * 4)
  }

  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0)
  ihdr.writeUInt32BE(height, 4)
  ihdr[8] = 8
  ihdr[9] = 6
  ihdr[10] = 0
  ihdr[11] = 0
  ihdr[12] = 0

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

// ===== 画布与基础绘制 =====
function createCanvas(width, height, background) {
  const canvas = Buffer.alloc(width * height * 4)
  for (let i = 0; i < width * height; i++) {
    canvas[i * 4] = background[0]
    canvas[i * 4 + 1] = background[1]
    canvas[i * 4 + 2] = background[2]
    canvas[i * 4 + 3] = background[3] ?? 255
  }
  return { width, height, canvas }
}

function blend(canvas, width, x, y, color, alpha = 1) {
  const index = (y * width + x) * 4
  const sourceAlpha = (color[3] ?? 255) / 255 * alpha
  const targetAlpha = canvas[index + 3] / 255
  const outAlpha = sourceAlpha + targetAlpha * (1 - sourceAlpha)
  if (outAlpha === 0) return
  for (let channel = 0; channel < 3; channel++) {
    canvas[index + channel] = Math.round(
      (color[channel] * sourceAlpha + canvas[index + channel] * targetAlpha * (1 - sourceAlpha)) / outAlpha,
    )
  }
  canvas[index + 3] = Math.round(outAlpha * 255)
}

function fillRect(image, x, y, width, height, color) {
  for (let py = y; py < y + height; py++) {
    if (py < 0 || py >= image.height) continue
    for (let px = x; px < x + width; px++) {
      if (px < 0 || px >= image.width) continue
      blend(image.canvas, image.width, px, py, color)
    }
  }
}

function fillCircle(image, cx, cy, radius, color) {
  const r2 = radius * radius
  for (let py = Math.floor(cy - radius); py <= cy + radius; py++) {
    if (py < 0 || py >= image.height) continue
    for (let px = Math.floor(cx - radius); px <= cx + radius; px++) {
      if (px < 0 || px >= image.width) continue
      const dx = px - cx
      const dy = py - cy
      if (dx * dx + dy * dy <= r2) blend(image.canvas, image.width, px, py, color)
    }
  }
}

function strokeCircle(image, cx, cy, radius, thickness, color) {
  const outer = radius * radius
  const inner = (radius - thickness) * (radius - thickness)
  for (let py = Math.floor(cy - radius); py <= cy + radius; py++) {
    if (py < 0 || py >= image.height) continue
    for (let px = Math.floor(cx - radius); px <= cx + radius; px++) {
      if (px < 0 || px >= image.width) continue
      const dx = px - cx
      const dy = py - cy
      const d2 = dx * dx + dy * dy
      if (d2 <= outer && d2 >= inner) blend(image.canvas, image.width, px, py, color)
    }
  }
}

function fillRoundedRect(image, x, y, width, height, radius, color) {
  for (let py = y; py < y + height; py++) {
    if (py < 0 || py >= image.height) continue
    for (let px = x; px < x + width; px++) {
      if (px < 0 || px >= image.width) continue
      const dx = Math.max(x + radius - px, 0, px - (x + width - radius - 1))
      const dy = Math.max(y + radius - py, 0, py - (y + height - radius - 1))
      if (dx * dx + dy * dy <= radius * radius) {
        blend(image.canvas, image.width, px, py, color)
      }
    }
  }
}

function fillLinearGradient(image, topColor, bottomColor) {
  for (let y = 0; y < image.height; y++) {
    const t = y / Math.max(1, image.height - 1)
    const color = [
      Math.round(topColor[0] + (bottomColor[0] - topColor[0]) * t),
      Math.round(topColor[1] + (bottomColor[1] - topColor[1]) * t),
      Math.round(topColor[2] + (bottomColor[2] - topColor[2]) * t),
      255,
    ]
    for (let x = 0; x < image.width; x++) {
      blend(image.canvas, image.width, x, y, color)
    }
  }
}

// ===== 素材定义 =====
const WHITE = [255, 255, 255, 255]
const BRAND_RED = [229, 57, 53, 255]
const DARK = [38, 50, 56, 255]
const GREY = [144, 164, 174, 255]

function buildAvatar() {
  const image = createCanvas(160, 160, [236, 239, 241, 255])
  fillCircle(image, 80, 62, 30, GREY)
  fillCircle(image, 80, 148, 62, GREY)
  return image
}

function buildAedPlaceholder() {
  const image = createCanvas(480, 300, [250, 250, 250, 255])
  fillRoundedRect(image, 120, 84, 240, 132, 18, BRAND_RED)
  fillRoundedRect(image, 140, 104, 200, 92, 10, WHITE)
  fillRect(image, 232, 120, 16, 60, BRAND_RED)
  fillRect(image, 210, 142, 60, 16, BRAND_RED)
  fillRect(image, 120, 232, 240, 10, [207, 216, 220, 255])
  return image
}

function buildCourseCover(accent) {
  const image = createCanvas(640, 360, accent)
  fillLinearGradient(image, accent, [255, 255, 255])
  fillCircle(image, 520, 90, 120, [255, 255, 255, 40])
  fillCircle(image, 120, 300, 160, [255, 255, 255, 32])
  fillRoundedRect(image, 56, 56, 180, 44, 22, [255, 255, 255, 220])
  fillRoundedRect(image, 56, 244, 320, 12, 6, [255, 255, 255, 180])
  fillRoundedRect(image, 56, 272, 220, 12, 6, [255, 255, 255, 140])
  return image
}

function buildEmergencyCall() {
  const image = createCanvas(96, 96, [0, 0, 0, 0])
  fillRoundedRect(image, 0, 0, 96, 96, 24, BRAND_RED)
  fillRoundedRect(image, 30, 22, 36, 52, 10, WHITE)
  fillCircle(image, 48, 34, 6, BRAND_RED)
  fillRect(image, 40, 48, 16, 4, BRAND_RED)
  fillRect(image, 40, 56, 16, 4, BRAND_RED)
  return image
}

function buildProfileBg() {
  const image = createCanvas(750, 320, [229, 57, 53, 255])
  fillLinearGradient(image, [239, 83, 80], [198, 40, 40])
  for (let i = 0; i < 5; i++) {
    strokeCircle(image, 620, 60, 90 + i * 46, 10, [255, 255, 255, 24])
  }
  fillCircle(image, 120, 250, 130, [255, 255, 255, 18])
  return image
}

function buildMarker(fill) {
  const image = createCanvas(64, 96, [0, 0, 0, 0])
  fillCircle(image, 32, 34, 26, fill)
  fillRect(image, 26, 54, 12, 30, fill)
  fillCircle(image, 32, 34, 11, WHITE)
  return image
}

function buildRippleRing() {
  const image = createCanvas(160, 160, [0, 0, 0, 0])
  strokeCircle(image, 80, 80, 74, 6, [229, 57, 53, 200])
  strokeCircle(image, 80, 80, 54, 4, [229, 57, 53, 120])
  strokeCircle(image, 80, 80, 34, 2, [229, 57, 53, 70])
  return image
}

const ASSETS = [
  ['avatar-default.png', buildAvatar()],
  ['aed-placeholder.png', buildAedPlaceholder()],
  ['course-cover-aed.png', buildCourseCover([229, 57, 53, 255])],
  ['course-cover-cpr.png', buildCourseCover([239, 108, 0, 255])],
  ['course-cover-airway.png', buildCourseCover([21, 101, 192, 255])],
  ['course-cover-wound.png', buildCourseCover([46, 125, 50, 255])],
  ['emergency-call.png', buildEmergencyCall()],
  ['profile-bg.png', buildProfileBg()],
  ['aed-marker-online.png', buildMarker([46, 125, 50, 255])],
  ['aed-marker-offline.png', buildMarker([96, 125, 139, 255])],
  ['ripple-ring.png', buildRippleRing()],
]

mkdirSync(outputDir, { recursive: true })

for (const [fileName, image] of ASSETS) {
  const png = encodePng(image.width, image.height, image.canvas)
  writeFileSync(resolve(outputDir, fileName), png)
  console.log(
    `[assets] ${fileName} ${image.width}x${image.height} ${(png.length / 1024).toFixed(1)}KB`,
  )
}

console.log(
  `[assets] 生成 ${ASSETS.length} 个自建演示素材到 static/images/（几何占位图，无第三方照片）`,
)
