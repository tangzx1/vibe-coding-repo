<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Search } from '@element-plus/icons-vue'
import type { BiReportColumnSchema, BiReportSchema, BiReportTableSchema } from '../types/biReport'
import { formatCell } from '../utils/format'

const props = defineProps<{
  schema: BiReportSchema | null
  loading: boolean
  errorMessage: string
  selectedTables: string[]
  selectedFields: string[]
}>()

const emit = defineEmits<{
  (e: 'add-table', table: string): void
  (e: 'toggle-field', table: string, column: string): void
  (e: 'reload'): void
}>()

interface TableItem {
  table: BiReportTableSchema
  columns: BiReportColumnSchema[]
}

const keyword = ref('')
const activeNames = ref<string[]>([])

const selectedTableSet = computed(() => new Set(props.selectedTables))
const selectedFieldSet = computed(() => new Set(props.selectedFields))

const totalColumns = computed(() =>
  props.schema?.tables.reduce((sum, table) => sum + table.columns.length, 0) ?? 0
)

const generatedTime = computed(() => {
  const value = props.schema?.generated_at
  return value ? value.replace('T', ' ').slice(11, 19) : ''
})

const filteredTables = computed<TableItem[]>(() => {
  const all = props.schema?.tables ?? []
  const query = keyword.value.trim().toLowerCase()
  if (!query) {
    return all.map(table => ({ table, columns: table.columns }))
  }

  return all
    .map(table => {
      const tableHit =
        table.table_name.toLowerCase().includes(query) ||
        table.display_name.toLowerCase().includes(query) ||
        (table.table_comment ?? '').toLowerCase().includes(query)

      const columns = tableHit
        ? table.columns
        : table.columns.filter(column =>
            column.column_name.toLowerCase().includes(query) ||
            column.display_name.toLowerCase().includes(query) ||
            (column.column_comment ?? '').toLowerCase().includes(query)
          )

      return { table, columns }
    })
    .filter(item => item.columns.length > 0)
})

function fieldKey(table: string, column: string): string {
  return `${table}.${column}`
}

function onActiveChange(value: unknown): void {
  if (Array.isArray(value)) {
    activeNames.value = value.filter((item): item is string => typeof item === 'string')
  }
}

// 搜索时把命中的表全部展开，清空搜索后恢复「只展开第一张表」的紧凑视图
watch(keyword, value => {
  if (value.trim()) {
    activeNames.value = filteredTables.value.map(item => item.table.table_name)
    return
  }
  const tables = props.schema?.tables ?? []
  activeNames.value = tables.slice(0, 1).map(table => table.table_name)
})

// 元数据首次加载后默认展开第一张表，避免一上来就是 100 多行
watch(
  () => props.schema,
  value => {
    if (value && activeNames.value.length === 0) {
      activeNames.value = value.tables.slice(0, 1).map(table => table.table_name)
    }
  },
  { immediate: true }
)
</script>

<template>
  <div v-loading="loading" class="schema-panel">
    <div class="schema-head">
      <div>
        <div class="panel-title">表与字段</div>
        <div v-if="schema" class="schema-meta">
          {{ schema.tables.length }} 张表 · {{ totalColumns }} 个字段
          <span v-if="generatedTime"> · 更新 {{ generatedTime }}</span>
        </div>
      </div>
      <el-button size="small" text type="primary" :loading="loading" @click="emit('reload')">
        刷新
      </el-button>
    </div>

    <div v-if="errorMessage" class="schema-error">
      <el-alert :title="errorMessage" type="error" show-icon :closable="false" />
      <el-button size="small" @click="emit('reload')">重试</el-button>
    </div>

    <template v-else>
      <div class="schema-search">
        <el-input
          v-model="keyword"
          :prefix-icon="Search"
          clearable
          size="small"
          placeholder="搜索表名 / 字段名 / 注释"
        />
      </div>

      <el-collapse
        v-if="filteredTables.length > 0"
        class="schema-list"
        :model-value="activeNames"
        @update:model-value="onActiveChange"
      >
        <el-collapse-item
          v-for="item in filteredTables"
          :key="item.table.table_name"
          :name="item.table.table_name"
          :class="{ 'is-query-table': selectedTableSet.has(item.table.table_name) }"
        >
          <template #title>
            <div class="table-title">
              <span class="table-name">{{ item.table.display_name }}</span>
              <span class="table-code">{{ item.table.table_name }}</span>
              <el-tag
                v-if="selectedTableSet.has(item.table.table_name)"
                size="small"
                type="success"
                effect="plain"
              >
                已加入
              </el-tag>
              <el-button
                v-else
                size="small"
                text
                type="primary"
                @click.stop="emit('add-table', item.table.table_name)"
              >
                加入查询
              </el-button>
            </div>
          </template>

          <div
            v-for="column in item.columns"
            :key="column.column_name"
            class="column-row"
          >
            <el-checkbox
              class="column-checkbox"
              :model-value="selectedFieldSet.has(fieldKey(item.table.table_name, column.column_name))"
              @change="emit('toggle-field', item.table.table_name, column.column_name)"
            >
              <span class="column-display">{{ formatCell(column.display_name) }}</span>
              <span class="column-code">{{ column.column_name }}</span>
            </el-checkbox>
            <span class="column-type">{{ column.data_type }}</span>
          </div>
        </el-collapse-item>
      </el-collapse>

      <el-empty
        v-else-if="schema"
        :description="keyword ? '没有匹配的表或字段' : '没有可查询的表'"
        :image-size="60"
      />
    </template>
  </div>
