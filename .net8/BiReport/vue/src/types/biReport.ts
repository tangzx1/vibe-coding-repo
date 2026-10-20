/**
 * 与后端 `VibeCodingApi.Model.Dtos` 逐字段对齐的类型定义。
 *
 * 命名说明：后端的 DTO 属性名本身就是 snake_case（`page_index`、`table_name`…），
 * System.Text.Json 的 camelCase 策略只改首字母大写的名字，对已经以 `_` 分隔的名字原样输出，
 * 所以请求和响应都是 snake_case，不能按 camelCase 写。
 */

/** 统一返回结构。顶层字段顺序是 data / code / message（继承类型先序列化），解析时与顺序无关。 */
export interface ApiResult<T> {
  code: number
  message: string
  data: T | null
}

// ---------------------------------------------------------------------------
// 元数据 GET /api/bi-report/metadata
// ---------------------------------------------------------------------------

export interface BiReportSchema {
  generated_at: string
  tables: BiReportTableSchema[]
}

export interface BiReportTableSchema {
  table_name: string
  display_name: string
  table_comment: string | null
  columns: BiReportColumnSchema[]
}

export interface BiReportColumnSchema {
  column_name: string
  display_name: string
  data_type: string
  is_nullable: boolean
  column_comment: string | null
  /** 是否数值类型；sum / avg 只对数值列开放 */
  is_numeric: boolean
  /** 是否日期时间类型 */
  is_date: boolean
  /** 是否字符串类型 */
  is_string: boolean
}

// ---------------------------------------------------------------------------
// 查询请求 POST /api/bi-report/query
// ---------------------------------------------------------------------------

export type JoinType = 'inner' | 'left'

export type Aggregate = 'count' | 'count_distinct' | 'sum' | 'avg' | 'max' | 'min'

export type FilterOp =
  | '='
  | '!='
  | '>'
  | '>='
  | '<'
  | '<='
  | 'like'
  | 'not like'
  | 'in'
  | 'not in'
  | 'between'
  | 'is null'
  | 'is not null'

export interface BiReportTableRefDto {
  /** 表名，必须是元数据里出现过的表 */
  table: string
  /** 表别名；本项目固定不传，由后端默认成表名 */
  alias?: string
}

export interface BiReportJoinDto {
  join_type: JoinType
  left_table: string
  left_column: string
  right_table: string
  right_column: string
}

export interface BiReportSelectDto {
  /** `表名.列名`，本项目一律写全，避免多表同名列歧义 */
  field: string
  aggregate?: Aggregate
  alias?: string
}

export interface BiReportFilterDto {
  /** `表名.列名` */
  field: string
  op: FilterOp
  /** `is null` / `is not null` 不传；`in` / `between` 传数组 */
  value?: unknown
}

export interface BiReportOrderByDto {
  /** 普通字段用 `表名.列名`；聚合字段用输出别名 */
  field: string
  desc: boolean
}

export interface BiReportQueryDto {
  tables: BiReportTableRefDto[]
  joins: BiReportJoinDto[]
  select: BiReportSelectDto[]
  where?: BiReportFilterDto[]
  group_by?: string[]
  order_by?: BiReportOrderByDto[]
  page_index: number
  page_size: number
  need_total: boolean
}

// ---------------------------------------------------------------------------
// 查询响应
// ---------------------------------------------------------------------------

export interface BiReportResult {
  /** 输出列名，顺序与 select 一致 */
  columns: string[]
  /** 每行的 key 就是 columns 里的名字 */
  items: Record<string, unknown>[]
  /** null 表示「未统计」（need_total=false），0 表示确实没有数据 */
  total: number | null
  page_index: number
  /** 后端夹紧后的实际每页条数 */
  page_size: number
  /** 未统计总数时为 0 */
  total_pages: number
}

// ---------------------------------------------------------------------------
// 前端内部状态（不是后端 DTO）
// ---------------------------------------------------------------------------

export type SortOrder = '' | 'asc' | 'desc'

export type GroupMode = 'auto' | 'manual'

/** 参与查询的一张表。本项目表别名固定等于表名，不支持自关联 */
export interface TableState {
  name: string
}

/** 一个输出字段。同一列只能选一次，key 用 `表名.列名` 唯一标识 */
export interface OutputFieldState {
  key: string
  table: string
  column: string
  aggregate: Aggregate | null
  alias: string
  /** 用户手动改过别名后，切换聚合函数不再自动重算别名 */
  aliasTouched: boolean
  sort: SortOrder
}

/** 一个关联条件 */
export interface JoinState {
  id: string
  joinType: JoinType
  leftTable: string
  leftColumn: string
  rightTable: string
  rightColumn: string
}

/** 一个过滤条件。值按运算符分散保存，避免在输入过程中反复解析 */
export interface FilterState {
  id: string
  table: string
  column: string
  op: FilterOp
  /** 标量运算符和 like 用 */
  scalar: string
  /** between 用，恰好两个元素 */
  between: [string, string]
  /** in / not in 的原始文本，每行一个值 */
  listText: string
}
