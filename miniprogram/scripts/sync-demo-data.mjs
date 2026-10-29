#!/usr/bin/env node
// 从仓库根 demo/（demo-v1 唯一真源）生成小程序只读 Mock 产物。
//
// 为什么要有这一步：小程序包内不能跨目录引用 demo/（构建源目录之外），而且 Mock
// 不允许手工维护第二份个人档案。因此由本脚本把 demo/ 数据转成带「generated」标记的
// TS 常量，小程序只消费生成产物。手工编辑生成文件会在下次 sync 时被覆盖。
//
// 用法：pnpm sync:demo（在 miniprogram/ 下执行）
// 校验失败以非 0 退出，不写任何文件。
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const miniprogramRoot = resolve(scriptDir, '..')
const repoRoot = resolve(miniprogramRoot, '..')
const demoRoot = resolve(repoRoot, 'demo')
const configPath = resolve(repoRoot, 'config/site.json')
const outputDir = resolve(miniprogramRoot, 'mock/generated')

const SUPPORTED_DEMO_SCHEMA_VERSION = 1
const EXPECTED_DEMO_ROLES = ['requester', 'active-rescuer', 'unqualified']

const failures = []

function fail(message) {
  failures.push(message)
}

function readJson(absolutePath) {
  let text
  try {
    text = readFileSync(absolutePath, 'utf8')
  } catch (error) {
    throw new Error(`读取失败 ${absolutePath}: ${error.message}`)
  }
  try {
    return JSON.parse(text)
  } catch (error) {
    throw new Error(`JSON 解析失败 ${absolutePath}: ${error.message}`)
  }
}

function readArray(absolutePath, label) {
  const value = readJson(absolutePath)
  if (!Array.isArray(value)) throw new Error(`${label} 必须是数组`)
  return value
}

function assertUnique(values, label) {
  const seen = new Set()
  for (const value of values) {
    if (seen.has(value)) fail(`${label} 存在重复值 "${value}"`)
    seen.add(value)
  }
}

function requireString(value, label) {
  if (typeof value !== 'string' || !value.trim()) fail(`${label} 必须是非空字符串`)
  return typeof value === 'string' ? value : ''
}

function requireFiniteNumber(value, label) {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    fail(`${label} 必须是有限数字`)
    return 0
  }
  return value
}

// ===== 站点配置（只用于校验 demo 校区 code，不复制进产物）=====
const siteConfig = readJson(configPath)
if (siteConfig?.schemaVersion !== 1) {
  throw new Error('config/site.json schemaVersion 必须为 1')
}
const campuses = siteConfig?.site?.campuses
if (!Array.isArray(campuses) || campuses.length === 0) {
  throw new Error('config/site.json 缺少 site.campuses')
}
const campusCodes = new Set(campuses.map((campus) => campus.code))

// ===== demo 数据 =====
const rolesFile = readJson(resolve(demoRoot, 'roles.json'))
if (rolesFile?.schemaVersion !== SUPPORTED_DEMO_SCHEMA_VERSION) {
  throw new Error(
    `demo/roles.json schemaVersion 必须为 ${SUPPORTED_DEMO_SCHEMA_VERSION}`,
  )
}
const dataVersion = requireString(rolesFile.dataVersion, 'roles.json.dataVersion')

const devices = readArray(resolve(demoRoot, 'data/aed-devices.json'), 'aed-devices.json')
const rescuerProfiles = readArray(
  resolve(demoRoot, 'data/rescuer-profiles.json'),
  'rescuer-profiles.json',
)
const courses = readArray(resolve(demoRoot, 'data/courses.json'), 'courses.json')
const quickActions = readArray(
  resolve(demoRoot, 'data/quick-actions.json'),
  'quick-actions.json',
)
const knowledgeSummary = readJson(resolve(demoRoot, 'data/knowledge-summary.json'))
const experienceUsers = readJson(
  resolve(demoRoot, 'data/wechat-experience-users.json'),
)
const adminProfiles = readJson(resolve(demoRoot, 'data/admin-user-profiles.json'))

if (!Array.isArray(experienceUsers?.users)) {
  throw new Error('wechat-experience-users.json 缺少 users 数组')
}
if (!adminProfiles?.profiles || typeof adminProfiles.profiles !== 'object') {
  throw new Error('admin-user-profiles.json 缺少 profiles 对象')
}

// ===== 校验：用户与账号状态 =====
const userOpenIds = experienceUsers.users.map((user) =>
  requireString(user?.openId, 'users[].openId'),
)
assertUnique(userOpenIds, 'users[].openId')
const userOpenIdSet = new Set(userOpenIds)

