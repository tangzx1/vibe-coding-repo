<script setup lang="ts">
import type {
  Aggregate,
  BiReportColumnSchema,
  BiReportTableSchema,
  OutputFieldState,
  SortOrder
} from '../types/biReport'
import { MAX_IDENTIFIER_LENGTH } from '../config/limits'

const props = defineProps<{
  fields: OutputFieldState[]
  schemaTables: Map<string, BiReportTableSchema>
  schemaColumns: Map<string, BiReportColumnSchema>
}>()

const emit = defineEmits<{
  (e: 'remove', key: string): void
  (e: 'update-aggregate', key: string, value: Aggregate | null): void
  (e: 'update-alias', key: string, value: string): void
  (e: 'update-sort', key: string, value: SortOrder): void
}>()

const AGGREGATE_OPTIONS: ReadonlyArray<{ value: Aggregate | ''; label: string }> = [
  { value: '', label: '不聚合' },
  { value: 'count', label: 'count 计数' },
  { value: 'count_distinct', label: 'count_distinct 去重计数' },
  { value: 'sum', label: 'sum 求和（仅数值）' },
  { value: 'avg', label: 'avg 平均（仅数值）' },
  { value: 'max', label: 'max 最大值' },
  { value: 'min', label: 'min 最小值' }
]

function columnOf(field: OutputFieldState): BiReportColumnSchema | undefined {
  return props.schemaColumns.get(field.key)
}

function tableLabelOf(field: OutputFieldState): string {
  return props.schemaTables.get(field.table)?.display_name ?? field.table
}

function columnLabelOf(field: OutputFieldState): string {
  return columnOf(field)?.display_name ?? field.column
}

function aggregateOptions(field: OutputFieldState) {
  const column = columnOf(field)
  return AGGREGATE_OPTIONS.map(option => ({
    ...option,
    // sum / avg 只对数值列开放，与后端校验一致
    disabled:
      (option.value === 'sum' || option.value === 'avg') && column !== undefined
        ? !column.is_numeric
        : false
  }))
}

function onAggregate(field: OutputFieldState, value: unknown): void {
  const aggregate: Aggregate | null =
    value === '' || value === null || value === undefined ? null : (value as Aggregate)
  emit('update-aggregate', field.key, aggregate)
}

function onAlias(field: OutputFieldState, value: string): void {
  emit('update-alias', field.key, value)
}

function onSort(field: OutputFieldState, value: unknown): void {
  const sort: SortOrder = value === 'asc' || value === 'desc' ? value : ''
  emit('update-sort', field.key, sort)
}
</script>

<template>
  <div class="field-editor">
    <div class="editor-hint">
      别名就是结果列名与 Excel 表头，只能用字母、数字、下划线和汉字（最长
      {{ MAX_IDENTIFIER_LENGTH }} 个字符）。排序按字段顺序生成，最多 5 个。
    </div>

    <el-table
      :data="fields"
      size="small"
      border
      empty-text="还没有输出字段，请在左侧勾选字段"
    >
      <el-table-column label="字段" min-width="210">
        <template #default="{ row }">
          <div class="field-name">
            {{ tableLabelOf(row) }}.{{ columnLabelOf(row) }}
          </div>
          <div class="field-code">
            {{ row.key }}
            <span v-if="columnOf(row)"> · {{ columnOf(row)!.data_type }}</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column label="聚合" width="180">
        <template #default="{ row }">
          <el-select
            :model-value="row.aggregate ?? ''"
            size="small"
            class="cell-control"
            @update:model-value="onAggregate(row, $event)"
          >
            <el-option
              v-for="option in aggregateOptions(row)"
              :key="option.value"
              :label="option.label"
              :value="option.value"
              :disabled="option.disabled"
            />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column label="别名" width="180">
        <template #default="{ row }">
          <el-input
            :model-value="row.alias"
            size="small"
            :maxlength="MAX_IDENTIFIER_LENGTH"
            class="cell-control"
            @update:model-value="onAlias(row, $event)"
          />
        </template>
      </el-table-column>

      <el-table-column label="排序" width="110">
        <template #default="{ row }">
          <el-select
            :model-value="row.sort"
            size="small"
            class="cell-control"
            @update:model-value="onSort(row, $event)"
          >
            <el-option label="不排序" value="" />
            <el-option label="升序" value="asc" />
            <el-option label="降序" value="desc" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column label="操作" width="70" align="center">
        <template #default="{ row }">
          <el-button link type="danger" size="small" @click="emit('remove', row.key)">
            移除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.field-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.editor-hint {
  color: #909399;
  font-size: 12px;
  line-height: 1.6;
}

.field-name {
  color: #303133;
  font-size: 13px;
}

.field-code {
  margin-top: 1px;
  color: #a8abb2;
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.cell-control {
  width: 100%;
}
</style>
