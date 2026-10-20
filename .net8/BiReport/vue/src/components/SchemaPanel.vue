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

const generatedAt = computed(() => {
  const value = props.schema?.generated_at
  return value ? value.replace('T', ' ').slice(0, 19) : ''
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
          <span v-if="generatedAt"> · 元数据 {{ generatedAt }}</span>
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
}

.schema-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 14px 6px;
}

.panel-title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.schema-meta {
  margin-top: 2px;
  color: #909399;
  font-size: 12px;
}

.schema-error {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px 14px 14px;
}

.schema-search {
  padding: 4px 12px 8px;
}

.schema-list {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 0 12px 12px;
  border-top: none;
  border-bottom: none;
}

.schema-list :deep(.el-collapse-item__header) {
  height: auto;
  min-height: 40px;
  padding: 4px 0;
  line-height: 1.4;
}

.schema-list :deep(.el-collapse-item__content) {
  padding-bottom: 8px;
}

.table-title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  min-width: 0;
  padding-right: 8px;
}

.table-name {
  font-weight: 600;
  color: #303133;
}

.table-code {
  color: #909399;
  font-size: 12px;
}

.column-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 1px 0 1px 10px;
  border-radius: 4px;
}

.column-row:hover {
  background: #f5f7fa;
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
  font-size: 13px;
}

.column-display {
  color: #303133;
}

.column-code {
  margin-left: 6px;
  color: #a8abb2;
  font-size: 12px;
}

.column-type {
  flex: none;
  color: #c0c4cc;
  font-size: 11px;
}
</style>
