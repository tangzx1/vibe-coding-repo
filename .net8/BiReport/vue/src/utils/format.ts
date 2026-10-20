/** null / undefined 统一显示为占位符，和 0、false、空字符串区分开 */
export function isNil(value: unknown): value is null | undefined {
  return value === null || value === undefined
}

/** ISO 日期时间里的 T 换成空格，方便阅读；其它字符串原样返回 */
function formatDateTimeText(text: string): string {
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/.test(text) ? text.replace('T', ' ') : text
}

/**
 * 单元格展示：null / undefined → `—`，其余转成字符串。
 *
 * 刻意不做千分位、toFixed 之类的格式化：BI 结果里 `60.050396` 这类精度是数据本身，
 * 前端擅自裁剪会让人对不上数据库。需要格式化的场景应该由 SQL 侧处理。
 */
export function formatCell(value: unknown): string {
  if (isNil(value)) return '—'
  if (typeof value === 'string') return formatDateTimeText(value)
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return String(value)
  }

  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

/** 转义 HTML，避免把用户输入的别名等文本直接塞进 ElMessageBox 的 rich HTML */
export function escapeHtml(text: string): string {
  const map: Record<string, string> = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  }
  return text.replace(/[&<>"']/g, ch => map[ch] ?? ch)
}
