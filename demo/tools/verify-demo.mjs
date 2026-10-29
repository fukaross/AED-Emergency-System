#!/usr/bin/env node
// TASK-002 数据校验入口：核对 config/site.json 与 demo/ 合成数据的字段、引用、
// 校区一致性与 GCJ-02 距离性质（最近排序、校区归属、服务半径内/边界/外）。
// 用法（仓库根目录）：node demo/tools/verify-demo.mjs
// 退出码 0 表示全部通过；非 0 表示有失败项并打印明细。
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..', '..')
const dataDir = join(root, 'demo', 'data')
const EARTH_RADIUS_M = 6371000
const toRad = (deg) => (deg * Math.PI) / 180

const failures = []
const checks = []

function check(name, ok, detail) {
  checks.push({ name, ok, detail })
  if (!ok) failures.push(detail ? `${name}: ${detail}` : name)
}

function readJson(path) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'))
  } catch (error) {
    check(`JSON 可解析 ${path.replace(root, '')}`, false, error.message)
    return null
  }
}

/** 与后端 RescuerEndpoints / 小程序 geo-distance.ts 相同的 Haversine 实现（R=6371000）。 */
function distanceMeters(a, b) {
  const dLat = toRad(b.lat - a.lat)
  const dLng = toRad(b.lng - a.lng)
  const sinLat = Math.sin(dLat / 2)
  const sinLng = Math.sin(dLng / 2)
  const h = sinLat * sinLat + Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * sinLng * sinLng
  return Math.round(2 * EARTH_RADIUS_M * Math.asin(Math.sqrt(h)))
}

const isCoord = (value, max) => typeof value === 'number' && Number.isFinite(value) && Math.abs(value) <= max
const isIsoDate = (value) => typeof value === 'string' && !Number.isNaN(Date.parse(value))
const isLocalAsset = (value) => typeof value === 'string' && value.startsWith('/static/')

/** 后端 IdNumberValidator：18 位 + 加权校验位。 */
function isValidSyntheticIdNumber(idNumber) {
  if (!/^\d{17}[\dXx]$/.test(idNumber)) return false
  if (!idNumber.startsWith('990000')) return false
  const weights = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2]
  const checkMap = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2']
  let sum = 0
  for (let i = 0; i < 17; i++) sum += Number(idNumber[i]) * weights[i]
  return idNumber[17].toUpperCase() === checkMap[sum % 11]
}

/** 与后端 RescuerEndpoints.ResolveAvailabilityStatus 相同的判定。 */
function availability(profile, radiusMeters, campusByCode) {
  const center = campusByCode.get(profile.campus)
  const distance = distanceMeters(center, profile)
  if (profile.qualificationStatus !== 'active') return { status: 'offline', reason: 'qualification_inactive', distance }
  return distance > radiusMeters
    ? { status: 'offline', reason: 'out_of_service_radius', distance }
    : { status: 'online', reason: null, distance }
}

function nearestCampus(point) {
  return campusList
    .map((campus) => ({ code: campus.code, distance: distanceMeters(point, campus.center) }))
    .sort((a, b) => a.distance - b.distance)[0]
}

// ---- 设计期望（改动数据时应同步更新并说明理由；用于锁定 TEST-002 证据） ----
const EXPECTED_DEVICE_CAMPUS = {
  'AED-DEMO-01': 'east', 'AED-DEMO-02': 'east', 'AED-DEMO-03': 'east', 'AED-DEMO-04': 'east',
  'AED-DEMO-05': 'east', 'AED-DEMO-06': 'west', 'AED-DEMO-07': 'west', 'AED-DEMO-08': 'west',
  'AED-DEMO-09': 'east', 'AED-DEMO-10': 'east',
}
const EXPECTED_DEVICE_DISTANCE = {
  'AED-DEMO-01': 120, 'AED-DEMO-02': 260, 'AED-DEMO-03': 520, 'AED-DEMO-04': 900,
  'AED-DEMO-05': 1500, 'AED-DEMO-06': 200, 'AED-DEMO-07': 600, 'AED-DEMO-08': 1100,
  'AED-DEMO-09': 30000, 'AED-DEMO-10': 36000,
}
const EXPECTED_PROFILE_DISTANCE = {
  'DEMO-RSP-001': 150, 'DEMO-RSP-002': 300, 'DEMO-RSP-003': 420, 'DEMO-RSP-004': 36000,
  'DEMO-RSP-005': 30000, 'DEMO-RSP-006': 180, 'DEMO-RSP-007': 700,
}
const EXPECTED_AVAILABILITY = {
  'DEMO-RSP-001': 'online', 'DEMO-RSP-002': 'offline', 'DEMO-RSP-003': 'online',
  'DEMO-RSP-004': 'offline', 'DEMO-RSP-005': 'online', 'DEMO-RSP-006': 'online',
  'DEMO-RSP-007': 'online',
}

