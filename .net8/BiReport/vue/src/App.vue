<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Download, Refresh, Search, View } from '@element-plus/icons-vue'
import SchemaPanel from './components/SchemaPanel.vue'
import FieldEditor from './components/FieldEditor.vue'
import JoinEditor from './components/JoinEditor.vue'
import FilterEditor from './components/FilterEditor.vue'
import GroupEditor from './components/GroupEditor.vue'
import ResultPanel from './components/ResultPanel.vue'
import RequestPreview from './components/RequestPreview.vue'
import { useQueryBuilder } from './composables/useQueryBuilder'
import { LIMITS, PAGE_SIZE_OPTIONS } from './config/limits'

const {
  schema,
  metadataLoading,
  metadataError,
  schemaTables,
  schemaColumns,
  tableLabel,
  loadMetadata,

  tables,
  fields,
  joins,
  filters,
  tableNames,
  mainTable,
  groupMode,
  manualGroupFields,
  hasAggregate,
  groupFieldOptions,

  addTable,
  removeTable,
  toggleField,
  removeField,
  setAggregate,
  setAlias,
  setSort,

  updateJoin,
  addFilter,
  updateFilter,
  removeFilter,
  setGroupMode,
  setManualGroups,

  pageSize,
  needTotal,
  setPageIndex,
  setPageSize,
  setNeedTotal,

  result,
  loading,
  exporting,
  errorMessage,
  previewJson,
  isDirtyForExport,
  executeQuery,
  exportReport
} = useQueryBuilder()

const previewVisible = ref(false)
const tableToAdd = ref('')

const selectedFieldList = computed(() => fields.value.map(field => field.key))

const addableTables = computed(() =>
  (schema.value?.tables ?? []).filter(table => !tableNames.value.includes(table.table_name))
)

onMounted(() => {
  void loadMetadata(false)
})

function handleAddTable(value: unknown): void {
  if (typeof value === 'string') addTable(value)
  tableToAdd.value = ''
}

function handlePageSize(value: unknown): void {
  if (typeof value !== 'number') return
  setPageSize(value)
  if (result.value) void executeQuery()
}

function handleNeedTotal(value: unknown): void {
  if (typeof value !== 'boolean') return
  setNeedTotal(value)
  if (result.value) void executeQuery()
}

function handlePageChange(page: number): void {
  setPageIndex(page)
  void executeQuery()
}

function handleSizeChange(size: number): void {
  setPageSize(size)
  void executeQuery()
}
</script>

