import { computed, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { exportXlsx, getMetadata, refreshMetadata, runQuery } from '../api/biReport'
import { extractMessage } from '../api/http'
import { LIMITS, MAX_IDENTIFIER_LENGTH, RESERVED_ALIASES } from '../config/limits'
import type {
  Aggregate,
  BiReportColumnSchema,
  BiReportFilterDto,
  BiReportOrderByDto,
  BiReportQueryDto,
  BiReportResult,
  BiReportSchema,
  BiReportSelectDto,
  BiReportTableSchema,
  FilterState,
  GroupMode,
  JoinState,
  OutputFieldState,
  SortOrder,
  TableState
} from '../types/biReport'
import { isValidIdentifier, sameColumnFamily } from '../utils/identifier'
import {
  buildFilterValue,
  compareScalarText,
  isNullOperator,
  isValidScalarText,
  opsForColumn,
  validateListInput
} from '../utils/value'
import { escapeHtml } from '../utils/format'
import { saveBlob } from '../utils/download'

let idSequence = 0
function nextId(prefix: string): string {
  idSequence += 1
  return `${prefix}_${idSequence}`
}

/** 当前编辑状态。模板通过解构使用这些 ref，Vue 会在模板里自动解包 */
export function useQueryBuilder() {
  // -------------------------------------------------------------------------
  // 元数据
  // -------------------------------------------------------------------------
  const schema = ref<BiReportSchema | null>(null)
  const metadataLoading = ref(false)
  const metadataError = ref('')

  const schemaTables = computed<Map<string, BiReportTableSchema>>(() => {
    const map = new Map<string, BiReportTableSchema>()
    for (const table of schema.value?.tables ?? []) {
      map.set(table.table_name, table)
    }
    return map
  })

  const schemaColumns = computed<Map<string, BiReportColumnSchema>>(() => {
    const map = new Map<string, BiReportColumnSchema>()
    for (const table of schema.value?.tables ?? []) {
      for (const column of table.columns) {
        map.set(`${table.table_name}.${column.column_name}`, column)
      }
    }
    return map
  })

  function getSchemaTable(name: string): BiReportTableSchema | undefined {
    return schemaTables.value.get(name)
  }

  function getColumnMeta(table: string, column: string): BiReportColumnSchema | undefined {
    return schemaColumns.value.get(`${table}.${column}`)
  }

  function tableLabel(name: string): string {
    return getSchemaTable(name)?.display_name ?? name
  }

  function fieldLabel(field: OutputFieldState): string {
    const table = getSchemaTable(field.table)
    const column = getColumnMeta(field.table, field.column)
    return `${table?.display_name ?? field.table}.${column?.display_name ?? field.column}`
  }

  // -------------------------------------------------------------------------
  // 查询定义状态
  // -------------------------------------------------------------------------
  const tables = ref<TableState[]>([])
  const fields = ref<OutputFieldState[]>([])
  const joins = ref<JoinState[]>([])
  const filters = ref<FilterState[]>([])

  const groupMode = ref<GroupMode>('auto')
  const manualGroupFields = ref<string[]>([])

  const pageIndex = ref(1)
  const pageSize = ref<number>(LIMITS.defaultPageSize)
  const needTotal = ref(true)

  // -------------------------------------------------------------------------
  // 执行状态
  // -------------------------------------------------------------------------
  const result = ref<BiReportResult | null>(null)
  const loading = ref(false)
  const exporting = ref(false)
  const errorMessage = ref('')

  /** 上次成功执行时的条件签名（不含分页），用于导出前判断条件是否被改过 */
  const executedSignature = ref<string | null>(null)

  /** 查询序号：连续点击只接受最后一次响应，避免慢请求把新结果覆盖掉 */
  let querySequence = 0

  // -------------------------------------------------------------------------
  // 派生状态
  // -------------------------------------------------------------------------
  const tableNames = computed(() => tables.value.map(table => table.name))
  const mainTable = computed(() => tables.value[0]?.name ?? '')
  const selectedFieldKeys = computed(() => new Set(fields.value.map(field => field.key)))
  const hasAggregate = computed(() => fields.value.some(field => field.aggregate !== null))
  const nonAggregateFields = computed(() => fields.value.filter(field => field.aggregate === null))

  /**
   * 实际发送给后端的 group_by。
   *
   * 自动模式 = 所有非聚合字段（「按这几列汇总」的直觉行为），没有聚合字段时不分组；
   * 手动模式 = 用户勾选的非聚合字段。两种模式都物化成显式数组，保证「预览 = 实际」。
   */
  const effectiveGroupBy = computed<string[]>(() => {
    if (groupMode.value === 'auto') {
      if (!hasAggregate.value) return []
      return nonAggregateFields.value.map(field => `${field.table}.${field.column}`)
    }

    const selected = new Set(manualGroupFields.value)
    return nonAggregateFields.value
      .filter(field => selected.has(field.key))
      .map(field => `${field.table}.${field.column}`)
  })

  const groupFieldOptions = computed(() =>
    nonAggregateFields.value.map(field => ({ key: field.key, label: fieldLabel(field) }))
  )

  // -------------------------------------------------------------------------
  // 元数据加载与对账
  // -------------------------------------------------------------------------
  async function loadMetadata(force = false): Promise<void> {
    metadataLoading.value = true
    metadataError.value = ''

    try {
      const loaded = force ? await refreshMetadata() : await getMetadata()
      schema.value = loaded
      reconcileSelection()
      if (force) {
        ElMessage.success(`元数据已刷新：${loaded.tables.length} 张表`)
      }
    } catch (error) {
      metadataError.value = extractMessage(error)
      ElMessage.error(metadataError.value)
    } finally {
      metadataLoading.value = false
    }
  }

  /**
   * 元数据刷新后清理已经失效的选择（DDL 改过表结构、或配置里移除了白名单表）。
   * 清完再补草稿关联，保证「表数」和「关联数」仍然对得上。
   */
  function reconcileSelection(): void {
    const current = schema.value
    if (!current) return

    const tableMap = new Map(current.tables.map(table => [table.table_name, table]))
    const columnSet = new Set<string>()
    for (const table of current.tables) {
      for (const column of table.columns) {
        columnSet.add(`${table.table_name}.${column.column_name}`)
      }
    }

    const beforeTables = tables.value.length
    tables.value = tables.value.filter(table => tableMap.has(table.name))
    const removedTables = beforeTables - tables.value.length

    const beforeFields = fields.value.length
    fields.value = fields.value.filter(field => columnSet.has(field.key))
    const removedFields = beforeFields - fields.value.length

    const beforeJoins = joins.value.length
    joins.value = joins.value.filter(join =>
      tableMap.has(join.leftTable) &&
      tableMap.has(join.rightTable) &&
      (!join.leftColumn || columnSet.has(`${join.leftTable}.${join.leftColumn}`)) &&
      (!join.rightColumn || columnSet.has(`${join.rightTable}.${join.rightColumn}`))
    )
    const removedJoins = beforeJoins - joins.value.length

    const beforeFilters = filters.value.length
    filters.value = filters.value.filter(filter =>
      !filter.table ||
      (tableMap.has(filter.table) &&
        (!filter.column || columnSet.has(`${filter.table}.${filter.column}`)))
    )
    const removedFilters = beforeFilters - filters.value.length

    const remainingFieldKeys = new Set(fields.value.map(field => field.key))
    manualGroupFields.value = manualGroupFields.value.filter(key => remainingFieldKeys.has(key))

    syncJoins()

    const removedParts: string[] = []
    if (removedTables > 0) removedParts.push(`${removedTables} 张表`)
    if (removedFields > 0) removedParts.push(`${removedFields} 个输出字段`)
    if (removedJoins > 0) removedParts.push(`${removedJoins} 个关联条件`)
    if (removedFilters > 0) removedParts.push(`${removedFilters} 个过滤条件`)
    if (removedParts.length > 0) {
      ElMessage.warning(`元数据已更新，已清理失效的选择：${removedParts.join('、')}`)
    }
  }

  // -------------------------------------------------------------------------
  // 表 / 字段操作
  // -------------------------------------------------------------------------

  /** 计算从主表出发、沿 joins 能走到的表（关联列没选完也算一条边，草稿也算） */
  function connectedTables(names: string[], edges: JoinState[]): Set<string> {
    const connected = new Set<string>()
    if (names.length === 0) return connected

    connected.add(names[0])
    let changed = true
    while (changed) {
      changed = false
      for (const edge of edges) {
        if (!edge.leftTable || !edge.rightTable) continue
        if (connected.has(edge.leftTable) && !connected.has(edge.rightTable)) {
          connected.add(edge.rightTable)
          changed = true
        }
        if (connected.has(edge.rightTable) && !connected.has(edge.leftTable)) {
          connected.add(edge.leftTable)
          changed = true
        }
      }
    }
    return connected
  }

  function createDraftJoin(target: string): JoinState {
    return {
      id: nextId('join'),
      joinType: 'left',
      leftTable: mainTable.value,
      leftColumn: '',
      rightTable: target,
      rightColumn: ''
    }
  }

  /**
   * 让 joins 数量始终等于 tables.length - 1。
   *
   * 新增表时补一条「左表 = 主表、左右列留空」的草稿，用户直接选列即可，
   * 而不是先看到「关联条件不足」再自己加一条。
   */
  function syncJoins(): void {
    const names = tableNames.value
    const nameSet = new Set(names)
    joins.value = joins.value.filter(join =>
      nameSet.has(join.leftTable) && nameSet.has(join.rightTable))

    const expected = Math.max(0, names.length - 1)
    while (joins.value.length > expected) {
      joins.value.pop()
    }

    while (joins.value.length < expected) {
      const connected = connectedTables(names, joins.value)
      const orphan = names.find(name => !connected.has(name))
      if (!orphan) break
      joins.value.push(createDraftJoin(orphan))
    }
  }

  function ensureTable(name: string): void {
    if (tables.value.some(table => table.name === name)) return
    tables.value.push({ name })
    syncJoins()
  }

  /** 显式加入一张表（桥接表可以只参与关联、不输出字段） */
  function addTable(name: string): void {
    if (!getSchemaTable(name)) return
    ensureTable(name)
  }

  function defaultAlias(column: string, aggregate: Aggregate | null): string {
    return aggregate ? `${aggregate}_${column}` : column
  }

  /** 生成不与现有别名冲突的默认别名；用户之后仍可手改 */
  function nextAlias(base: string, ignoreKey?: string): string {
    const used = new Set(
      fields.value
        .filter(field => field.key !== ignoreKey)
        .map(field => field.alias.trim().toLowerCase())
    )
    if (!used.has(base.toLowerCase())) return base

    let suffix = 2
    while (used.has(`${base}_${suffix}`.toLowerCase())) {
      suffix += 1
    }
    return `${base}_${suffix}`
  }

  function addField(table: string, column: string): void {
    const key = `${table}.${column}`
    if (fields.value.some(field => field.key === key)) return
    if (!getColumnMeta(table, column)) return

    // 勾选字段时自动把表加入查询；表已存在时 ensureTable 是幂等的
    ensureTable(table)
    fields.value.push({
      key,
      table,
      column,
      aggregate: null,
      alias: nextAlias(defaultAlias(column, null)),
      aliasTouched: false,
      sort: ''
    })
  }

  /**
   * 取消勾选字段只删字段，不删表：表可能还被关联、过滤或手工「加入查询」引用。
   * 移除表由 removeTable 单独处理（有级联清理和二次确认）。
   */
  function removeField(key: string): void {
    fields.value = fields.value.filter(field => field.key !== key)
    manualGroupFields.value = manualGroupFields.value.filter(item => item !== key)
  }

  function toggleField(table: string, column: string): void {
    const key = `${table}.${column}`
    if (fields.value.some(field => field.key === key)) {
      removeField(key)
    } else {
      addField(table, column)
    }
  }

  async function removeTable(name: string): Promise<void> {
    if (!tables.value.some(table => table.name === name)) return

    const fieldCount = fields.value.filter(field => field.table === name).length
    const joinCount = joins.value.filter(join =>
      join.leftTable === name || join.rightTable === name).length
    const filterCount = filters.value.filter(filter => filter.table === name).length

    if (fieldCount + joinCount + filterCount > 0) {
      try {
        await ElMessageBox.confirm(
          `表「${tableLabel(name)}」上还有 ${fieldCount} 个输出字段、` +
          `${joinCount} 个关联条件、${filterCount} 个过滤条件，移除后会一并清除。是否继续？`,
          '移除表',
          { type: 'warning', confirmButtonText: '移除', cancelButtonText: '取消' }
        )
      } catch {
        return
      }
    }

    fields.value = fields.value.filter(field => field.table !== name)
    const remainingFieldKeys = new Set(fields.value.map(field => field.key))
    manualGroupFields.value = manualGroupFields.value.filter(key => remainingFieldKeys.has(key))
    joins.value = joins.value.filter(join =>
      join.leftTable !== name && join.rightTable !== name)
    filters.value = filters.value.filter(filter => filter.table !== name)
    tables.value = tables.value.filter(table => table.name !== name)
    syncJoins()
  }

  function setAggregate(key: string, aggregate: Aggregate | null): void {
    const field = fields.value.find(item => item.key === key)
    if (!field) return

    field.aggregate = aggregate
    if (aggregate) {
      // 聚合字段不能再作为分组字段
      manualGroupFields.value = manualGroupFields.value.filter(item => item !== key)
    }
    if (!field.aliasTouched) {
      field.alias = nextAlias(defaultAlias(field.column, aggregate), key)
    }
  }

  function setAlias(key: string, alias: string): void {
    const field = fields.value.find(item => item.key === key)
    if (!field) return
    field.alias = alias
    field.aliasTouched = true
  }

  function setSort(key: string, sort: SortOrder): void {
    const field = fields.value.find(item => item.key === key)
    if (!field) return
    field.sort = sort
  }

  // -------------------------------------------------------------------------
  // 关联 / 过滤 / 分组操作
  // -------------------------------------------------------------------------
  function updateJoin(id: string, patch: Partial<JoinState>): void {
    const join = joins.value.find(item => item.id === id)
    if (!join) return
    Object.assign(join, patch)
  }

  function addFilter(): void {
    if (tables.value.length === 0) {
      ElMessage.warning('请先选择至少一张表')
      return
    }

    filters.value.push({
      id: nextId('filter'),
      table: tables.value[0].name,
      column: '',
      op: '=',
      scalar: '',
      between: ['', ''],
      listText: ''
    })
  }

  function updateFilter(id: string, patch: Partial<FilterState>): void {
    const filter = filters.value.find(item => item.id === id)
    if (!filter) return
    Object.assign(filter, patch)
  }

  function removeFilter(id: string): void {
    filters.value = filters.value.filter(filter => filter.id !== id)
  }

  function setGroupMode(mode: GroupMode): void {
    if (mode === groupMode.value) return
    if (mode === 'manual') {
      // 从「自动分组」切换过来时，先用当前自动结果初始化勾选项
      manualGroupFields.value = nonAggregateFields.value.map(field => field.key)
    }
    groupMode.value = mode
  }

  function setManualGroups(keys: string[]): void {
    const allowed = new Set(nonAggregateFields.value.map(field => field.key))
    manualGroupFields.value = keys.filter(key => allowed.has(key))
  }

  // -------------------------------------------------------------------------
  // 请求构建
  // -------------------------------------------------------------------------
  function buildWhere(): BiReportFilterDto[] | null {
    const list: BiReportFilterDto[] = []

    for (const filter of filters.value) {
      // 不完整的行由 validateQuery 负责提示；这里跳过，避免把半成品发出去
      if (!filter.table || !filter.column) continue
      const column = getColumnMeta(filter.table, filter.column)
      if (!column) continue

      const item: BiReportFilterDto = {
        field: `${filter.table}.${filter.column}`,
        op: filter.op
      }
      if (!isNullOperator(filter.op)) {
        item.value = buildFilterValue(filter, column)
      }
      list.push(item)
    }

    return list.length > 0 ? list : null
  }

  function buildOrderBy(): BiReportOrderByDto[] {
    const list: BiReportOrderByDto[] = []

    for (const field of fields.value) {
      if (field.sort === '') continue
      // 聚合列按别名排序；普通列发完整字段引用，避免和别名规则混淆
      const reference = field.aggregate
        ? field.alias.trim() || `${field.table}.${field.column}`
        : `${field.table}.${field.column}`
      list.push({ field: reference, desc: field.sort === 'desc' })
    }

    return list
  }

  function buildRequest(): BiReportQueryDto {
    const request: BiReportQueryDto = {
      tables: tables.value.map(table => ({ table: table.name })),
      joins: joins.value.map(join => ({
        join_type: join.joinType,
        left_table: join.leftTable,
        left_column: join.leftColumn,
        right_table: join.rightTable,
        right_column: join.rightColumn
      })),
      select: fields.value.map(field => {
        const item: BiReportSelectDto = { field: `${field.table}.${field.column}` }
        if (field.aggregate) item.aggregate = field.aggregate
        const alias = field.alias.trim()
        if (alias) item.alias = alias
        return item
      }),
      page_index: pageIndex.value,
      page_size: pageSize.value,
      need_total: needTotal.value
    }

    const where = buildWhere()
    if (where) request.where = where

    const groupBy = effectiveGroupBy.value
    if (groupBy.length > 0) request.group_by = groupBy

    const orderBy = buildOrderBy()
    if (orderBy.length > 0) request.order_by = orderBy

    return request
  }

  /** 条件签名：导出不分页，所以页码 / 每页条数 / 是否统计总数不参与比较 */
  function signatureOf(request: BiReportQueryDto): string {
    return JSON.stringify({
      tables: request.tables,
      joins: request.joins,
      select: request.select,
      where: request.where ?? null,
      group_by: request.group_by ?? null,
      order_by: request.order_by ?? null
    })
  }

  const previewJson = computed(() => JSON.stringify(buildRequest(), null, 2))

  const isDirtyForExport = computed(() =>
    executedSignature.value !== null &&
    signatureOf(buildRequest()) !== executedSignature.value
  )

  // -------------------------------------------------------------------------
  // 提交前校验（后端仍是权威出口，这里只是把常见错误提前说清楚）
  // -------------------------------------------------------------------------
  function validateQuery(): string[] {
    const problems: string[] = []

    if (!schema.value) {
      return ['元数据尚未加载，请先刷新元数据']
    }

    if (tables.value.length === 0) problems.push('请至少选择一张表')
    if (fields.value.length === 0) problems.push('请至少选择一个输出字段')
    if (tables.value.length > LIMITS.maxJoinTables) {
      problems.push(`参与查询的表最多 ${LIMITS.maxJoinTables} 张`)
    }
    if (fields.value.length > LIMITS.maxSelectColumns) {
      problems.push(`输出字段最多 ${LIMITS.maxSelectColumns} 个`)
    }
    if (filters.value.length > LIMITS.maxWhereConditions) {
      problems.push(`过滤条件最多 ${LIMITS.maxWhereConditions} 个`)
    }

    const orderCount = fields.value.filter(field => field.sort !== '').length
    if (orderCount > LIMITS.maxOrderBy) {
      problems.push(`排序字段最多 ${LIMITS.maxOrderBy} 个，当前 ${orderCount} 个`)
    }

    const tableSet = new Set(tables.value.map(table => table.name))
    const aliasOwners = new Map<string, string>()

    for (const field of fields.value) {
      if (!tableSet.has(field.table)) {
        problems.push(`输出字段 ${field.key} 所属的表不在查询中`)
        continue
      }

      const column = getColumnMeta(field.table, field.column)
      if (!column) {
        problems.push(`输出字段 ${field.key} 不在当前元数据中，请刷新元数据`)
        continue
      }

      if ((field.aggregate === 'sum' || field.aggregate === 'avg') && !column.is_numeric) {
        problems.push(
          `字段 ${field.key} 是 ${column.data_type} 类型，不能使用 ${field.aggregate} 聚合`
        )
      }

      const alias = field.alias.trim()
      if (!alias) {
        problems.push(`字段 ${field.key} 的输出别名不能为空`)
      } else if (!isValidIdentifier(alias)) {
        problems.push(
          `别名 ${alias} 不合法：只允许字母、数字、下划线和汉字，不能以数字开头，` +
          `最长 ${MAX_IDENTIFIER_LENGTH} 个字符`
        )
      } else if (RESERVED_ALIASES.has(alias.toLowerCase())) {
        problems.push(`别名 ${alias} 与对象内置属性重名，请换一个`)
      } else {
        const lower = alias.toLowerCase()
        if (aliasOwners.has(lower)) {
          problems.push(`别名 ${alias} 重复，请为每个输出字段指定不同的别名`)
        } else {
          aliasOwners.set(lower, field.key)
        }
      }
    }

    // 分组完整性：有聚合列时，所有非聚合输出列必须在 group_by 里
    if (hasAggregate.value || effectiveGroupBy.value.length > 0) {
      const groupSet = new Set(effectiveGroupBy.value.map(reference => reference.toLowerCase()))
      for (const field of fields.value) {
        if (field.aggregate) continue
        if (!groupSet.has(`${field.table}.${field.column}`.toLowerCase())) {
          problems.push(
            `字段 ${field.alias || field.key} 既没有聚合也没有出现在分组字段中，` +
            '请把它加入分组或改用聚合函数'
          )
        }
      }
    }

    // 关联条件
    const expectedJoins = Math.max(0, tables.value.length - 1)
    if (tables.value.length === 1 && joins.value.length > 0) {
      problems.push('只查询一张表时不需要关联条件')
    } else if (tables.value.length > 1 && joins.value.length !== expectedJoins) {
      problems.push(
        `关联条件数量不正确：${tables.value.length} 张表需要 ${expectedJoins} 个，` +
        `当前 ${joins.value.length} 个`
      )
    }

    for (const [index, join] of joins.value.entries()) {
      const position = index + 1

      if (!tableSet.has(join.leftTable) || !tableSet.has(join.rightTable)) {
        problems.push(`第 ${position} 个关联条件引用了未加入查询的表`)
        continue
      }
      if (join.leftTable === join.rightTable) {
        problems.push(`第 ${position} 个关联条件的左右表不能相同`)
      }

      const left = join.leftColumn
        ? getColumnMeta(join.leftTable, join.leftColumn)
        : undefined
      const right = join.rightColumn
        ? getColumnMeta(join.rightTable, join.rightColumn)
        : undefined

      if (!join.leftColumn) {
        problems.push(`第 ${position} 个关联条件尚未选择左字段`)
      } else if (!left) {
        problems.push(
          `第 ${position} 个关联条件：表 ${join.leftTable} 中没有字段 ${join.leftColumn}`
        )
      }

      if (!join.rightColumn) {
        problems.push(`第 ${position} 个关联条件尚未选择右字段`)
      } else if (!right) {
        problems.push(
          `第 ${position} 个关联条件：表 ${join.rightTable} 中没有字段 ${join.rightColumn}`
        )
      }

      if (left && right && !sameColumnFamily(left, right)) {
        problems.push(
          `第 ${position} 个关联条件的字段类型不匹配：` +
          `${join.leftTable}.${join.leftColumn}（${left.data_type}）与 ` +
          `${join.rightTable}.${join.rightColumn}（${right.data_type}）`
        )
      }
    }

    // 连通性：n 张表、n-1 条边，从主表 BFS 必须能到达所有表
    if (tables.value.length > 1 && joins.value.length === expectedJoins) {
      const adjacency = new Map<string, Set<string>>()
      for (const table of tables.value) {
        adjacency.set(table.name, new Set())
      }
      for (const join of joins.value) {
        adjacency.get(join.leftTable)?.add(join.rightTable)
        adjacency.get(join.rightTable)?.add(join.leftTable)
      }

      const main = tables.value[0].name
      const visited = new Set<string>([main])
      const queue: string[] = [main]
      while (queue.length > 0) {
        const current = queue.shift() as string
        for (const next of adjacency.get(current) ?? []) {
          if (!visited.has(next)) {
            visited.add(next)
            queue.push(next)
          }
        }
      }

      const unreachable = tables.value
        .map(table => table.name)
        .filter(name => !visited.has(name))
      if (unreachable.length > 0) {
        problems.push(
          `表 ${unreachable.join('、')} 没有和主表 ${main} 连接，会形成笛卡尔积`
        )
      }
    }

    // 过滤条件
    for (const [index, filter] of filters.value.entries()) {
      const position = index + 1

      if (!filter.table || !filter.column) {
        problems.push(`第 ${position} 个过滤条件尚未选择字段`)
        continue
      }

      const column = getColumnMeta(filter.table, filter.column)
      if (!column) {
        problems.push(`第 ${position} 个过滤条件的字段不在当前元数据中`)
        continue
      }

      if (!opsForColumn(column).some(op => op.value === filter.op)) {
        problems.push(
          `第 ${position} 个过滤条件：字段 ${column.column_name} 不支持运算符 ${filter.op}`
        )
        continue
      }

      if (isNullOperator(filter.op)) continue

      if (filter.op === 'like' || filter.op === 'not like') {
        if (filter.scalar.length === 0) {
          problems.push(`第 ${position} 个过滤条件：LIKE 的值不能为空`)
        } else if (filter.scalar.length > LIMITS.maxLikeLength) {
          problems.push(
            `第 ${position} 个过滤条件：LIKE 的值最长 ${LIMITS.maxLikeLength} 个字符`
          )
        }
        continue
      }

      if (filter.op === 'in' || filter.op === 'not in') {
        const listError = validateListInput(filter.listText, column)
        if (listError) problems.push(`第 ${position} 个过滤条件：${listError}`)
        continue
      }

      if (filter.op === 'between') {
        const [lower, upper] = filter.between
        if (!lower.trim() || !upper.trim()) {
          problems.push(`第 ${position} 个过滤条件：BETWEEN 需要起始值和结束值`)
          continue
        }
        if (!isValidScalarText(lower, column)) {
          problems.push(
            `第 ${position} 个过滤条件的起始值不是合法的 ${column.data_type} 值`
          )
          continue
        }
        if (!isValidScalarText(upper, column)) {
          problems.push(
            `第 ${position} 个过滤条件的结束值不是合法的 ${column.data_type} 值`
          )
          continue
        }
        if (compareScalarText(lower, upper, column) > 0) {
          problems.push(`第 ${position} 个过滤条件：起始值不能大于结束值`)
        }
        continue
      }

      if (!filter.scalar.trim()) {
        problems.push(
          `第 ${position} 个过滤条件：值不能为空（判断空值请用 IS NULL）`
        )
      } else if (!isValidScalarText(filter.scalar, column)) {
        problems.push(
          `第 ${position} 个过滤条件的值不是合法的 ${column.data_type} 值`
        )
      }
    }

    // 分页
    if (pageIndex.value < 1) problems.push('页码不能小于 1')
    if (pageSize.value < 1 || pageSize.value > LIMITS.maxPageSize) {
      problems.push(`每页条数必须在 1~${LIMITS.maxPageSize} 之间`)
    }
    if ((pageIndex.value - 1) * pageSize.value > LIMITS.maxOffset) {
      problems.push('页码过大，请增加筛选条件后重试')
    }

    return problems
  }

  function showProblems(problems: string[]): void {
    const html = problems.map(problem => `<div>• ${escapeHtml(problem)}</div>`).join('')
    ElMessageBox.alert(html, '请先修正以下问题', {
      dangerouslyUseHTMLString: true,
      confirmButtonText: '知道了',
      type: 'warning'
    }).catch(() => undefined)
  }

  // -------------------------------------------------------------------------
  // 执行查询 / 导出
  // -------------------------------------------------------------------------
  async function executeQuery(): Promise<void> {
    const problems = validateQuery()
    if (problems.length > 0) {
      showProblems(problems)
      return
    }

    const request = buildRequest()
    const sequence = ++querySequence

    loading.value = true
    errorMessage.value = ''

    try {
      const response = await runQuery(request)
      if (sequence !== querySequence) return

      result.value = response
      pageIndex.value = response.page_index
      pageSize.value = response.page_size
      // 用「实际发出去的请求」做签名，查询期间用户若改了条件，导出时会提示条件已变
      executedSignature.value = signatureOf(request)
    } catch (error) {
      if (sequence !== querySequence) return

      result.value = null
      errorMessage.value = extractMessage(error)
      ElMessage.error(errorMessage.value)
    } finally {
      if (sequence === querySequence) {
        loading.value = false
      }
    }
  }

  async function exportReport(): Promise<void> {
    const problems = validateQuery()
    if (problems.length > 0) {
      showProblems(problems)
      return
    }

    const request = buildRequest()
    if (executedSignature.value !== null && signatureOf(request) !== executedSignature.value) {
      try {
        await ElMessageBox.confirm(
          '当前查询条件与上次执行的条件不一致，将按当前条件导出。是否继续？',
          '条件已修改',
          { type: 'warning', confirmButtonText: '继续导出', cancelButtonText: '取消' }
        )
      } catch {
        return
      }
    }

    exporting.value = true
    try {
      const { blob, fileName } = await exportXlsx(request)
      saveBlob(blob, fileName)
      ElMessage.success(`已导出 ${fileName}`)
    } catch (error) {
      ElMessage.error(extractMessage(error))
    } finally {
      exporting.value = false
    }
  }

  /** 清空当前查询条件（保留元数据），用于重新开始一次报表配置 */
  function resetConditions(): void {
    tables.value = []
    fields.value = []
    joins.value = []
    filters.value = []
    manualGroupFields.value = []
    groupMode.value = 'auto'
    pageIndex.value = 1
    result.value = null
    errorMessage.value = ''
    executedSignature.value = null
    // 让仍在途中的查询结果失效；同时收起 loading，避免按钮一直转圈
    querySequence += 1
    loading.value = false
  }

  // -------------------------------------------------------------------------
  // 分页
  // -------------------------------------------------------------------------
  function setPageIndex(index: number): void {
    pageIndex.value = Math.max(1, Math.trunc(index))
  }

  function setPageSize(size: number): void {
    pageSize.value = Math.min(Math.max(1, Math.trunc(size)), LIMITS.maxPageSize)
    pageIndex.value = 1
  }

  function setNeedTotal(value: boolean): void {
    needTotal.value = value
  }

  return {
    // 元数据
    schema,
    metadataLoading,
    metadataError,
    schemaTables,
    schemaColumns,
    getSchemaTable,
    getColumnMeta,
    tableLabel,
    fieldLabel,
    loadMetadata,

    // 查询定义
    tables,
    fields,
    joins,
    filters,
    tableNames,
    mainTable,
    selectedFieldKeys,
    groupMode,
    manualGroupFields,
    hasAggregate,
    nonAggregateFields,
    effectiveGroupBy,
    groupFieldOptions,

    // 表 / 字段
    addTable,
    removeTable,
    toggleField,
    removeField,
    setAggregate,
    setAlias,
    setSort,

    // 关联 / 过滤 / 分组
    updateJoin,
    addFilter,
    updateFilter,
    removeFilter,
    setGroupMode,
    setManualGroups,
    resetConditions,

    // 分页
    pageIndex,
    pageSize,
    needTotal,
    setPageIndex,
    setPageSize,
    setNeedTotal,

    // 执行
    result,
    loading,
    exporting,
    errorMessage,
    previewJson,
    isDirtyForExport,
    validateQuery,
    buildRequest,
    executeQuery,
    exportReport
  }
}

export type QueryBuilder = ReturnType<typeof useQueryBuilder>
