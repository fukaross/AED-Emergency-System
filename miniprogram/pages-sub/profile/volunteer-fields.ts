export type TextKey =
  | 'name'
  | 'phone'
  | 'email'
  | 'emergencyContactName'
  | 'emergencyContactPhone'
  | 'idNumber'
  | 'homeAddress'
  | 'personalResume'
  | 'joinReason'

export type SelectKey =
  | 'gender'
  | 'politicalStatus'
  | 'education'
  | 'occupation'
  | 'professionalTitle'

export type SingleImageKey = 'idPhoto' | 'idCardFront' | 'idCardBack'
export type MultiImageKey = 'qualificationImages' | 'volunteerCertificateImages'

interface OptionItem {
  readonly label: string
  readonly value: string
}

interface TextField {
  readonly key: TextKey
  readonly label: string
  readonly placeholder: string
  readonly required?: boolean
  readonly multiline?: boolean
}

interface SelectField {
  readonly key: SelectKey
  readonly label: string
  readonly placeholder: string
  readonly options: readonly OptionItem[]
}

interface ImageField<T extends string> {
  readonly key: T
  readonly label: string
  readonly hint: string
}

const GENDER_OPTIONS: readonly OptionItem[] = [
  { label: '男', value: 'male' },
  { label: '女', value: 'female' },
  { label: '保密', value: 'unknown' },
]

export const TEXT_FIELDS: readonly TextField[] = [
  { key: 'name', label: '姓名', placeholder: '请输入姓名', required: true },
  { key: 'phone', label: '联系电话', placeholder: '请输入联系电话', required: true },
  { key: 'email', label: '邮箱', placeholder: '请输入邮箱', required: true },
  { key: 'emergencyContactName', label: '紧急联系人', placeholder: '请输入紧急联系人', required: true },
  { key: 'emergencyContactPhone', label: '紧急联系人电话', placeholder: '请输入紧急联系人电话', required: true },
  { key: 'idNumber', label: '身份证号', placeholder: '请输入身份证号', required: true },
  { key: 'homeAddress', label: '家庭住址', placeholder: '请输入家庭住址', required: true },
  { key: 'personalResume', label: '个人简历', placeholder: '请输入个人简历', required: true, multiline: true },
  { key: 'joinReason', label: '入队原因', placeholder: '请输入入队原因', required: true, multiline: true },
]

export const SELECT_FIELDS: readonly SelectField[] = [
  { key: 'gender', label: '性别', placeholder: '请选择性别', options: GENDER_OPTIONS },
  { key: 'politicalStatus', label: '政治面貌', placeholder: '请选择政治面貌', options: [
    { label: '群众', value: '群众' },
    { label: '共青团员', value: '共青团员' },
    { label: '中共党员', value: '中共党员' },
    { label: '其他', value: '其他' },
  ] },
  { key: 'education', label: '学历', placeholder: '请选择学历', options: [
    { label: '高中', value: '高中' },
    { label: '大专', value: '大专' },
    { label: '本科', value: '本科' },
    { label: '硕士', value: '硕士' },
    { label: '博士', value: '博士' },
    { label: '其他', value: '其他' },
  ] },
  { key: 'occupation', label: '职业', placeholder: '请选择职业', options: [
    { label: '学生', value: '学生' },
    { label: '教师', value: '教师' },
    { label: '职工', value: '职工' },
    { label: '校医', value: '校医' },
    { label: '其他', value: '其他' },
  ] },
  { key: 'professionalTitle', label: '专业技术职称', placeholder: '请选择专业技术职称', options: [
    { label: '无', value: '无' },
    { label: '初级', value: '初级' },
    { label: '中级', value: '中级' },
    { label: '副高级', value: '副高级' },
    { label: '正高级', value: '正高级' },
    { label: '其他', value: '其他' },
  ] },
]

export const SINGLE_IMAGE_FIELDS: readonly ImageField<SingleImageKey>[] = [
  { key: 'idPhoto', label: '证件照', hint: '上传照片' },
  { key: 'idCardFront', label: '身份证照', hint: '身份证正面' },
  { key: 'idCardBack', label: '身份证照', hint: '身份证反面' },
]

export const MULTI_IMAGE_FIELDS: readonly ImageField<MultiImageKey>[] = [
  { key: 'qualificationImages', label: '资质证书', hint: '点击上传图片' },
  { key: 'volunteerCertificateImages', label: '志愿者资质证书', hint: '点击上传图片' },
]

export const COMMITMENT_TEXT =
  '我自愿申请加入校园 AED 急救志愿服务队，承诺遵守学校急救志愿服务管理要求，参加必要培训与演练；在接到校园急救协助通知时，在确保自身安全和不替代专业医疗救治的前提下，按规范提供现场协助、AED 取送、秩序维护和信息联络；尊重求助者隐私，服从校医室和现场负责人的统一安排，不传播现场照片、视频及个人信息。'
