<script setup lang="ts">
import type {
  BiReportColumnSchema,
  BiReportTableSchema,
  JoinState
} from '../types/biReport'
import { isJoinableColumn, sameColumnFamily } from '../utils/identifier'

const props = defineProps<{
  joins: JoinState[]
  tableNames: string[]
  mainTable: string
  schemaTables: Map<string, BiReportTableSchema>
}>()

const emit = defineEmits<{
  (e: 'update', id: string, patch: Partial<JoinState>): void
}>()

function columnsOf(table: string): BiReportColumnSchema[] {
  return props.schemaTables.get(table)?.columns ?? []
}

function findColumn(table: string, column: string): BiReportColumnSchema | undefined {
  return columnsOf(table).find(item => item.column_name === column)
}

function tableOptionLabel(table: string): string {
  const schema = props.schemaTables.get(table)
  return schema ? `${schema.display_name}（${schema.table_name}）` : table
}

function columnOptionLabel(column: BiReportColumnSchema): string {
  return `${column.display_name}（${column.column_name} · ${column.data_type}）`
}

function leftColumnOptions(join: JoinState): BiReportColumnSchema[] {
  return columnsOf(join.leftTable)
}

/** 右侧候选只保留与左侧字段同族的列；左字段还没选时，排除不能参与 join 的列 */
function rightColumnOptions(join: JoinState): BiReportColumnSchema[] {
  const columns = columnsOf(join.rightTable).filter(isJoinableColumn)
  const left = join.leftColumn ? findColumn(join.leftTable, join.leftColumn) : undefined
  if (!left) return columns
  return columns.filter(column => sameColumnFamily(left, column))
}

function rightTableOptions(join: JoinState): string[] {
  return props.tableNames.filter(name => name !== join.leftTable)
}

function hasNoRightCandidate(join: JoinState): boolean {
  return Boolean(
    join.leftColumn &&
    join.rightTable &&
    join.rightColumn === '' &&
    rightColumnOptions(join).length === 0
  )
}

function onJoinType(join: JoinState, value: unknown): void {
  if (value === 'inner' || value === 'left') {
    emit('update', join.id, { joinType: value })
  }
}

function onLeftTable(join: JoinState, value: unknown): void {
  if (typeof value !== 'string') return

  const patch: Partial<JoinState> = { leftTable: value, leftColumn: '' }
  // 左右表不能相同；如果新左表撞上了右表，自动把右表换成另一张
  if (join.rightTable === value) {
    patch.rightTable = props.tableNames.find(name => name !== value) ?? ''
    patch.rightColumn = ''
  }
  emit('update', join.id, patch)
}

function onLeftColumn(join: JoinState, value: unknown): void {
  const column = typeof value === 'string' ? value : ''
  const patch: Partial<JoinState> = { leftColumn: column }

  // 换了左字段后，原右字段可能已经不兼容，直接清掉避免提交时才报错
  const left = column ? findColumn(join.leftTable, column) : undefined
  const right = join.rightColumn ? findColumn(join.rightTable, join.rightColumn) : undefined
  if (left && right && !sameColumnFamily(left, right)) {
    patch.rightColumn = ''
  }
  emit('update', join.id, patch)
}

function onRightTable(join: JoinState, value: unknown): void {
  if (typeof value !== 'string') return
  emit('update', join.id, { rightTable: value, rightColumn: '' })
}

function onRightColumn(join: JoinState, value: unknown): void {
  emit('update', join.id, { rightColumn: typeof value === 'string' ? value : '' })
}
</script>

<template>
  <div class="join-editor">
    <div class="editor-hint">
      主表是 <strong>{{ tableOptionLabel(mainTable) }}</strong>。关联条件由后端按主表重排成树，
      LEFT 表示保留主表一侧的全部行；两端字段必须属于同一类型族（数值 / 日期 / 字符串）。
    </div>

    <div v-for="(join, index) in joins" :key="join.id" class="join-block">
      <div class="join-row">
        <div class="join-index">关联 {{ index + 1 }}</div>

        <el-select
          :model-value="join.joinType"
          size="small"
          class="join-type"
          @update:model-value="onJoinType(join, $event)"
        >
          <el-option label="内连接 INNER" value="inner" />
          <el-option label="左连接 LEFT" value="left" />
        </el-select>

        <el-select
          :model-value="join.leftTable"
          size="small"
          filterable
          placeholder="左表"
          class="join-table"
          @update:model-value="onLeftTable(join, $event)"
        >
          <el-option
            v-for="name in tableNames"
            :key="name"
            :label="tableOptionLabel(name)"
            :value="name"
          />
        </el-select>

        <el-select
          :model-value="join.leftColumn"
          size="small"
          filterable
          clearable
          placeholder="左字段"
          class="join-column"
          @update:model-value="onLeftColumn(join, $event)"
        >
          <el-option
            v-for="column in leftColumnOptions(join)"
            :key="column.column_name"
            :label="columnOptionLabel(column)"
            :value="column.column_name"
          />
        </el-select>

        <span class="join-equals">=</span>

        <el-select
          :model-value="join.rightTable"
          size="small"
          filterable
          placeholder="右表"
          class="join-table"
          @update:model-value="onRightTable(join, $event)"
        >
          <el-option
            v-for="name in rightTableOptions(join)"
            :key="name"
            :label="tableOptionLabel(name)"
            :value="name"
          />
        </el-select>

        <el-select
          :model-value="join.rightColumn"
          size="small"
          filterable
          clearable
          placeholder="右字段（只列同族字段）"
          class="join-column"
          @update:model-value="onRightColumn(join, $event)"
        >
          <el-option
            v-for="column in rightColumnOptions(join)"
            :key="column.column_name"
            :label="columnOptionLabel(column)"
            :value="column.column_name"
          />
        </el-select>
      </div>

      <div v-if="hasNoRightCandidate(join)" class="join-warning">
        右侧表里没有与左侧字段同族的列，请换一个字段或换一张表。
      </div>
    </div>
  </div>
</template>

<style scoped>
.join-editor {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.editor-hint {
  color: var(--app-text-faint);
  font-size: 12px;
  line-height: 1.7;
}

.editor-hint strong {
  color: #cdd5df;
  font-weight: 600;
}

.join-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.join-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.join-index {
  flex: none;
  width: 56px;
  color: var(--app-text-muted);
  font-size: 12px;
}

.join-type {
  width: 128px;
}

.join-table {
  width: 170px;
}

.join-column {
  width: 200px;
}

.join-equals {
  color: var(--app-blue-soft);
  font-weight: 600;
}

.join-warning {
  margin-left: 64px;
  color: var(--app-amber);
  font-size: 12px;
}
</style>
