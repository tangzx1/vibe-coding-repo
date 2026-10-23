<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { Delete, Download, Refresh, Search, View } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
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
  resetConditions,

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
const activeTab = ref<'config' | 'result'>('config')
const editorScrollRef = ref<HTMLElement | null>(null)
const resultAnchorRef = ref<HTMLElement | null>(null)

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

function goConfig(): void {
  activeTab.value = 'config'
  editorScrollRef.value?.scrollTo({ top: 0, behavior: 'smooth' })
}

function goResult(): void {
  activeTab.value = 'result'
  void nextTick(() => {
    resultAnchorRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  })
}

async function handleReset(): Promise<void> {
  if (tables.value.length === 0 && fields.value.length === 0 && filters.value.length === 0) {
    return
  }

  try {
    await ElMessageBox.confirm(
      '将清空当前的表、输出字段、关联条件与过滤条件，是否继续？',
      '清空条件',
      { type: 'warning', confirmButtonText: '清空', cancelButtonText: '取消' }
    )
  } catch {
    return
  }

  resetConditions()
  ElMessage.success('已清空查询条件')
}
</script>

<template>
  <div class="app-shell">
    <header class="app-header">
      <div class="brand">
        <svg class="brand-logo" viewBox="0 0 44 44" aria-hidden="true">
          <polygon points="22,3 39,12 22,21 5,12" fill="#4a9eff" />
          <polygon points="5,12 22,21 22,41 5,32" fill="#1f5fd0" />
          <polygon points="39,12 22,21 22,41 39,32" fill="#25c0d8" />
          <polygon points="22,3 39,12 39,15.5 22,24.5 5,15.5 5,12" fill="rgba(255,255,255,.16)" />
        </svg>
        <div class="brand-text">
          <span class="brand-title">元矿山 · BI 报表</span>
          <span class="brand-sub">多表关联即席查询平台</span>
        </div>
      </div>

      <nav class="top-nav">
        <button
          type="button"
          class="nav-tab"
          :class="{ 'is-active': activeTab === 'config' }"
          @click="goConfig"
        >
          报表查询
        </button>
        <button
          type="button"
          class="nav-tab"
          :class="{ 'is-active': activeTab === 'result' }"
          @click="goResult"
        >
          查询结果
        </button>
        <button type="button" class="nav-tab" @click="previewVisible = true">
          请求预览
        </button>
      </nav>

      <div class="top-status">
        <span class="status-dot" />
        <span class="status-text">只读查询</span>
        <span class="status-divider" />
        <span class="status-text">
          {{ schema ? `${schema.tables.length} 张可查表` : '元数据加载中' }}
        </span>
      </div>
    </header>

    <div class="app-body">
      <aside class="app-sidebar">
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
        <div class="sidebar-foot">
          <span class="foot-dot" />
          <span>参数化 SQL · 元数据缓存</span>
        </div>
      </aside>

      <main class="app-content">
        <div class="content-tabs">
          <button
            type="button"
            class="content-tab"
            :class="{ 'is-active': activeTab === 'config' }"
            @click="goConfig"
          >
            查询配置
          </button>
          <button
            type="button"
            class="content-tab"
            :class="{ 'is-active': activeTab === 'result' }"
            @click="goResult"
          >
            查询结果
            <span v-if="result" class="tab-badge">
              {{ result.total ?? result.items.length }}
            </span>
          </button>
        </div>

        <div class="query-toolbar">
          <div class="toolbar-stats">
            <span class="stat"><b>{{ tables.length }}</b> 张表</span>
            <span class="stat-sep" />
            <span class="stat"><b>{{ fields.length }}</b> 输出字段</span>
            <span class="stat-sep" />
            <span class="stat"><b>{{ filters.length }}</b> 过滤条件</span>
            <span v-if="isDirtyForExport" class="stat-warning">
              <span class="warn-dot" />
              条件已修改
            </span>
          </div>

          <div class="toolbar-actions">
            <el-button :icon="Refresh" :loading="metadataLoading" @click="loadMetadata(true)">
              刷新元数据
            </el-button>
            <el-button type="primary" :icon="Search" :loading="loading" @click="executeQuery">
              查询
            </el-button>
            <el-button type="success" :icon="Download" :loading="exporting" @click="exportReport">
              导出 Excel
            </el-button>
            <el-button :icon="View" @click="previewVisible = true">请求预览</el-button>
            <el-button type="danger" :icon="Delete" @click="handleReset">清空条件</el-button>
          </div>
        </div>

        <div ref="editorScrollRef" class="editor-scroll">
          <div class="editor-stack">
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
                    class="table-tag"
                    :class="{ 'is-main': index === 0 }"
                    type="info"
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

            <div ref="resultAnchorRef" class="section-anchor">
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
            </div>
          </div>
        </div>
      </main>
    </div>
  </div>

  <RequestPreview v-model="previewVisible" :json="previewJson" />
