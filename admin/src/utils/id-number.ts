const ID_NUMBER_WEIGHTS = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2]
const ID_NUMBER_CHECK_MAP = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2']
const ID_NUMBER_PATTERN = /^\d{17}[\dXx]$/

export function validateIdNumber(idNumber: string | null | undefined) {
  const value = idNumber?.trim()
  if (!value) return '身份证号不能为空'

  if (!ID_NUMBER_PATTERN.test(value)) {
    return '身份证号格式不正确（需18位，末位可为数字或X）'
  }

  const sum = ID_NUMBER_WEIGHTS.reduce((total, weight, index) => {
    return total + Number.parseInt(value[index], 10) * weight
  }, 0)

  return value[17].toUpperCase() === ID_NUMBER_CHECK_MAP[sum % 11]
    ? ''
    : '身份证号校验位不正确'
}
