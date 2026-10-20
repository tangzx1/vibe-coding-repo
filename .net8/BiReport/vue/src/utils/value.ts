import { LIMITS } from '../config/limits'
import type { BiReportColumnSchema, FilterOp, FilterState } from '../types/biReport'

/** 运算符下拉。后端大小写与多余空格不敏感，这里统一用小写形式发送 */
export const FILTER_OPERATORS: ReadonlyArray<{ value: FilterOp; label: string }> = [
  { value: '=', label: '等于 =' },
  { value: '!=', label: '不等于 !=' },
  { value: '>', label: '大于 >' },
  { value: '>=', label: '大于等于 >=' },
  { value: '<', label: '小于 <' },
  { value: '<=', label: '小于等于 <=' },
  { value: 'like', label: '包含 LIKE' },
  { value: 'not like', label: '不包含 NOT LIKE' },
  { value: 'in', label: '属于 IN' },
  { value: 'not in', label: '不属于 NOT IN' },
  { value: 'between', label: '区间 BETWEEN' },
  { value: 'is null', label: '为空 IS NULL' },
  { value: 'is not null', label: '不为空 IS NOT NULL' }
]

const STRING_ONLY_OPERATORS: ReadonlySet<FilterOp> = new Set(['like', 'not like'])
const NULL_OPERATORS: ReadonlySet<FilterOp> = new Set(['is null', 'is not null'])

/** 某个列可用的运算符。LIKE 只对字符串列开放（数值/日期列用区间或等值更准确） */
export function opsForColumn(
  column: BiReportColumnSchema | null | undefined
): ReadonlyArray<{ value: FilterOp; label: string }> {
  if (column?.is_string) return FILTER_OPERATORS
  return FILTER_OPERATORS.filter(op => !STRING_ONLY_OPERATORS.has(op.value))
}

export function isNullOperator(op: FilterOp): boolean {
  return NULL_OPERATORS.has(op)
}

/** bigint / decimal 用字符串发送，避免浏览器把 64 位整数或高精度小数转成 double 丢精度 */
const PRECISION_SENSITIVE_TYPES: ReadonlySet<string> = new Set(['bigint', 'decimal', 'numeric'])

export function isPrecisionSensitiveNumeric(dataType: string): boolean {
  return PRECISION_SENSITIVE_TYPES.has(dataType.toLowerCase())
}

/** 十进制数字文本。与后端接受的形式对齐，不接受千分位与进制前缀 */
const NUMERIC_TEXT = /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/

function parseDateText(text: string): number | null {
  // 后端接受 `yyyy-MM-dd HH:mm:ss`；替换成 ISO 的 T 后 Date.parse 的兼容性最好
  const normalized = text.trim().replace(' ', 'T')
  const time = Date.parse(normalized)
  return Number.isNaN(time) ? null : time
}

/** 单个文本值是否能按列类型转换。用于提交前提示，最终仍以后端转换为准 */
export function isValidScalarText(text: string, column: BiReportColumnSchema): boolean {
  const value = text.trim()
  if (!value) return false

  if (column.is_numeric) {
    if (!NUMERIC_TEXT.test(value)) return false
    // decimal / bigint 以字符串发送，不受 JS Number 精度限制
    return isPrecisionSensitiveNumeric(column.data_type) || Number.isFinite(Number(value))
  }

  if (column.is_date) {
    return parseDateText(value) !== null
  }

  return true
}

/** between 的下界不能大于上界 */
export function compareScalarText(left: string, right: string, column: BiReportColumnSchema): number {
  if (column.is_numeric) {
    const a = Number(left.trim())
    const b = Number(right.trim())
    if (Number.isFinite(a) && Number.isFinite(b)) return a < b ? -1 : a > b ? 1 : 0
  }

  if (column.is_date) {
    const a = parseDateText(left)
    const b = parseDateText(right)
    if (a !== null && b !== null) return a < b ? -1 : a > b ? 1 : 0
  }

  return left.trim().localeCompare(right.trim(), 'zh-CN')
}

/**
 * 把 IN / NOT IN 的 textarea 文本拆成值列表：
 * 每行一个值；数值列额外允许用逗号分隔（批量粘贴 `1,2,3` 很常见）。
 * 字符串列不对逗号拆分——值里本身可能就有逗号。
 */
export function parseListInput(text: string, column: BiReportColumnSchema): string[] {
  const lines = text.split(/\r?\n/)
  const parts = column.is_numeric ? lines.flatMap(line => line.split(',')) : lines

  const seen = new Set<string>()
  const values: string[] = []
  for (const part of parts) {
    const value = part.trim()
    if (!value || seen.has(value)) continue
    seen.add(value)
    values.push(value)
  }
  return values
}

/**
 * 按列类型把文本转成请求里的值。
 *
 * - 数值列：普通类型发 number；bigint / decimal / numeric 发字符串保住精度
 * - 日期列：发字符串，由后端按 `yyyy-MM-dd HH:mm:ss` 等格式解析
 * - 字符串列：原样发送（等于 / IN 场景下空格也可能是值的一部分）
 */
export function toScalarValue(text: string, column: BiReportColumnSchema): string | number {
  if (!column.is_numeric) return text

  const trimmed = text.trim()
  if (isPrecisionSensitiveNumeric(column.data_type)) return trimmed

  const value = Number(trimmed)
  return Number.isFinite(value) ? value : trimmed
}

/** 把一个过滤条件状态转成请求里的 `value`（`is null` 系列不调用本函数） */
export function buildFilterValue(filter: FilterState, column: BiReportColumnSchema): unknown {
  switch (filter.op) {
    case 'like':
    case 'not like':
      return filter.scalar

    case 'in':
    case 'not in':
      return parseListInput(filter.listText, column).map(value => toScalarValue(value, column))

    case 'between':
      return [toScalarValue(filter.between[0], column), toScalarValue(filter.between[1], column)]

    default:
      return toScalarValue(filter.scalar, column)
  }
}

/** 校验一个 IN 值列表，返回错误提示；合法时返回 null */
export function validateListInput(text: string, column: BiReportColumnSchema): string | null {
  const values = parseListInput(text, column)
  if (values.length === 0) return 'IN / NOT IN 至少需要 1 个值'
  if (values.length > LIMITS.maxInValues) {
    return `IN / NOT IN 最多 ${LIMITS.maxInValues} 个值，当前 ${values.length} 个`
  }

  const invalid = values.find(value => !isValidScalarText(value, column))
  if (invalid !== undefined) {
    const dataType = column.is_numeric ? '数值' : '日期'
    return `值「${invalid}」不是合法的${dataType}（列类型 ${column.data_type}）`
  }

  return null
}