</template>

<style scoped>
/* ==========================================================================
   整体骨架
   ========================================================================== */
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  min-height: 560px;
  background: var(--app-bg);
  overflow: hidden;
}

/* ==========================================================================
   顶部栏
   ========================================================================== */
.app-header {
  flex: none;
  display: flex;
  align-items: center;
  gap: 24px;
  height: 62px;
  padding: 0 20px;
  background: linear-gradient(180deg, #12161b 0%, #0e1115 100%);
  border-bottom: 1px solid #232a32;
  box-shadow: 0 1px 0 rgba(255, 255, 255, 0.02);
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: none;
  min-width: 270px;
}

.brand-logo {
  width: 36px;
  height: 36px;
  flex: none;
  filter: drop-shadow(0 2px 6px rgba(47, 124, 246, 0.35));
}

.brand-text {
  display: flex;
  flex-direction: column;
  line-height: 1.25;
}

.brand-title {
  color: #f2f5f9;
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 0.5px;
  white-space: nowrap;
}

.brand-sub {
  color: #7a8491;
  font-size: 11px;
  letter-spacing: 0.6px;
  white-space: nowrap;
}

.top-nav {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.nav-tab {
  height: 30px;
  padding: 0 22px;
  border: none;
  background: #1a1f25;
  clip-path: polygon(9px 0, 100% 0, calc(100% - 9px) 100%, 0 100%);
  color: #9aa4b0;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.5px;
  white-space: nowrap;
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.nav-tab:hover {
  background: #222932;
  color: #dfe6ee;
}

.nav-tab.is-active {
  background: linear-gradient(180deg, #3d86f7 0%, #2a6fe0 100%);
  color: #ffffff;
  font-weight: 600;
}

.top-status {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: none;
  color: #8d97a4;
  font-size: 12px;
  white-space: nowrap;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--app-green);
  box-shadow: 0 0 8px rgba(60, 174, 77, 0.8);
}

.status-divider {
  width: 1px;
  height: 14px;
  background: #2a313a;
}

/* ==========================================================================
   主体：左侧元数据栏 + 右侧编辑区
   ========================================================================== */
.app-body {
  display: flex;
  flex: 1;
  min-height: 0;
}

.app-sidebar {
  display: flex;
  flex-direction: column;
  flex: none;
  width: 320px;
  min-height: 0;
  background: var(--app-sidebar);
  border-right: 1px solid #232a32;
}

.app-sidebar :deep(.schema-panel) {
  flex: 1;
  min-height: 0;
}

.sidebar-foot {
  display: flex;
  align-items: center;
  flex: none;
  gap: 6px;
  padding: 9px 14px;
  border-top: 1px solid #1e242b;
  color: #65707c;
  font-size: 11px;
  letter-spacing: 0.3px;
}

.foot-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--app-blue);
  box-shadow: 0 0 6px rgba(47, 124, 246, 0.7);
}

.app-content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
  background: var(--app-bg);
}

/* ==========================================================================
   内容页签
   ========================================================================== */
.content-tabs {
  display: flex;
  align-items: flex-end;
  flex: none;
  gap: 28px;
  height: 44px;
  padding: 0 18px;
  background: var(--app-header);
  border-bottom: 1px solid #232a32;
}

.content-tab {
  position: relative;
  height: 100%;
  padding: 0 2px;
  border: none;
  background: none;
  color: #8b95a1;
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
}

.content-tab:hover {
  color: #c3ccd7;
}

.content-tab.is-active {
  color: #f2f5f9;
  font-weight: 600;
}

.content-tab.is-active::after {
  content: '';
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 2px;
  background: linear-gradient(90deg, #2f7cf6, #6aa8ff);
  box-shadow: 0 0 10px rgba(47, 124, 246, 0.55);
}

.tab-badge {
  display: inline-block;
  margin-left: 6px;
  padding: 0 6px;
  border: 1px solid rgba(47, 124, 246, 0.4);
  border-radius: 8px;
  background: rgba(47, 124, 246, 0.16);
  color: var(--app-blue-soft);
  font-size: 11px;
  font-weight: 500;
  line-height: 16px;
  vertical-align: 1px;
}

/* ==========================================================================
   工具栏
   ========================================================================== */
.query-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex: none;
  flex-wrap: wrap;
  gap: 12px 16px;
  padding: 10px 18px;
  background: var(--app-header);
  border-bottom: 1px solid #232a32;
}

.toolbar-stats {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 14px;
  color: var(--app-text-muted);
  font-size: 12px;
}

.stat b {
  margin-right: 3px;
  color: #dce3eb;
  font-size: 14px;
  font-weight: 600;
}

.stat-sep {
  width: 1px;
  height: 14px;
  background: #2a313a;
}

.stat-warning {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--app-amber);
}

