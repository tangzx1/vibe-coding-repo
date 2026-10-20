<script setup lang="ts">
import { computed } from 'vue'
import type { BiReportResult } from '../types/biReport'
import { formatCell } from '../utils/format'

const props = defineProps<{
  result: BiReportResult | null
  loading: boolean
  errorMessage: string
}>()

const emit = defineEmits<{
  (e: 'page-change', page: number): void
  (e: 'size-change', size: number): void
}>()

const columns = computed(() => props.result?.columns ?? [])
const rows = computed(() => props.result?.items ?? [])

const emptyText = computed(() =>
  props.result ? '没有查询到符合条件的数据' : '设置查询条件后点击「查询」查看结果'
)

const canNext = computed(() => {
  const current = props.result
  if (!current) return false
  if (current.total !== null) return current.page_index < current.total_pages
  // 未统计总数时，只有本页取满才可能有下一页
  return current.items.length >= current.page_size
})

function cell(row: Record<string, unknown>, column: string): string {
  return formatCell(row[column])
}

function onCurrentChange(page: number): void {
  emit('page-change', page)
}

function onSizeChange(size: number): void {
  emit('size-change', size)
}

function prevPage(): void {
  if (props.result && props.result.page_index > 1) {
    emit('page-change', props.result.page_index - 1)
  }
}

function nextPage(): void {
  if (props.result && canNext.value) {
    emit('page-change', props.result.page_index + 1)
  }
}
</script>

<template>
  <div class="result-panel">
    <el-alert
      v-if="errorMessage"
      :title="errorMessage"
      type="error"
      show-icon
      :closable="false"
    />

    <div v-if="result" class="result-stats">
      <template v-if="result.total !== null">
        共 <strong>{{ result.total }}</strong> 条 · 第 {{ result.page_index }} /
        {{ Math.max(result.total_pages, 1) }} 页
      </template>
      <template v-else>
        未统计总数 · 第 {{ result.page_index }} 页（本页 {{ result.items.length }} 条）
      </template>
    </div>

    <el-table
      v-loading="loading"
      :data="rows"
      border
      stripe
      size="small"
      max-height="520"
      :empty-text="emptyText"
    >
      <el-table-column
        v-for="column in columns"
        :key="column"
        :prop="column"
        :label="column"
        min-width="130"
        show-overflow-tooltip
      >
        <template #default="{ row }">{{ cell(row, column) }}</template>
      </el-table-column>
    </el-table>

    <div v-if="result" class="result-pager">
      <el-pagination
        v-if="result.total !== null"
        background
        layout="total, sizes, prev, pager, next, jumper"
        :total="result.total"
        :current-page="result.page_index"
        :page-size="result.page_size"
        :page-sizes="[10, 20, 50, 100, 200]"
        @current-change="onCurrentChange"
        @size-change="onSizeChange"
      />

      <div v-else class="simple-pager">
        <el-button size="small" :disabled="result.page_index <= 1" @click="prevPage">
          上一页
        </el-button>
        <span class="simple-page-no">第 {{ result.page_index }} 页</span>
        <el-button size="small" :disabled="!canNext" @click="nextPage">下一页</el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.result-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.result-stats {
  color: #606266;
  font-size: 12px;
}

.result-stats strong {
  color: #303133;
}

.result-pager {
  display: flex;
  justify-content: flex-end;
}

.simple-pager {
  display: flex;
  align-items: center;
  gap: 12px;
}

.simple-page-no {
  color: #606266;
  font-size: 12px;
}
</style>
