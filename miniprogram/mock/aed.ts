// AED 设备 Mock 数据 —— 来自 demo/data/aed-devices.json 生成产物（GCJ-02 合成点位）。
import type { AedItem } from '@/types/aed'
import { DEMO_AED_DEVICES } from './generated/demo-aed-devices.generated'

export const AED_MOCK_DATA: readonly AedItem[] = DEMO_AED_DEVICES.map(
  (device) => ({
    id: device.id,
    name: device.name,
    lat: device.lat,
    lng: device.lng,
    status: device.status,
    image: device.image,
    address: device.address,
    lastMaintenance: device.lastMaintenance,
    nextMaintenance: device.nextMaintenance,
    maintainer: device.maintainer,
  }),
)
