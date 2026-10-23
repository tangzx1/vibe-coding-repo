<script setup lang="ts">
import type { GroupMode } from '../types/biReport'

interface GroupOption {
  key: string
  label: string
}

const props = defineProps<{
  mode: GroupMode
  manualFields: string[]
  fieldOptions: GroupOption[]
  hasAggregate: boolean
}>()

const emit = defineEmits<{
  (e: 'update:mode', mode: GroupMode): void
  (e: 'update:manual', keys: string[]): void
}>()

function onMode(value: unknown): void {
  if (value === 'auto' || value === 'manual') {
    emit('update:mode', value)
  }
}

function toggle(key: string): void {
  if (props.manualFields.includes(key)) {
    emit('update:manual', props.manualFields.filter(item => item !== key))
  } else {
    emit('update:manual', [...props.manualFields, key])
  }
}
</script>

<template>
  <div class="group-editor">
    <div class="editor-hint">
      只有使用聚合函数时分组才起作用。聚合查询里所有未聚合的输出字段都必须出现在分组字段中，
      否则 MySQL 的 ONLY_FULL_GROUP_BY 会直接拒绝。
    </div>

    <el-radio-group
      size="small"
      :model-value="mode"
      @update:model-value="onMode"
    >
      <el-radio-button value="auto">自动（全部非聚合字段）</el-radio-button>
      <el-radio-button value="manual">手动选择</el-radio-button>
    </el-radio-group>

    <div v-if="mode === 'auto'" class="group-content">
      <template v-if="hasAggregate && fieldOptions.length > 0">
        <el-tag
          v-for="option in fieldOptions"
          :key="option.key"
          size="small"
          type="info"
          effect="plain"
        >
          {{ option.label }}
        </el-tag>
      </template>
      <span v-else-if="hasAggregate" class="group-empty">
        没有非聚合字段，本次聚合查询不分组，只返回一行汇总结果。
      </span>
      <span v-else class="group-empty">未使用聚合函数，本次查询不分组。</span>
    </div>

    <div v-else class="group-content">
      <el-checkbox
        v-for="option in fieldOptions"
        :key="option.key"
        :model-value="manualFields.includes(option.key)"
        @change="toggle(option.key)"
      >
        {{ option.label }}
      </el-checkbox>
      <span v-if="fieldOptions.length === 0" class="group-empty">
        没有可参与分组的非聚合字段。
      </span>
    </div>
  </div>
</template>

<style scoped>
.group-editor {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.editor-hint {
  color: var(--app-text-faint);
  font-size: 12px;
  line-height: 1.7;
}

.group-content {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 24px;
}

.group-empty {
  color: var(--app-text-muted);
  font-size: 12px;
}
</style>
