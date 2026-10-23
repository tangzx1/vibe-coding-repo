<script setup lang="ts">
import type {
  BiReportColumnSchema,
  BiReportTableSchema,
  FilterOp,
  FilterState
} from '../types/biReport'
import { isNullOperator, opsForColumn } from '../utils/value'

const props = defineProps<{
  filters: FilterState[]
  tableNames: string[]
  schemaTables: Map<string, BiReportTableSchema>
}>()

const emit = defineEmits<{
  (e: 'add'): void
  (e: 'remove', id: string): void
  (e: 'update', id: string, patch: Partial<FilterState>): void
}>()

function columnsOf(table: string): BiReportColumnSchema[] {
  return props.schemaTables.get(table)?.columns ?? []
}

function columnOf(filter: FilterState): BiReportColumnSchema | undefined {
  return columnsOf(filter.table).find(column => column.column_name === filter.column)
}

function tableOptionLabel(table: string): string {
  const schema = props.schemaTables.get(table)
  return schema ? `${schema.display_name}（${schema.table_name}）` : table
}

function columnOptionLabel(column: BiReportColumnSchema): string {
  return `${column.display_name}（${column.column_name} · ${column.data_type}）`
}

function operatorOptions(filter: FilterState) {
  return opsForColumn(columnOf(filter) ?? null)
}

function dateTypeOf(filter: FilterState): 'date' | 'datetime' {
  return columnOf(filter)?.data_type === 'date' ? 'date' : 'datetime'
}

function dateFormatOf(filter: FilterState): string {
  return columnOf(filter)?.data_type === 'date' ? 'YYYY-MM-DD' : 'YYYY-MM-DD HH:mm:ss'
}

function defaultTimeOf(filter: FilterState, end: boolean): Date | undefined {
  if (columnOf(filter)?.data_type === 'date') return undefined
  return end
    ? new Date(2000, 0, 1, 23, 59, 59)
    : new Date(2000, 0, 1, 0, 0, 0)
}

function onTable(filter: FilterState, value: unknown): void {
  if (typeof value !== 'string') return
  emit('update', filter.id, {
    table: value,
    column: '',
    op: '=',
    scalar: '',
    between: ['', ''],
    listText: ''
  })
}

function onColumn(filter: FilterState, value: unknown): void {
  if (typeof value !== 'string') return

  const column = columnsOf(filter.table).find(item => item.column_name === value)
  const allowed = opsForColumn(column ?? null)
  const op = allowed.some(item => item.value === filter.op) ? filter.op : '='

  emit('update', filter.id, {
    column: value,
    op,
    scalar: '',
    between: ['', ''],
    listText: ''
  })
}

function onOperator(filter: FilterState, value: unknown): void {
  if (typeof value !== 'string') return
  if (!operatorOptions(filter).some(option => option.value === value)) return

  const op = value as FilterOp
  const patch: Partial<FilterState> = { op }
  // IS NULL / IS NOT NULL 不接受比较值，切换过去时顺手清空，避免「填了却没用上」
  if (isNullOperator(op)) {
    patch.scalar = ''
    patch.between = ['', '']
    patch.listText = ''
  }
  emit('update', filter.id, patch)
}

function onScalar(filter: FilterState, value: unknown): void {
  emit('update', filter.id, { scalar: typeof value === 'string' ? value : '' })
}

function onBetween(filter: FilterState, index: 0 | 1, value: unknown): void {
  const between: [string, string] = [filter.between[0], filter.between[1]]
  between[index] = typeof value === 'string' ? value : ''
  emit('update', filter.id, { between })
}

function onList(filter: FilterState, value: string): void {
  emit('update', filter.id, { listText: value })
}
</script>

