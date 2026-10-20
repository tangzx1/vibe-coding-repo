/**
 * 前端镜像后端 `appsettings.json` 中 `BiQuery` 节的限额。
 *
 * 这些数字只用于「提交前先给出友好提示」，后端仍是唯一权威出口：
 * 两边不一致时以后端返回的错误为准，改配置后记得同步这里。
 */
export const LIMITS = {
  /** 默认每页条数 */
  defaultPageSize: 20,
  /** 每页条数上限，后端会把超过的值夹紧 */
  maxPageSize: 200,
  /** 输出字段上限 */
  maxSelectColumns: 100,
  /** 参与查询的表上限（含主表） */
  maxJoinTables: 8,
  /** 过滤条件上限 */
  maxWhereConditions: 30,
  /** IN / NOT IN 的值个数上限 */
  maxInValues: 1000,
  /** 排序字段上限 */
  maxOrderBy: 5,
  /** 导出最多行数 */
  exportMaxRows: 20000,
  /** 导出最多单元格数（行 × 列） */
  maxExportCells: 500000,
  /** LIKE 模式串长度上限 */
  maxLikeLength: 500,
  /** OFFSET 上限，再深就该加筛选条件 */
  maxOffset: 10000000
} as const

/** 标识符长度上限，与 MySQL、后端 BiIdentifier 一致 */
export const MAX_IDENTIFIER_LENGTH = 64

/** 表别名长度上限，与后端 BiReportSqlBuilder 一致 */
export const MAX_TABLE_ALIAS_LENGTH = 30

/**
 * 不能用作输出别名的名字。
 *
 * 后端只按 MySQL 标识符规则校验，这些名字本身是合法的；但结果集是
 * `Dictionary<string, object?>` 序列化成的 JSON 对象，前端在 JS 里按 key 取值时，
 * `__proto__` / `constructor` / `prototype` 会碰到对象原型链，带来意外行为。
 */
export const RESERVED_ALIASES: ReadonlySet<string> = new Set([
  '__proto__',
  'constructor',
  'prototype'
])

/** 每页条数下拉候选 */
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100, 200] as const