const site = readJson(join(root, 'config', 'site.json'))
const roles = readJson(join(root, 'demo', 'roles.json'))
const files = {}
for (const name of readdirSync(dataDir).filter((file) => file.endsWith('.json')).sort()) {
  files[name.replace('.json', '')] = readJson(join(dataDir, name))
}
const REQUIRED_DATA_FILES = [
  'wechat-experience-users', 'admin-users', 'admin-user-profiles', 'admin-audit-logs',
  'aed-devices', 'rescuer-profiles', 'knowledge-summary', 'courses', 'quick-actions',
  'help-requests', 'rescue-sessions',
]
for (const name of REQUIRED_DATA_FILES) check(`必需数据文件存在 ${name}.json`, Boolean(files[name]))

// ---- 站点配置 ----
check('site.schemaVersion = 1', site?.schemaVersion === 1)
const config = site?.site ?? {}
const sameKeys = (object, expected) =>
  Object.keys(object ?? {}).sort().join(',') === [...expected].sort().join(',')
check('站点配置根键仅 schemaVersion/site', sameKeys(site, ['schemaVersion', 'site']),
  Object.keys(site ?? {}).join(','))
check('site 键集合与 CONTRACT-001 一致', sameKeys(config, [
  'organizationName', 'appTitle', 'adminTitle', 'coordinateSystem', 'map',
  'campuses', 'serviceRadiusKm', 'rescuePhone',
]), Object.keys(config).join(','))
check('map 键仅 center/searchCity', sameKeys(config.map, ['center', 'searchCity']), Object.keys(config.map ?? {}).join(','))
check('map.center 键仅 latitude/longitude', sameKeys(config.map?.center, ['latitude', 'longitude']))
check('site.coordinateSystem = gcj02', config.coordinateSystem === 'gcj02')
for (const field of ['organizationName', 'appTitle', 'adminTitle']) {
  check(`site.${field} 非空`, typeof config[field] === 'string' && config[field].trim().length > 0)
}
check('serviceRadiusKm 为正数', typeof config.serviceRadiusKm === 'number' && config.serviceRadiusKm > 0)
check('rescuePhone 为字符串且不含真实号码', config.rescuePhone === '')
check('map.center 合法', isCoord(config.map?.center?.latitude, 90) && isCoord(config.map?.center?.longitude, 180))
check('map.searchCity 非空', typeof config.map?.searchCity === 'string' && config.map.searchCity.trim().length > 0)
check('至少 2 个校区', Array.isArray(config.campuses) && config.campuses.length >= 2)
const campuses = Array.isArray(config.campuses) ? config.campuses : []
const campusList = []
const campusByCode = new Map()
for (const campus of campuses) {
  check(`校区 code 格式合法 (${campus.code})`, /^[a-z0-9]+(-[a-z0-9]+)*$/.test(campus.code ?? ''))
  check(`校区 code 唯一 (${campus.code})`, !campusByCode.has(campus.code))
  check(`校区 name 非空 (${campus.code})`, typeof campus.name === 'string' && campus.name.trim().length > 0)
  check(`校区键仅 code/name/center (${campus.code})`, sameKeys(campus, ['code', 'name', 'center']))
  check(`校区中心点合法 (${campus.code})`, isCoord(campus.center?.latitude, 90) && isCoord(campus.center?.longitude, 180))
  const center = { lat: campus.center?.latitude, lng: campus.center?.longitude }
  campusByCode.set(campus.code, center)
  campusList.push({ code: campus.code, name: campus.name, center })
}
const radiusMeters = Math.round((config.serviceRadiusKm ?? 0) * 1000)