const demoUsers = experienceUsers.users.map((user) => {
  const openId = requireString(user?.openId, 'users[].openId')
  const adminProfile = adminProfiles.profiles[openId]
  if (!adminProfile) fail(`用户 ${openId} 缺少 admin-user-profiles 身份记录`)
  const accountStatus = adminProfile?.accountStatus
  if (accountStatus !== 'active' && accountStatus !== 'disabled') {
    fail(`用户 ${openId} 的 accountStatus 必须是 active|disabled`)
  }
  const primaryRole = adminProfile?.primaryRole
  if (!['student', 'teacher', 'staff'].includes(primaryRole)) {
    fail(`用户 ${openId} 的 primaryRole 必须是 student|teacher|staff`)
  }
  return {
    openId,
    name: requireString(user?.name, `users[${openId}].name`),
    avatarUrl: requireString(user?.avatarUrl, `users[${openId}].avatarUrl`),
    gender: ['male', 'female', 'unknown'].includes(user?.gender)
      ? user.gender
      : 'unknown',
    phone: requireString(user?.phone, `users[${openId}].phone`),
    primaryRole,
    accountStatus,
  }
})
const demoUserByOpenId = new Map(demoUsers.map((user) => [user.openId, user]))

// ===== 校验：AED 设备 =====
assertUnique(
  devices.map((device) => device?.id),
  'aed-devices[].id',
)
const demoDevices = devices.map((device, index) => {
  const label = `aed-devices[${index}]`
  const status = device?.status
  if (status !== 'online' && status !== 'offline') {
    fail(`${label}.status 必须是 online|offline`)
  }
  const lat = requireFiniteNumber(device?.lat, `${label}.lat`)
  const lng = requireFiniteNumber(device?.lng, `${label}.lng`)
  return {
    id: requireString(device?.id, `${label}.id`),
    name: requireString(device?.name, `${label}.name`),
    lat,
    lng,
    status,
    statusNote:
      typeof device?.statusNote === 'string' && device.statusNote ? device.statusNote : null,
    image: requireString(device?.image, `${label}.image`),
    address: requireString(device?.address, `${label}.address`),
    lastMaintenance: requireString(device?.lastMaintenance, `${label}.lastMaintenance`),
    nextMaintenance: requireString(device?.nextMaintenance, `${label}.nextMaintenance`),
    maintainer: requireString(device?.maintainer, `${label}.maintainer`),
  }
})