.warn-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--app-amber);
  box-shadow: 0 0 6px rgba(224, 160, 58, 0.7);
}

.toolbar-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.toolbar-actions :deep(.el-button) {
  height: 32px;
  margin-left: 0;
  padding: 0 15px;
  border-radius: 2px;
  font-size: 12.5px;
  font-weight: 600;
  letter-spacing: 0.3px;
}

.toolbar-actions :deep(.el-button + .el-button) {
  margin-left: 0;
}

.toolbar-actions :deep(.el-button--primary) {
  --el-button-bg-color: #2f7cf6;
  --el-button-border-color: #3f88f8;
  --el-button-hover-bg-color: #4a8df8;
  --el-button-hover-border-color: #5b9bff;
  --el-button-active-bg-color: #1f63d4;
  --el-button-active-border-color: #1f63d4;
  box-shadow: 0 2px 8px rgba(47, 124, 246, 0.28);
}

.toolbar-actions :deep(.el-button--success) {
  --el-button-bg-color: #3cae4d;
  --el-button-border-color: #45b956;
  --el-button-hover-bg-color: #4dc05e;
  --el-button-hover-border-color: #5ccb6d;
  --el-button-active-bg-color: #2f8c3e;
  --el-button-active-border-color: #2f8c3e;
  box-shadow: 0 2px 8px rgba(60, 174, 77, 0.24);
}

.toolbar-actions :deep(.el-button--danger) {
  --el-button-bg-color: #d9534f;
  --el-button-border-color: #e05f5b;
  --el-button-hover-bg-color: #e56b67;
  --el-button-hover-border-color: #ec7d79;
  --el-button-active-bg-color: #ad413e;
  --el-button-active-border-color: #ad413e;
  box-shadow: 0 2px 8px rgba(217, 83, 79, 0.22);
}

/* ==========================================================================
   编辑区
   ========================================================================== */
.editor-scroll {
  flex: 1;
  min-height: 0;
  padding: 12px 18px 28px;
  overflow: auto;
}

.editor-stack {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-anchor {
  scroll-margin-top: 8px;
}

.panel {
  --el-card-bg-color: #111519;
  --el-card-border-color: #262d36;
  border-radius: 3px;
}

.panel :deep(.el-card__header) {
  padding: 9px 14px;
  background: linear-gradient(180deg, #191e25 0%, #161b21 100%);
  border-bottom: 1px solid #262d36;
}

.panel :deep(.el-card__body) {
  padding: 12px 14px;
  background: #111519;
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

.panel-sub {
  color: var(--app-text-muted);
  font-size: 12px;
}

.add-table-select {
  width: 200px;
}

/* ==========================================================================
   表标签
   ========================================================================== */
.table-chips {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 26px;
}

.table-chips :deep(.el-tag) {
  height: 26px;
  padding: 0 9px;
  border-radius: 2px;
  font-size: 12px;
}

.table-chips :deep(.el-tag.is-main) {
  --el-tag-bg-color: rgba(47, 124, 246, 0.13);
  --el-tag-border-color: rgba(47, 124, 246, 0.5);
  --el-tag-text-color: #82b4ff;
}

.main-badge {
  margin-left: 5px;
  padding: 0 4px;
  border-radius: 2px;
  background: rgba(47, 124, 246, 0.22);
  color: #9cc4ff;
  font-size: 10px;
}

.empty-hint {
  color: var(--app-text-faint);
  font-size: 12px;
  line-height: 1.7;
}

/* ==========================================================================
   分页设置行
   ========================================================================== */
.option-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.option-label {
  color: var(--app-text-regular);
  font-size: 12.5px;
}

.page-size-select {
  width: 110px;
}

.option-hint {
  color: var(--app-text-faint);
  font-size: 12px;
}

/* ==========================================================================
   响应式
   ========================================================================== */
@media (max-width: 1280px) {
  .app-sidebar {
    width: 292px;
  }

  .brand {
    min-width: 220px;
  }

  .brand-sub {
    display: none;
  }

  .toolbar-actions :deep(.el-button) {
    padding: 0 11px;
  }
}

@media (max-width: 1080px) {
  .top-nav {
    display: none;
  }

  .top-status .status-divider,
  .top-status .status-text:last-child {
    display: none;
  }
}

@media (max-width: 900px) {
  .app-shell {
    height: auto;
    min-height: 100vh;
    overflow: visible;
  }

  .app-header {
    flex-wrap: wrap;
    height: auto;
    padding: 10px 14px;
  }

  .brand {
    min-width: 0;
  }

  .app-body {
    flex-direction: column;
  }

  .app-sidebar {
    width: 100%;
    height: 340px;
    border-right: none;
    border-bottom: 1px solid #232a32;
  }

  .app-content {
    min-height: 600px;
  }

  .editor-scroll {
    overflow: visible;
  }
}
</style>