// ---- 用户与身份档案 ----
const users = files['wechat-experience-users']?.users ?? []
const adminUsers = files['admin-users']?.users ?? []
const adminProfiles = files['admin-user-profiles']?.profiles ?? {}
const auditLogs = files['admin-audit-logs']?.logs ?? []
check('admin-users 初始为空', Array.isArray(adminUsers) && adminUsers.length === 0)
check('admin-audit-logs 初始为空', Array.isArray(auditLogs) && auditLogs.length === 0)

const openIds = new Set()
const phones = new Set()
for (const user of users) {
  check(`用户 openId 唯一 (${user.openId})`, !openIds.has(user.openId))
  openIds.add(user.openId)
  check(`用户手机号格式合法 (${user.openId})`, /^1[3-9]\d{9}$/.test(user.phone ?? ''))
  check(`用户手机号唯一 (${user.openId})`, !phones.has(user.phone))
  phones.add(user.phone)
  check(`用户姓名为非空合成标签 (${user.openId})`, typeof user.name === 'string' && user.name.includes('演示'))
  check(`用户无真实 token (${user.openId})`, user.sessionTokenHash === '')
  check(`用户无订阅残留 (${user.openId})`, user.subscriptions && Object.keys(user.subscriptions).length === 0)
  check(`用户时间戳可解析 (${user.openId})`, isIsoDate(user.createdAt) && isIsoDate(user.lastLoginAt))
  check(`用户头像为本地占位 (${user.openId})`, isLocalAsset(user.avatarUrl))
}

const VALID_PRIMARY_ROLES = new Set(['student', 'teacher', 'staff', 'admin'])
const VALID_ACCOUNT_STATUSES = new Set(['active', 'disabled'])
const VALID_RESPONDER_STATUSES = new Set(['active', 'inactive'])
const VALID_LEVELS = new Set(['honor', 'trainee', 'official', 'core'])
check('admin-user-profiles 覆盖全部用户', Object.keys(adminProfiles).length === openIds.size)
for (const [openId, profile] of Object.entries(adminProfiles)) {
  check(`身份档案键与 openId 一致 (${openId})`, profile.openId === openId && openIds.has(openId))
  check(`primaryRole 合法 (${openId})`, VALID_PRIMARY_ROLES.has(profile.primaryRole))
  check(`accountStatus 合法 (${openId})`, VALID_ACCOUNT_STATUSES.has(profile.accountStatus))
  check(`responderStatus 合法或为空 (${openId})`, profile.responderStatus === null || VALID_RESPONDER_STATUSES.has(profile.responderStatus))
  check(`level 合法或为空 (${openId})`, profile.level === null || VALID_LEVELS.has(profile.level))
}

