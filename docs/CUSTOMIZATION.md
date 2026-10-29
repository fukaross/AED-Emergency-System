# 定制指南（换机构 / 换校区 / 换地图）

## 1. 唯一手工维护真源：`config/site.json`

```json
{
  "schemaVersion": 1,
  "site": {
    "organizationName": "示范园区",
    "appTitle": "AED 急救协作",
    "adminTitle": "AED 管理端",
    "coordinateSystem": "gcj02",
    "map": { "center": { "latitude": 31.2315, "longitude": 121.4785 }, "searchCity": "上海" },
    "campuses": [
      { "code": "east", "name": "东区", "center": { "latitude": 31.245, "longitude": 121.505 } },
      { "code": "west", "name": "西区", "center": { "latitude": 31.218, "longitude": 121.452 } }
    ],
    "serviceRadiusKm": 30,
    "rescuePhone": ""
  }
}
```

| 字段 | 影响范围 | 注意 |
| --- | --- | --- |
| `organizationName` / `appTitle` / `adminTitle` | 三端品牌与标题 | 小程序 `manifest.json` 的 `name` 是**独立手工同步点**（微信端展示名），改标题时两处一起改 |
| `campuses[].code` | 后端校区校验、用户/档案/设备筛选、演示数据归属 | 非空、唯一、稳定，建议小写字母/数字/短横线；**不是源码枚举** |
| `campuses[].name` / `center` | 客户端展示与就近校区推导 | GCJ-02；最近校区按 Haversine 距离，同距按数组顺序 |
| `map.center` / `map.searchCity` | 地图初始视野与地址检索城市 | 地址检索另需 `VITE_TENCENT_MAP_KEY`（小程序 `.env`，另见下） |
| `serviceRadiusKm` | 服务半径（默认 30） | `distance > 半径*1000` 才超界；等于边界不判离线 |
| `rescuePhone` | 求助页急救热线 | 留空时不显示真实拨打；演示模式统一禁拨号 |

## 2. 增加第三校区（不改源码枚举）

1. 在 `config/site.json` 的 `campuses` 数组追加一项（`code` 唯一）。
2. 在 `demo/data/` 中给需要归属该校区的急救员档案/AED 设备补点位（`campus` 字段或按最近中心推导），
   并同步 `demo/tools/verify-demo.mjs` 的期望值（该脚本会校验距离性质）。
3. 执行 `node demo/tools/verify-demo.mjs` 与 `python scripts/verify-sanitization.py --root .`。
4. **重新构建** `miniprogram` 与 `admin`，**重启**后端；不需要改任何校区枚举或后端校验清单。

## 3. 生效时机（不承诺热更新）

| 改动 | 生效方式 |
| --- | --- |
| `config/site.json` | 后端：**重启**（启动时校验 `schemaVersion/校区唯一/坐标/半径`，非法启动即失败）；小程序/Admin：**重新构建** |
| `demo/data/*.json` | 重新执行 `init-demo-data`（目标必须是新的空目录或空目录；已有库不会覆盖）+ 重新生成小程序 Mock |
| `demo/roles.json` | 同 demo 数据；演示 code 只映射到其中登记的角色 |
| `.env*`（小程序/Admin） | 重新构建；Admin 的 `VITE_AMAP_KEY` 缺失时地图区域显示明确不可用状态 |
| Admin 前端源码 | `cd admin; pnpm build`（产物写入 `backend/src/AedBackend.Api/wwwroot/admin`），后端无需重启即可托管静态文件 |

> 安全边界：不要用环境变量覆盖站点内容；不要把真实 AppSecret、管理密钥、OSS 凭据、地图 Key 写入仓库；
> 本地秘密放 `backend/src/AedBackend.Api/appsettings.Development.local.json`（已被忽略）或进程环境变量。

## 4. 改地图/定位相关

- Admin 地图：`admin/.env.local`（或部署环境变量）配置 `VITE_AMAP_KEY` / `VITE_AMAP_SECURITY_CODE`。
- 小程序地址解析：`miniprogram/.env.*` 配置 `VITE_TENCENT_MAP_KEY`；为空时地址解析降级，
  地图服务会打印“Key 未配置，跳过路线规划”，不会渲染假底图。
- 坐标统一 GCJ-02；替换校区中心后请复核演示数据距离性质（`node demo/tools/verify-demo.mjs`）。
