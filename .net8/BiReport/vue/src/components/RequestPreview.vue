<script setup lang="ts">
import { computed } from 'vue'
import { ElMessage } from 'element-plus'

const visible = defineModel<boolean>({ required: true })

const props = defineProps<{
  json: string
}>()

const lineCount = computed(() => props.json.split('\n').length)

async function copy(): Promise<void> {
  try {
    await navigator.clipboard.writeText(props.json)
    ElMessage.success('请求 JSON 已复制到剪贴板')
  } catch {
    ElMessage.error('复制失败，请手动选择文本复制')
  }
}
</script>

<template>
  <el-drawer v-model="visible" title="请求预览" size="560px" direction="rtl">
    <div class="preview-meta">
      <div>
        即将发送到 <code>POST /api/bi-report/query</code>
        （导出使用同一份请求体，只忽略分页参数）。
      </div>
      <div class="preview-count">{{ lineCount }} 行</div>
    </div>
    <pre class="preview-json">{{ json }}</pre>

    <template #footer>
      <el-button @click="visible = false">关闭</el-button>
      <el-button type="primary" @click="copy">复制 JSON</el-button>
    </template>
  </el-drawer>
</template>

<style scoped>
.preview-meta {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
  color: var(--app-text-muted);
  font-size: 12px;
  line-height: 1.6;
}

.preview-meta code {
  padding: 1px 5px;
  border: 1px solid #262d36;
  border-radius: 2px;
  background: #0b0e12;
  color: var(--app-blue-soft);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.preview-count {
  flex: none;
  color: var(--app-text-faint);
}

.preview-json {
  margin: 0;
  padding: 14px;
  max-height: calc(100vh - 220px);
  overflow: auto;
  background: #0a0d11;
  border: 1px solid #262d36;
  border-radius: 3px;
  color: #c9d6e4;
  font-size: 12px;
  line-height: 1.65;
  white-space: pre;
}

.preview-json::selection {
  background: rgba(47, 124, 246, 0.35);
}
</style>