<template>
  <el-container class="app-shell">
    <el-header class="app-header" height="56px">
      <div class="brand">
        <span class="brand-title">BI 报表查询</span>
        <span class="brand-sub">即席关联查询 · 不保存报表定义</span>
      </div>

      <div class="toolbar">
        <el-button
          :icon="Refresh"
          :loading="metadataLoading"
          @click="loadMetadata(true)"
        >
          刷新元数据
        </el-button>
        <el-button type="primary" :icon="Search" :loading="loading" @click="executeQuery">
          查询
        </el-button>
        <el-button :icon="Download" :loading="exporting" @click="exportReport">
          导出 Excel
        </el-button>
        <el-button :icon="View" @click="previewVisible = true">请求预览</el-button>
      </div>
    </el-header>

    <el-main class="app-main">
      <div class="workspace">
        <aside class="schema-col">
          <SchemaPanel
            :schema="schema"
            :loading="metadataLoading"
            :error-message="metadataError"
            :selected-tables="tableNames"
            :selected-fields="selectedFieldList"
            @add-table="addTable"
            @toggle-field="toggleField"
            @reload="loadMetadata(true)"
          />
        </aside>

        <section class="editor-col">
          <el-card shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">
                  参与查询的表
                  <el-tag size="small" type="info" effect="plain">
                    {{ tables.length }} / {{ LIMITS.maxJoinTables }}
                  </el-tag>
                </span>
                <el-select
                  v-model="tableToAdd"
                  size="small"
                  placeholder="添加表"
                  class="add-table-select"
                  :disabled="addableTables.length === 0"
                  @change="handleAddTable"
                >
                  <el-option
                    v-for="table in addableTables"
                    :key="table.table_name"
                    :label="`${table.display_name}（${table.table_name}）`"
                    :value="table.table_name"
                  />
                </el-select>
              </div>
            </template>

            <div class="table-chips">
              <template v-if="tables.length > 0">
                <el-tag
                  v-for="(table, index) in tables"
                  :key="table.name"
                  :type="index === 0 ? 'success' : 'info'"
                  effect="plain"
                  closable
                  @close="removeTable(table.name)"
                >
                  {{ tableLabel(table.name) }}
                  <span v-if="index === 0" class="main-badge">主表</span>
                </el-tag>
              </template>
              <span v-else class="empty-hint">
                从左侧勾选字段会自动加入表；桥接表可以用右上角「添加表」只加入、不输出字段。
              </span>
            </div>
          </el-card>

          <el-card v-if="tables.length > 1" shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">
                  关联条件
                  <el-tag size="small" type="info" effect="plain">{{ joins.length }} 个</el-tag>
                </span>
              </div>
            </template>
            <JoinEditor
              :joins="joins"
              :table-names="tableNames"
              :main-table="mainTable"
              :schema-tables="schemaTables"
              @update="updateJoin"
            />
          </el-card>

          <el-card shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">
                  输出字段
                  <el-tag size="small" type="info" effect="plain">{{ fields.length }} 个</el-tag>
                </span>
              </div>
            </template>
            <FieldEditor
              :fields="fields"
              :schema-tables="schemaTables"
              :schema-columns="schemaColumns"
              @remove="removeField"
              @update-aggregate="setAggregate"
              @update-alias="setAlias"
              @update-sort="setSort"
            />
          </el-card>

          <el-card shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">
                  过滤条件
                  <el-tag size="small" type="info" effect="plain">{{ filters.length }} 个</el-tag>
                </span>
              </div>
            </template>
            <FilterEditor
              :filters="filters"
              :table-names="tableNames"
              :schema-tables="schemaTables"
              @add="addFilter"
              @remove="removeFilter"
              @update="updateFilter"
            />
          </el-card>

          <el-card shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">分组</span>
              </div>
            </template>
            <GroupEditor
              :mode="groupMode"
              :manual-fields="manualGroupFields"
              :field-options="groupFieldOptions"
              :has-aggregate="hasAggregate"
              @update:mode="setGroupMode"
              @update:manual="setManualGroups"
            />
          </el-card>

          <el-card shadow="never" class="panel">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">分页</span>
              </div>
            </template>
            <div class="option-row">
              <span class="option-label">每页条数</span>
              <el-select
                :model-value="pageSize"
                size="small"
                class="page-size-select"
                @update:model-value="handlePageSize"
              >
                <el-option
                  v-for="size in PAGE_SIZE_OPTIONS"
                  :key="size"
                  :label="`${size} 条`"
                  :value="size"
                />
              </el-select>

              <span class="option-label">统计总数</span>
              <el-switch :model-value="needTotal" @update:model-value="handleNeedTotal" />
              <span class="option-hint">
                关闭后响应里的 total 为 null，可省一次 count 查询
              </span>
            </div>
          </el-card>

          <el-card shadow="never" class="panel result-card">
            <template #header>
              <div class="panel-head">
                <span class="panel-title">
                  查询结果
                  <el-tag
                    v-if="isDirtyForExport"
                    size="small"
                    type="warning"
                    effect="plain"
                  >
                    条件已修改，结果不是最新的
                  </el-tag>
                </span>
                <span v-if="result" class="panel-sub">{{ result.columns.length }} 列</span>
              </div>
            </template>
            <ResultPanel
              :result="result"
              :loading="loading"
              :error-message="errorMessage"
              @page-change="handlePageChange"
              @size-change="handleSizeChange"
            />
          </el-card>
        </section>
      </div>
    </el-main>
  </el-container>

  <RequestPreview v-model="previewVisible" :json="previewJson" />
</template>

<style scoped>
.app-shell {
  height: 100vh;
}

.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 0 20px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
}

.brand {
  display: flex;
  align-items: baseline;
  gap: 10px;
  min-width: 0;
}

.brand-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.brand-sub {
  color: #a8abb2;
  font-size: 12px;
}

.toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.toolbar :deep(.el-button + .el-button) {
  margin-left: 0;
}

.app-main {
  padding: 16px;
  overflow: hidden;
  min-height: 0;
}

.workspace {
  display: flex;
  gap: 16px;
  height: 100%;
  min-height: 0;
}

.schema-col {
  flex: none;
  width: 340px;
  min-height: 0;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  overflow: hidden;
}

.editor-col {
  flex: 1;
  min-width: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-right: 4px;
  padding-bottom: 16px;
}

.panel {
  flex: none;
}

.panel :deep(.el-card__header) {
  padding: 10px 14px;
}

.panel :deep(.el-card__body) {
  padding: 12px 14px;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.panel-title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  color: #303133;
  font-size: 14px;
  font-weight: 600;
}

.panel-sub {
  color: #909399;
  font-size: 12px;
}

.add-table-select {
  width: 190px;
}

.table-chips {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 24px;
}

.main-badge {
  margin-left: 4px;
  padding: 0 4px;
  background: #f0f9eb;
  border-radius: 2px;
  color: #67c23a;
  font-size: 10px;
}

.empty-hint {
  color: #a8abb2;
  font-size: 12px;
  line-height: 1.6;
}

.option-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.option-label {
  color: #606266;
  font-size: 13px;
}

.page-size-select {
  width: 110px;
}

.option-hint {
  color: #a8abb2;
  font-size: 12px;
}

@media (max-width: 1180px) {
  .schema-col {
    width: 290px;
  }
}

@media (max-width: 900px) {
  .brand-sub {
    display: none;
  }

  .workspace {
    flex-direction: column;
    overflow: auto;
  }

  .schema-col {
    width: 100%;
    height: 360px;
  }

  .editor-col {
    overflow: visible;
  }
}
</style>
