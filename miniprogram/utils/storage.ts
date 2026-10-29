// 本地缓存的类型安全封装，支持 TTL 过期

interface CacheEntry<T> {
  readonly data: T
  readonly expireAt: number | null
}

/** 写入缓存 */
export function setCache<T>(key: string, data: T, ttlMs?: number): void {
  const entry: CacheEntry<T> = {
    data,
    expireAt: ttlMs ? Date.now() + ttlMs : null,
  }
  try {
    uni.setStorageSync(key, JSON.stringify(entry))
  } catch (e) {
    console.warn(`[Storage] setCache failed for key="${key}"`, e)
  }
}

/** 读取缓存，过期返回 null */
export function getCache<T>(key: string): T | null {
  try {
    const raw = uni.getStorageSync(key)
    if (!raw) return null

    const entry: CacheEntry<T> = JSON.parse(raw as string)
    if (entry.expireAt && Date.now() > entry.expireAt) {
      uni.removeStorageSync(key)
      return null
    }
    return entry.data
  } catch {
    return null
  }
}

/** 移除缓存 */
export function removeCache(key: string): void {
  try {
    uni.removeStorageSync(key)
  } catch {
    // silent
  }
}

/** 清除所有缓存 */
export function clearAllCache(): void {
  try {
    uni.clearStorageSync()
  } catch {
    // silent
  }
}

/** 获取缓存占用大小 (字节) */
export function getCacheSize(): number {
  try {
    const info = uni.getStorageInfoSync()
    return (info.currentSize ?? 0) * 1024
  } catch {
    return 0
  }
}