// ---- 急救员档案 ----
const profiles = files['rescuer-profiles'] ?? []
const profilesById = new Map()
const profileByBoundOpenId = new Map()
for (const profile of profiles) {
  check(`档案 id 唯一 (${profile.id})`, !profilesById.has(profile.id))
  profilesById.set(profile.id, profile)
  check(`档案 recordId 非空 (${profile.id})`, typeof profile.recordId === 'string' && profile.recordId.length > 0)
  check(`档案 campus 来自站点配置 (${profile.id})`, campusByCode.has(profile.campus))
  check(`档案 qualificationStatus 合法 (${profile.id})`, ['active', 'inactive'].includes(profile.qualificationStatus))
  check(`档案 level 合法 (${profile.id})`, profile.level === null || VALID_LEVELS.has(profile.level))
  check(`档案坐标合法 (${profile.id})`, isCoord(profile.lat, 90) && isCoord(profile.lng, 180))
  check(`档案身份证为 990000 合成段且校验位正确 (${profile.id})`, isValidSyntheticIdNumber(profile.idNumber ?? ''))
  check(`档案手机号为合成段 (${profile.id})`, /^130000000\d{2}$/.test(profile.phone ?? ''))
  const certNo = profile.certificateNo ?? ''
  check(`档案证书脱敏值与现有规则一致 (${profile.id})`,
    certNo.length >= 8 && profile.certificateNoMasked === `${certNo.slice(0, 4)}****${certNo.slice(-4)}`)
  check(`档案来源指向 demo 数据而非原始档案 (${profile.id})`,
    profile.sourceFile === 'demo/data/rescuer-profiles.json' && profile.sourceSheet === 'demo-v1' &&
    typeof profile.sourceRowNo === 'number' && profile.sourceSeqNo === profile.id && profile.remark?.includes('合成演示'))
  check(`档案头像为本地占位 (${profile.id})`, isLocalAsset(profile.avatarUrl))
  if (profile.boundOpenId !== null) {
    check(`档案绑定用户存在 (${profile.id})`, openIds.has(profile.boundOpenId))
    check(`同一用户仅一条绑定档案 (${profile.id})`, !profileByBoundOpenId.has(profile.boundOpenId))
    profileByBoundOpenId.set(profile.boundOpenId, profile)
    const boundUser = users.find((user) => user.openId === profile.boundOpenId)
    check(`绑定档案与绑定用户手机号一致 (${profile.id})`, boundUser?.phone === profile.phone)
  } else {
    check(`未绑定档案的 boundOpenId 为 null (${profile.id})`, profile.boundOpenId === null)
  }
}

// ---- 课程 / 关键步骤 / 知识统计 ----
const courses = files['courses'] ?? []
const courseIds = new Set()
for (const course of courses) {
  check(`课程 id 唯一 (${course.id})`, !courseIds.has(course.id))
  courseIds.add(course.id)
  check(`课程类型合法 (${course.id})`, ['article', 'video'].includes(course.type))
  check(`课程封面为本地占位 (${course.id})`, isLocalAsset(course.thumbnail))
  check(`课程正文为中性演示内容 (${course.id})`, typeof course.content === 'string' && course.content.includes('演示'))
  check(`课程免责声明非空 (${course.id})`, typeof course.disclaimer === 'string' && course.disclaimer.length > 0)
  check(`课程时间戳可解析 (${course.id})`, isIsoDate(course.createdAt))
  check(`课程阅读量非负 (${course.id})`, Number.isInteger(course.viewCount) && course.viewCount >= 0)
  check(`课程视频 URL 非空或为空列表 (${course.id})`, course.videos.every((video) => typeof video.url === 'string' && video.url.length > 0))
  const orders = course.steps.map((step) => step.order)
  check(`课程步骤 order 唯一 (${course.id})`, new Set(orders).size === orders.length)
  check(`课程步骤图片为本地占位 (${course.id})`, course.steps.every((step) => isLocalAsset(step.image)))
}
const quickActions = files['quick-actions'] ?? []
const quickActionIds = new Set()
for (const action of quickActions) {
  check(`关键步骤 id 唯一 (${action.id})`, !quickActionIds.has(action.id))
  quickActionIds.add(action.id)
  const orders = action.steps.map((step) => step.order)
  check(`关键步骤 order 唯一 (${action.id})`, new Set(orders).size === orders.length)
  check(`关键步骤图片为本地占位 (${action.id})`, action.steps.every((step) => isLocalAsset(step.image)))
}
const knowledge = files['knowledge-summary'] ?? {}
const KNOWN_STAT_KEYS = new Set(['aed_count', 'trained_people', 'certified_people', 'coverage_rate'])
const statKeys = (knowledge.items ?? []).map((item) => item.key)
check('知识统计 key 合法且唯一', statKeys.every((key) => KNOWN_STAT_KEYS.has(key)) && new Set(statKeys).size === statKeys.length)
check('知识统计 updatedAt 可解析', isIsoDate(knowledge.updatedAt))