<template>
  <div class="filter-editor">
    <div class="editor-hint">
      多个条件之间是「与」的关系。IN 每行一个值（数值列也可用逗号分隔）；
      LIKE 的 % 和 _ 需要自己写。判断空值请用 IS NULL / IS NOT NULL。
    </div>

    <div v-if="filters.length === 0" class="filter-empty">
      还没有过滤条件。不加条件会扫描全表，大表建议至少加一个时间或主键范围。
    </div>

    <div v-for="(filter, index) in filters" :key="filter.id" class="filter-row">
      <div class="filter-index">{{ index + 1 }}</div>

      <el-select
        :model-value="filter.table"
        size="small"
        filterable
        placeholder="表"
        class="filter-table"
        @update:model-value="onTable(filter, $event)"
      >
        <el-option
          v-for="name in tableNames"
          :key="name"
          :label="tableOptionLabel(name)"
          :value="name"
        />
      </el-select>

      <el-select
        :model-value="filter.column"
        size="small"
        filterable
        placeholder="字段"
        class="filter-column"
        @update:model-value="onColumn(filter, $event)"
      >
        <el-option
          v-for="column in columnsOf(filter.table)"
          :key="column.column_name"
          :label="columnOptionLabel(column)"
          :value="column.column_name"
        />
      </el-select>

      <el-select
        :model-value="filter.op"
        size="small"
        placeholder="运算符"
        class="filter-op"
        @update:model-value="onOperator(filter, $event)"
      >
        <el-option
          v-for="option in operatorOptions(filter)"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>

      <div class="filter-value">
        <span v-if="isNullOperator(filter.op)" class="no-value">无需填写值</span>

        <div v-else-if="filter.op === 'between'" class="value-group">
          <template v-if="columnOf(filter)?.is_date">
            <el-date-picker
              :model-value="filter.between[0] || null"
              :type="dateTypeOf(filter)"
              :value-format="dateFormatOf(filter)"
              :default-time="defaultTimeOf(filter, false)"
              size="small"
              placeholder="起始"
              class="range-input"
              @update:model-value="onBetween(filter, 0, $event)"
            />
            <span class="range-sep">至</span>
            <el-date-picker
              :model-value="filter.between[1] || null"
              :type="dateTypeOf(filter)"
              :value-format="dateFormatOf(filter)"
              :default-time="defaultTimeOf(filter, true)"
              size="small"
              placeholder="结束"
              class="range-input"
              @update:model-value="onBetween(filter, 1, $event)"
            />
          </template>
          <template v-else>
            <el-input
              :model-value="filter.between[0]"
              size="small"
              placeholder="起始值"
              class="range-input"
              @update:model-value="onBetween(filter, 0, $event)"
            />
            <span class="range-sep">至</span>
            <el-input
              :model-value="filter.between[1]"
              size="small"
              placeholder="结束值"
              class="range-input"
              @update:model-value="onBetween(filter, 1, $event)"
            />
          </template>
        </div>

        <el-input
          v-else-if="filter.op === 'in' || filter.op === 'not in'"
          type="textarea"
          :rows="2"
          size="small"
          :model-value="filter.listText"
          placeholder="每行一个值；数值列也可用逗号分隔"
          @update:model-value="onList(filter, $event)"
        />

        <el-input
          v-else-if="filter.op === 'like' || filter.op === 'not like'"
          size="small"
          :model-value="filter.scalar"
          placeholder="支持 % 和 _，如 %01%"
          @update:model-value="onScalar(filter, $event)"
        />

        <template v-else>
          <el-date-picker
            v-if="columnOf(filter)?.is_date"
            :model-value="filter.scalar || null"
            :type="dateTypeOf(filter)"
            :value-format="dateFormatOf(filter)"
            :default-time="defaultTimeOf(filter, false)"
            size="small"
            placeholder="选择日期时间"
            class="filter-input"
            @update:model-value="onScalar(filter, $event)"
          />
          <el-input
            v-else
            size="small"
            :model-value="filter.scalar"
            placeholder="值"
            @update:model-value="onScalar(filter, $event)"
          />
        </template>
      </div>

      <el-button
        class="filter-remove"
        link
        type="danger"
        size="small"
        @click="emit('remove', filter.id)"
      >
        删除
      </el-button>
    </div>

    <div>
      <el-button size="small" :disabled="tableNames.length === 0" @click="emit('add')">
        + 添加过滤条件
      </el-button>
    </div>
  </div>
</template>

<style scoped>
.filter-editor {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.editor-hint {
  color: var(--app-text-faint);
  font-size: 12px;
  line-height: 1.7;
}

.filter-empty {
  color: var(--app-text-muted);
  font-size: 12px;
}

.filter-row {
  display: flex;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 8px;
  padding-bottom: 8px;
  border-bottom: 1px dashed #262d36;
}

.filter-index {
  flex: none;
  width: 20px;
  line-height: 28px;
  color: var(--app-text-muted);
  font-size: 12px;
  text-align: right;
}

.filter-table {
  width: 170px;
}

.filter-column {
  width: 210px;
}

.filter-op {
  width: 150px;
}

.filter-value {
  flex: 1;
  min-width: 240px;
}

.filter-input {
  width: 100%;
}

.value-group {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.range-input {
  width: 190px;
}

.range-sep {
  color: var(--app-text-muted);
  font-size: 12px;
}

.no-value {
  display: inline-block;
  line-height: 28px;
  color: var(--app-text-faint);
  font-size: 12px;
}

.filter-remove {
  flex: none;
  line-height: 28px;
}
</style>