// ===== 校验：急救员档案 =====
assertUnique(
  rescuerProfiles.map((profile) => profile?.id),
  'rescuer-profiles[].id',
)
assertUnique(
  rescuerProfiles.map((profile) => profile?.recordId),
  'rescuer-profiles[].recordId',
)
const demoRescuerProfiles = rescuerProfiles.map((profile, index) => {
  const label = `rescuer-profiles[${index}]`
  const campus = requireString(profile?.campus, `${label}.campus`)
  if (!campusCodes.has(campus)) {
    fail(`${label}.campus "${campus}" 不在 config/site.json 校区内`)
  }
  if (profile?.gender !== 'male' && profile?.gender !== 'female') {
    fail(`${label}.gender 必须是 male|female`)
  }
  if (!['honor', 'trainee', 'official', 'core'].includes(profile?.level)) {
    fail(`${label}.level 非法`)
  }
  if (!['active', 'inactive'].includes(profile?.qualificationStatus)) {
    fail(`${label}.qualificationStatus 必须是 active|inactive`)
  }
  const boundOpenId = profile?.boundOpenId ?? null
  if (boundOpenId !== null && !userOpenIdSet.has(boundOpenId)) {
    fail(`${label}.boundOpenId "${boundOpenId}" 在用户表中不存在`)
  }
  for (const key of ['skillCpr', 'skillAed', 'skillStb']) {
    if (typeof profile?.[key] !== 'boolean') fail(`${label}.${key} 必须是布尔值`)
  }
  return {
    id: requireString(profile?.id, `${label}.id`),
    recordId: requireString(profile?.recordId, `${label}.recordId`),
    name: requireString(profile?.name, `${label}.name`),
    gender: profile?.gender,
    birthDate: requireString(profile?.birthDate, `${label}.birthDate`),
    phone: requireString(profile?.phone, `${label}.phone`),
    idNumber: requireString(profile?.idNumber, `${label}.idNumber`),
    phoneMasked: Boolean(profile?.phoneMasked),
    idMasked: Boolean(profile?.idMasked),
    avatarUrl: requireString(profile?.avatarUrl, `${label}.avatarUrl`),
    campus,
    level: profile?.level,
    qualificationStatus: profile?.qualificationStatus,
    lat: requireFiniteNumber(profile?.lat, `${label}.lat`),
    lng: requireFiniteNumber(profile?.lng, `${label}.lng`),
    certificateNo: requireString(profile?.certificateNo, `${label}.certificateNo`),
    certificateNoMasked: requireString(
      profile?.certificateNoMasked,
      `${label}.certificateNoMasked`,
    ),
    issuingAuthority: requireString(profile?.issuingAuthority, `${label}.issuingAuthority`),
    issueDate: requireString(profile?.issueDate, `${label}.issueDate`),
    workUnit: requireString(profile?.workUnit, `${label}.workUnit`),
    skillCpr: Boolean(profile?.skillCpr),
    skillAed: Boolean(profile?.skillAed),
    skillStb: Boolean(profile?.skillStb),
    trainingYear: Number(profile?.trainingYear),
    trainingProject: requireString(profile?.trainingProject, `${label}.trainingProject`),
    trainingSessionName: requireString(
      profile?.trainingSessionName,
      `${label}.trainingSessionName`,
    ),
    theoryScore: Number(profile?.theoryScore),
    traumaAssessmentResult: requireString(
      profile?.traumaAssessmentResult,
      `${label}.traumaAssessmentResult`,
    ),
    cprAssessmentResult: requireString(
      profile?.cprAssessmentResult,
      `${label}.cprAssessmentResult`,
    ),
    aedAssessmentResult: requireString(
      profile?.aedAssessmentResult,
      `${label}.aedAssessmentResult`,
    ),
    cprAedCombinedResult: requireString(
      profile?.cprAedCombinedResult,
      `${label}.cprAedCombinedResult`,
    ),
    sourceFile: requireString(profile?.sourceFile, `${label}.sourceFile`),
    sourceSheet: requireString(profile?.sourceSheet, `${label}.sourceSheet`),
    sourceRowNo: Number(profile?.sourceRowNo),
    sourceSeqNo: requireString(profile?.sourceSeqNo, `${label}.sourceSeqNo`),
    remark: requireString(profile?.remark, `${label}.remark`),
    boundOpenId,
    source: requireString(profile?.source, `${label}.source`),
    sourceApplicationId: profile?.sourceApplicationId ?? null,
  }
})
const rescuerProfileById = new Map(
  demoRescuerProfiles.map((profile) => [profile.id, profile]),
)

// ===== 校验：演示角色 =====
const rawRoles = rolesFile.demoRoles
if (!Array.isArray(rawRoles)) fail('roles.json.demoRoles 必须是数组')
assertUnique(
  (rawRoles ?? []).map((role) => role?.loginCode),
  'demoRoles[].loginCode',
)
for (const expectedRole of EXPECTED_DEMO_ROLES) {
  if (!(rawRoles ?? []).some((role) => role?.role === expectedRole)) {
    fail(`roles.json 缺少必需演示角色 "${expectedRole}"`)
  }
}

const demoRoles = (rawRoles ?? []).map((role, index) => {
  const label = `demoRoles[${index}]`
  const openId = requireString(role?.openId, `${label}.openId`)
  if (!userOpenIdSet.has(openId)) {
    fail(`${label}.openId "${openId}" 在用户表中不存在`)
  }
  const campus = requireString(role?.campus, `${label}.campus`)
  if (!campusCodes.has(campus)) {
    fail(`${label}.campus "${campus}" 不在 config/site.json 校区内`)
  }
  const responderProfileId = role?.responderProfileId ?? null
  if (responderProfileId !== null) {
    const profile = rescuerProfileById.get(responderProfileId)
    if (!profile) {
      fail(`${label}.responderProfileId "${responderProfileId}" 在档案表中不存在`)
    } else {
      if (profile.boundOpenId !== openId) {
        fail(`${label} 的档案 ${responderProfileId} 未绑定到 openId "${openId}"`)
      }
      if (profile.qualificationStatus !== role?.qualificationStatus) {
        fail(`${label}.qualificationStatus 与档案 ${responderProfileId} 不一致`)
      }
    }
  }
  if (typeof role?.expectedCertifiedResponder !== 'boolean') {
    fail(`${label}.expectedCertifiedResponder 必须是布尔值`)
  }
  const binding = role?.schoolBinding
  if (!binding || typeof binding !== 'object') {
    fail(`${label}.schoolBinding 缺失`)
  }
  return {
    role: requireString(role?.role, `${label}.role`),
    loginCode: requireString(role?.loginCode, `${label}.loginCode`),
    openId,
    displayName: requireString(role?.displayName, `${label}.displayName`),
    campus,
    primaryRole: requireString(role?.primaryRole, `${label}.primaryRole`),
    accountStatus: requireString(role?.accountStatus, `${label}.accountStatus`),
    responderProfileId,
    qualificationStatus: role?.qualificationStatus ?? null,
    expectedCertifiedResponder: Boolean(role?.expectedCertifiedResponder),
    schoolBinding: {
      personType: requireString(binding?.personType, `${label}.schoolBinding.personType`),
      name: requireString(binding?.name, `${label}.schoolBinding.name`),
      college: requireString(binding?.college, `${label}.schoolBinding.college`),
      className: binding?.className ?? null,
      studentId: binding?.studentId ?? null,
      staffId: binding?.staffId ?? null,
      phone: requireString(binding?.phone, `${label}.schoolBinding.phone`),
      note: requireString(binding?.note, `${label}.schoolBinding.note`),
    },
  }
})