// ---- AED 设备 ----
const devices = files['aed-devices'] ?? []
const deviceIds = new Set()
const deviceNames = new Set()
for (const device of devices) {
  check(`设备 id 唯一 (${device.id})`, !deviceIds.has(device.id))
  deviceIds.add(device.id)
  check(`设备名称唯一 (${device.id})`, !deviceNames.has(device.name))
  deviceNames.add(device.name)
  check(`设备状态合法 (${device.id})`, ['online', 'offline'].includes(device.status))
  check(`离线设备必须带状态备注 (${device.id})`, device.status !== 'offline' || (device.statusNote ?? '').length > 0)
  check(`设备坐标合法 (${device.id})`, isCoord(device.lat, 90) && isCoord(device.lng, 180))
  check(`设备图片为本地占位 (${device.id})`, isLocalAsset(device.image))
  check(`设备地址标注合成点位 (${device.id})`, typeof device.address === 'string' && device.address.includes('合成'))
}
const aedCount = (knowledge.items ?? []).find((item) => item.key === 'aed_count')
check('知识统计 AED 数量与设备数一致', aedCount?.value === devices.length, `knowledge=${aedCount?.value} devices=${devices.length}`)
check('初始求助为空', Array.isArray(files['help-requests']?.requests) && files['help-requests'].requests.length === 0)
check('初始会话为空', Array.isArray(files['rescue-sessions']?.sessions) && files['rescue-sessions'].sessions.length === 0)

// ---- 固定演示角色 ----
const ROLE_NAMES = ['requester', 'active-rescuer', 'unqualified']
const demoRoles = roles?.demoRoles ?? []
check('roles.json dataVersion = demo-v1', roles?.dataVersion === 'demo-v1')
check('roles.json schemaVersion = 1', roles?.schemaVersion === 1)
check('三个固定演示角色齐全', demoRoles.length === 3 && ROLE_NAMES.every((role) => demoRoles.some((item) => item.role === role)))
check('演示登录 code 唯一', new Set(demoRoles.map((item) => item.loginCode)).size === demoRoles.length)
for (const item of demoRoles) {
  check(`角色用户存在 (${item.role})`, openIds.has(item.openId))
  check(`角色 campus 来自站点配置 (${item.role})`, campusByCode.has(item.campus))
  const profile = item.responderProfileId ? profilesById.get(item.responderProfileId) : null
  if (item.responderProfileId) check(`角色档案存在 (${item.role})`, Boolean(profile))
  const certified = Boolean(profile && profile.qualificationStatus === 'active' && profile.boundOpenId === item.openId)
  check(`角色认证预期自洽 (${item.role})`, certified === item.expectedCertifiedResponder)
  check(`角色资格状态与档案一致 (${item.role})`, (profile?.qualificationStatus ?? null) === item.qualificationStatus)
  if (item.role === 'requester') check('requester 未绑定急救员档案', Boolean(profileByBoundOpenId.get(item.openId)) === false)
  if (item.role === 'unqualified') check('unqualified 档案资格为 inactive', profile?.qualificationStatus === 'inactive')
  if (item.role === 'active-rescuer') {
    check('active-rescuer 绑定档案资格 active 且属于同校区', profile?.campus === item.campus && profile?.boundOpenId === item.openId)
  }
}
for (const sample of roles?.negativeSamples ?? []) {
  const profile = profilesById.get(sample.responderProfileId)
  check(`负向样本账号状态一致 (${sample.id})`, adminProfiles[sample.openId]?.accountStatus === sample.accountStatus)
  check(`负向样本档案资格为 active（账号状态优先） (${sample.id})`, profile?.qualificationStatus === 'active')
}

// ---- GCJ-02 距离性质 ----
// 校区配置无效或 code 重复时跳过距离计算，避免 undefined 中心点抛异常；此类问题已由上方检查记为失败。
const campusesValid = campusList.length >= 2
  && new Set(campusList.map((campus) => campus.code)).size === campusList.length
  && campusList.every((campus) => isCoord(campus.center.lat, 90) && isCoord(campus.center.lng, 180))
