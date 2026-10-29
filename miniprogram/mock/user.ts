// 用户和急救员档案的 Mock 数据入口 —— 当前身份来自 demo 演示角色（可切换）。
import type {
  ArchiveDetail,
  ArchiveListParams,
  RescuerArchiveListResponse,
  RescuerSummary,
  UpdateUserProfilePayload,
  UserProfile,
} from '@/types/user'
import type {
  VolunteerApplicationPayload,
  VolunteerApplicationResponse,
} from '@/types/volunteer'
import {
  buildActiveArchiveListItems,
  buildArchiveDetail,
  buildArchiveListItemById,
  buildRescuerSummary,
  cloneSchoolProfile,
  getArchiveProfileRecord,
  resolveCurrentArchiveProfileId,
} from './archive-fixtures'
import {
  DEMO_AVATAR_FALLBACK,
  resolveCurrentRescuerProfile,
  resolveDemoSchoolBinding,
  toMockArchiveId,
} from './demo-fixtures'
import { resolveCurrentDemoUser } from './demo-role'
export {
  queryMockResponderClaims,
  queryMockSchoolBindings,
  saveMockResponderClaim,
  saveMockSchoolBinding,
} from './user-identity'

const MOCK_USER_STORAGE_KEY = 'aed_mock_user_profile'
const MOCK_VOLUNTEER_APPLICATION_STORAGE_KEY =
  'aed_mock_volunteer_application'
const MOCK_CAN_VIEW_FULL_PHONE = false
const LEGACY_DEFAULT_AVATAR = '/static/images/avatar-default.png'

type EditableUserProfilePatch = Partial<
  Pick<UserProfile, 'name' | 'avatarUrl' | 'gender' | 'phone'>
>

function normalizeStoredAvatarUrl(avatarUrl: unknown): string | undefined {
  if (typeof avatarUrl !== 'string') return undefined
  const normalized = avatarUrl.trim()
  if (!normalized || normalized === LEGACY_DEFAULT_AVATAR) return undefined
  return normalized
}

function withDefinedFields<T extends object>(source: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(source).filter((entry) => entry[1] !== undefined),
  ) as Partial<T>
}

function cloneRescuerSummary(
  summary: RescuerSummary | null | undefined,
): RescuerSummary | null {
  return summary ? { ...summary } : null
}

function buildBaseUserProfile(): UserProfile {
  const user = resolveCurrentDemoUser()
  const rescuerProfile = resolveCurrentRescuerProfile()
  const archiveId = rescuerProfile ? toMockArchiveId(rescuerProfile.id) : null
  const rescuerSummary = archiveId ? buildRescuerSummary(archiveId) : null
  const binding = resolveDemoSchoolBinding(user.openId)

  return {
    id: user.openId,
    name: user.name,
    avatarUrl: user.avatarUrl || DEMO_AVATAR_FALLBACK,
    gender: user.gender,
    phone: user.phone,
    primaryRole: user.primaryRole,
    certified: rescuerSummary?.certified ?? false,
    level: rescuerSummary?.level ?? null,
    schoolProfile: binding
      ? {
          personType: binding.personType,
          name: binding.name,
          college: binding.college,
          className: binding.className,
          studentId: binding.studentId ?? undefined,
          staffId: binding.staffId ?? undefined,
          phone: user.phone,
          bindingStatus: 'active',
        }
      : null,
    rescuerSummary,
  }
}

function cloneUserProfile(profile: UserProfile): UserProfile {
  return {
    ...profile,
    schoolProfile: cloneSchoolProfile(profile.schoolProfile),
    rescuerSummary: cloneRescuerSummary(profile.rescuerSummary),
  }
}

function readStoredUserPatch(): EditableUserProfilePatch {
  try {
    const stored = uni.getStorageSync(MOCK_USER_STORAGE_KEY) as string
    const parsed = stored ? (JSON.parse(stored) as Partial<UserProfile>) : {}
    return withDefinedFields({
      name: parsed.name,
      avatarUrl: normalizeStoredAvatarUrl(parsed.avatarUrl),
      gender: parsed.gender,
      phone: parsed.phone,
    })
  } catch {
    return {}
  }
}

function pickEditableUserPatch(
  patch: UpdateUserProfilePayload,
): EditableUserProfilePatch {
  return withDefinedFields({
    name: patch.name,
    avatarUrl: normalizeStoredAvatarUrl(patch.avatarUrl),
    gender: patch.gender,
    phone: patch.phone,
  })
}

export function canMockViewFullPhone(): boolean {
  return MOCK_CAN_VIEW_FULL_PHONE
}

export function loadMockUserProfile(): UserProfile {
  return cloneUserProfile({ ...buildBaseUserProfile(), ...readStoredUserPatch() })
}

export function saveMockUserProfile(
  patch: UpdateUserProfilePayload,
): UserProfile {
  try {
    uni.setStorageSync(
      MOCK_USER_STORAGE_KEY,
      JSON.stringify({ ...readStoredUserPatch(), ...pickEditableUserPatch(patch) }),
    )
  } catch {
    console.warn('[Mock User API] 保存用户资料失败')
  }

  return loadMockUserProfile()
}

export function queryMockArchives(
  params?: ArchiveListParams,
): RescuerArchiveListResponse {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const keyword = params?.keyword?.trim()
  const currentArchiveId = resolveCurrentArchiveProfileId()
  const selfArchive = currentArchiveId
    ? buildArchiveListItemById(currentArchiveId)
    : null
  let items = buildActiveArchiveListItems()

  if (params?.level) items = items.filter((item) => item.level === params.level)
  if (keyword) items = items.filter((item) => item.name.includes(keyword))

  return {
    total: items.length,
    page,
    pageSize,
    totalPages: Math.max(1, Math.ceil(items.length / pageSize)),
    canViewFullPhone: canMockViewFullPhone(),
    selfArchive,
    items: items.slice((page - 1) * pageSize, page * pageSize),
  }
}

export function getMockArchiveDetail(archiveId: string): ArchiveDetail | null {
  return buildArchiveDetail(archiveId)
}

export function getMockArchivePhone(archiveId: string): string | null {
  return getArchiveProfileRecord(archiveId)?.phoneRaw ?? null
}

export function saveMockVolunteerApplication(
  payload: VolunteerApplicationPayload,
): VolunteerApplicationResponse {
  const response = {
    applicationId: `volunteer-${Date.now()}`,
    status: 'submitted',
    submittedAt: new Date().toISOString(),
  } as const

  try {
    uni.setStorageSync(
      MOCK_VOLUNTEER_APPLICATION_STORAGE_KEY,
      JSON.stringify({ payload, response }),
    )
  } catch {
    console.warn('[Mock User API] 保存志愿者申请失败')
  }

  return response
}