</template>

<style scoped>
.schema-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: var(--app-sidebar);
}

.schema-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex: none;
  gap: 8px;
  padding: 14px 14px 10px;
  border-bottom: 1px solid #1e242b;
}

.panel-title {
  display: flex;
  align-items: center;
  gap: 7px;
  color: #dbe2ea;
  font-size: 13.5px;
  font-weight: 600;
  letter-spacing: 0.3px;
}

.panel-title::before {
  content: '';
  flex: none;
  width: 3px;
  height: 13px;
  border-radius: 1px;
  background: linear-gradient(180deg, #5b9bff, #1f63d4);
  box-shadow: 0 0 6px rgba(47, 124, 246, 0.45);
}

.schema-meta {
  margin-top: 3px;
  color: var(--app-text-faint);
  font-size: 11.5px;
}

.schema-error {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 14px 14px;
}

.schema-search {
  flex: none;
  padding: 10px 12px 8px;
}

.schema-list {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 0 10px 12px;
  border-top: none;
  border-bottom: none;
}

.schema-list :deep(.el-collapse-item__header) {
  height: auto;
  min-height: 38px;
  padding: 4px 8px 4px 10px;
  line-height: 1.4;
  border-left: 2px solid transparent;
  border-radius: 2px;
  background: transparent;
  transition: background 0.15s ease, border-color 0.15s ease;
}

.schema-list :deep(.el-collapse-item__header:hover) {
  background: #141a20;
}

.schema-list :deep(.el-collapse-item.is-query-table > .el-collapse-item__header) {
  border-left-color: var(--app-blue);
  background: linear-gradient(90deg, rgba(47, 124, 246, 0.12), transparent 82%);
}

.schema-list :deep(.el-collapse-item__header.is-active) {
  color: #eef3f8;
}

.schema-list :deep(.el-collapse-item__content) {
  padding-bottom: 6px;
  color: var(--app-text-regular);
}

.schema-list :deep(.el-collapse-item__arrow) {
  color: #5f6a76;
}

.table-title {
  display: flex;
  align-items: center;
  flex: 1;
  flex-wrap: nowrap;
  gap: 6px;
  min-width: 0;
  overflow: hidden;
  padding-right: 8px;
}

.table-name {
  flex: none;
  color: #c3ccd7;
  font-weight: 600;
  font-size: 13px;
  white-space: nowrap;
}

.is-query-table .table-name {
  color: #ffffff;
}

.table-code {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  color: var(--app-text-faint);
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.table-title :deep(.el-tag),
.table-title :deep(.el-button) {
  flex: none;
}

.column-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 1px 4px 1px 14px;
  border-radius: 2px;
}

.column-row:hover {
  background: var(--app-hover);
}

.column-checkbox {
  flex: 1;
  min-width: 0;
  height: 26px;
}

.column-checkbox :deep(.el-checkbox__label) {
  min-width: 0;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  font-size: 12.5px;
}

.column-display {
  color: #b7c1cd;
}

.column-code {
  margin-left: 6px;
  color: var(--app-text-faint);
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.column-type {
  flex: none;
  color: #5c6672;
  font-size: 10.5px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}
</style>