const eastCenter = campusByCode.get('east')
const westCenter = campusByCode.get('west')
const geoRows = []
let eastNear3000 = []
let westNear3000 = []
let eastNear30000 = []
let eastNear29999 = []
if (campusesValid) {
  check('viewer 在东区中心时最近校区为 east', nearestCampus(eastCenter).code === 'east')
  check('viewer 在西区中心时最近校区为 west', nearestCampus(westCenter).code === 'west')

  for (const device of devices) {
    const own = nearestCampus(device)
    const ownDistance = distanceMeters(campusByCode.get(own.code), device)
    check(`设备 ${device.id} 最接近配置中的归属校区 ${EXPECTED_DEVICE_CAMPUS[device.id]}`,
      own.code === EXPECTED_DEVICE_CAMPUS[device.id], `实际=${own.code}`)
    check(`设备 ${device.id} 到归属校区距离为设计的整数米`,
      ownDistance === EXPECTED_DEVICE_DISTANCE[device.id], `实际=${ownDistance}`)
    const klass = ownDistance < radiusMeters ? '内' : ownDistance === radiusMeters ? '边界' : '外'
    geoRows.push({ kind: 'AED', id: device.id, campus: own.code, distance: ownDistance, klass })
  }
  for (const profile of profiles) {
    if (!campusByCode.has(profile.campus)) continue
    check(`档案 ${profile.id} campus 与最近校区一致`, nearestCampus(profile).code === profile.campus)
    const result = availability(profile, radiusMeters, campusByCode)
    check(`档案 ${profile.id} 到校区中心距离为设计的整数米`,
      result.distance === EXPECTED_PROFILE_DISTANCE[profile.id], `实际=${result.distance}`)
    check(`档案 ${profile.id} 在线/离线判定符合设计`,
      result.status === EXPECTED_AVAILABILITY[profile.id], `实际=${result.status}(${result.reason})`)
    const klass = result.distance < radiusMeters ? '内' : result.distance === radiusMeters ? '边界' : '外'
    geoRows.push({ kind: 'RSP', id: profile.id, campus: profile.campus, distance: result.distance, klass, status: result.status })
  }

  /** 与 SqliteAedDeviceStore.GetNearbyAsync 相同：<= radius 过滤，online 优先，再按距离升序。 */
  const simulateAedNearby = (queryPoint, radius) => devices
    .map((device) => ({ id: device.id, status: device.status, distance: distanceMeters(queryPoint, device) }))
    .filter((item) => item.distance <= radius)
    .sort((a, b) => (b.status === 'online' ? 1 : 0) - (a.status === 'online' ? 1 : 0) || a.distance - b.distance)

  eastNear3000 = simulateAedNearby(eastCenter, 3000).map((item) => item.id)
  check('东区中心 3000m 内最近排序符合预期（在线优先+距离升序）',
    eastNear3000.join(',') === 'AED-DEMO-01,AED-DEMO-02,AED-DEMO-03,AED-DEMO-04,AED-DEMO-05', eastNear3000.join(','))
  westNear3000 = simulateAedNearby(westCenter, 3000).map((item) => item.id)
  check('西区中心 3000m 内最近排序符合预期',
    westNear3000.join(',') === 'AED-DEMO-06,AED-DEMO-07,AED-DEMO-08', westNear3000.join(','))
  eastNear30000 = simulateAedNearby(eastCenter, radiusMeters)
  check(`东区中心 ${radiusMeters}m 查询包含边界设备 AED-DEMO-09`, eastNear30000.some((item) => item.id === 'AED-DEMO-09'))
  check(`东区中心 ${radiusMeters}m 查询不包含超界设备 AED-DEMO-10`, !eastNear30000.some((item) => item.id === 'AED-DEMO-10'))
  check(`东区中心 ${radiusMeters}m 查询命中 ${devices.length - 1} 台设备`, eastNear30000.length === devices.length - 1, `实际=${eastNear30000.length}`)
  eastNear29999 = simulateAedNearby(eastCenter, radiusMeters - 1)
  check(`${radiusMeters - 1}m 查询排除恰好边界的 AED-DEMO-09`, !eastNear29999.some((item) => item.id === 'AED-DEMO-09'))
  check('36000m 查询包含全部设备', simulateAedNearby(eastCenter, 36000).length === devices.length)
} else {
  check('校区配置有效（距离性质校验前置条件）', false, '校区缺失、code 重复或坐标非法，已跳过距离计算')
}

