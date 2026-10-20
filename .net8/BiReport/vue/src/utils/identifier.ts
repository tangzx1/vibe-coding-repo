import { MAX_IDENTIFIER_LENGTH } from '../config/limits'
import type { BiReportColumnSchema } from '../types/biReport'

/**
 * 与后端 `BiIdentifier.SafeIdentifier` 完全一致：
 * 字母（含汉字）或下划线开头，后接字母 / 数字 / 下划线 / 汉字，总长 1~64。
 *
 * 反引号、引号、空格、点、括号都不在字符集里，所以这既是前端提示的依据，
 * 也是后端「用户输入不会闭合标识符」这条安全不变量在本地的镜像。
 */
const IDENTIFIER_PATTERN = /^[A-Za-z_一-龥][A-Za-z0-9_一-龥]*$/

/** 判断一个字符串能否作为表名 / 列名 / 输出别名 */
export function isValidIdentifier(name: string): boolean {
  return name.length > 0 && name.length <= MAX_IDENTIFIER_LENGTH && IDENTIFIER_PATTERN.test(name)
}

/** 列的类型家族。`other`（如 time / blob）不能参与 join */
export type ColumnFamily = 'numeric' | 'date' | 'string' | 'other'

export function columnFamily(column: BiReportColumnSchema): ColumnFamily {
  if (column.is_numeric) return 'numeric'
  if (column.is_date) return 'date'
  if (column.is_string) return 'string'
  return 'other'
}

/**
 * 两列能否互相关联。
 *
 * 后端 `BiIdentifier.IsJoinCompatible` 只允许数值对数值、日期对日期、字符串对字符串；
 * 三个标志全为 false 的类型（time、blob）任何情况下都不参与 join。
 * 前端在候选下拉里直接过滤掉不兼容项，比提交后被 400 打回体验好。
 */
export function sameColumnFamily(left: BiReportColumnSchema, right: BiReportColumnSchema): boolean {
  const leftFamily = columnFamily(left)
  return leftFamily !== 'other' && leftFamily === columnFamily(right)
}

/** 该列是否可作为 join 的一端 */
export function isJoinableColumn(column: BiReportColumnSchema): boolean {
  return columnFamily(column) !== 'other'
}