// ===== 校验：课堂数据 =====
assertUnique(
  courses.map((course) => course?.id),
  'courses[].id',
)
assertUnique(
  quickActions.map((action) => action?.id),
  'quick-actions[].id',
)
const aedCountItem = (knowledgeSummary?.items ?? []).find(
  (item) => item?.key === 'aed_count',
)
if (!aedCountItem) fail('knowledge-summary 缺少 aed_count 统计项')
if (aedCountItem && aedCountItem.value !== demoDevices.length) {
  fail(
    `knowledge-summary.aed_count=${aedCountItem.value} 与 AED 设备数 ${demoDevices.length} 不一致`,
  )
}
for (const course of courses) {
  if (course?.type === 'video' && (!course?.videoUrl || !course?.videos?.length)) {
    fail(`课程 ${course?.id} 是 video 类型但缺少视频地址`)
  }
  requireString(course?.thumbnail, `courses[${course?.id}].thumbnail`)
}

if (failures.length > 0) {
  for (const failure of failures) {
    console.error(`[sync-demo-data] FAIL ${failure}`)
  }
  console.error(`[sync-demo-data] 共 ${failures.length} 项校验失败，未写入文件`)
  process.exit(1)
}

// ===== 生成 =====
const HEADER = `// @generated by miniprogram/scripts/sync-demo-data.mjs — DO NOT EDIT.
// Source: demo/ (${dataVersion}) + config/site.json 校区校验；非手工维护的第二份档案。
// 重新生成：pnpm sync:demo（在 miniprogram/ 下执行）
`

function toJson(value) {
  return JSON.stringify(value, null, 2)
}

function writeGenerated(fileName, body) {
  const target = resolve(outputDir, fileName)
  writeFileSync(target, `${HEADER}\n${body}\n`, 'utf8')
  console.log(`[sync-demo-data] wrote ${fileName}`)
}

mkdirSync(outputDir, { recursive: true })

writeGenerated(
  'demo-metadata.generated.ts',
  `export const DEMO_DATA_VERSION = ${JSON.stringify(dataVersion)}
export const DEMO_ORGANIZATION_REFERENCE = '见 config/site.json（不复制进产物）'
`,
)

writeGenerated(
  'demo-aed-devices.generated.ts',
  `import type { DemoAedDevice } from '../demo-types'

export const DEMO_AED_DEVICES: readonly DemoAedDevice[] = ${toJson(demoDevices)}
`,
)

writeGenerated(
  'demo-rescuer-profiles.generated.ts',
  `import type { DemoRescuerProfile } from '../demo-types'

export const DEMO_RESCUER_PROFILES: readonly DemoRescuerProfile[] = ${toJson(demoRescuerProfiles)}
`,
)

writeGenerated(
  'demo-classroom.generated.ts',
  `import type {
  DemoCourse,
  DemoKnowledgeSummary,
  DemoQuickAction,
} from '../demo-types'

export const DEMO_COURSES: readonly DemoCourse[] = ${toJson(courses)}

export const DEMO_QUICK_ACTIONS: readonly DemoQuickAction[] = ${toJson(quickActions)}

export const DEMO_KNOWLEDGE_SUMMARY: DemoKnowledgeSummary = ${toJson(knowledgeSummary)}
`,
)

writeGenerated(
  'demo-roles.generated.ts',
  `import type { DemoRole, DemoUser } from '../demo-types'

export const DEMO_USERS: readonly DemoUser[] = ${toJson(demoUsers)}

export const DEMO_ROLES: readonly DemoRole[] = ${toJson(demoRoles)}
`,
)

console.log(
  `[sync-demo-data] OK ${dataVersion}: ${demoDevices.length} AED, ${demoRescuerProfiles.length} 档案, ${demoUsers.length} 用户, ${demoRoles.length} 角色, ${courses.length} 课程, ${quickActions.length} 关键步骤`,
)