// ---- 合成数据性质与泄露扫描 ----
const dataTexts = []
const collectStrings = (value, out) => {
  if (typeof value === 'string') out.push(value)
  else if (Array.isArray(value)) value.forEach((item) => collectStrings(item, out))
  else if (value && typeof value === 'object') Object.values(value).forEach((item) => collectStrings(item, out))
  return out
}
for (const value of Object.values(files)) dataTexts.push(...collectStrings(value, []))
dataTexts.push(...collectStrings(roles, []))

const externalUrls = dataTexts.filter((text) => /https?:\/\//i.test(text))
check('demo 数据不含外部 URL（避免未核查授权素材/现网地址）', externalUrls.length === 0, externalUrls.slice(0, 3).join(' | '))
const idNumberHits = dataTexts.filter((text) => /(?<!\d)\d{18}(?!\d)/.test(text))
check('demo 数据中的 18 位号码均为 990000 合成段',
  idNumberHits.every((text) => /(?<!\d)990000\d{11}[\dXx](?!\d)/.test(text)), idNumberHits.slice(0, 3).join(' | '))
const phoneHits = dataTexts.filter((text) => /(?<!\d)1[3-9]\d{9}(?!\d)/.test(text))
check('demo 数据中的手机号均为 130000000xx 合成段',
  phoneHits.every((text) => /(?<!\d)130000000\d{2}(?!\d)/.test(text)), phoneHits.slice(0, 3).join(' | '))
const credentialKeys = []
const collectKeys = (value) => {
  if (Array.isArray(value)) value.forEach(collectKeys)
  else if (value && typeof value === 'object') {
    for (const [key, child] of Object.entries(value)) {
      if (/(secret|password|passwd|privatekey|apikey|accesskey|credential)/i.test(key)) credentialKeys.push(key)
      collectKeys(child)
    }
  }
}
collectKeys(site)
check('config/site.json 不含凭据字段', credentialKeys.length === 0, credentialKeys.join(','))

// ---- 输出 ----
const pad = (text, width) => String(text).padEnd(width)
console.log(`站点配置：${config.organizationName} / ${config.appTitle} / 服务半径 ${config.serviceRadiusKm} km / 校区 ${campuses.map((c) => `${c.code}:${c.name}`).join(', ')}`)
console.log(`数据规模：用户 ${users.length} / 档案 ${profiles.length} / AED ${devices.length} / 课程 ${courses.length} / 关键步骤 ${quickActions.length}`)
console.log('')
console.log(pad('类型', 5) + pad('ID', 16) + pad('校区', 6) + pad('距离(m)', 10) + pad('半径分类', 9) + '判定')
for (const row of geoRows) {
  console.log(pad(row.kind, 5) + pad(row.id, 16) + pad(row.campus, 6) + pad(row.distance, 10) + pad(row.klass, 9) + (row.status ?? ''))
}
console.log('')
console.log(`东区中心 3000m 查询：${eastNear3000.join(' -> ')}`)
console.log(`西区中心 3000m 查询：${westNear3000.join(' -> ')}`)
console.log(`东区中心 ${radiusMeters}m 查询命中 ${eastNear30000.length} 台（含边界 AED-DEMO-09，排除 AED-DEMO-10）`)
console.log(`东区中心 ${radiusMeters - 1}m 查询命中 ${eastNear29999.length} 台（排除恰好边界设备）`)
console.log('')
const failed = checks.filter((item) => !item.ok)
console.log(`检查项：${checks.length} 通过 ${checks.length - failed.length} 失败 ${failed.length}`)
for (const item of failed) console.log(`FAIL ${item.name}${item.detail ? ` -> ${item.detail}` : ''}`)
process.exit(failed.length === 0 ? 0 : 1)
