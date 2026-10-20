import axios from 'axios'
import { http, unwrap } from './http'
import { parseFileName } from '../utils/download'
import type {
  ApiResult,
  BiReportQueryDto,
  BiReportResult,
  BiReportSchema
} from '../types/biReport'

/** 取可查询元数据：有哪些表、每张表有哪些字段（不含表间关联关系） */
export async function getMetadata(): Promise<BiReportSchema> {
  const response = await http.get<ApiResult<BiReportSchema>>('bi-report/metadata')
  return unwrap(response.data)
}

/** 强制刷新元数据缓存（DDL 变更后用，不必等缓存过期） */
export async function refreshMetadata(): Promise<BiReportSchema> {
  const response = await http.post<ApiResult<BiReportSchema>>('bi-report/metadata/refresh')
  return unwrap(response.data)
}

/** 执行即席查询，分页返回 */
export async function runQuery(query: BiReportQueryDto): Promise<BiReportResult> {
  const response = await http.post<ApiResult<BiReportResult>>('bi-report/query', query)
  return unwrap(response.data)
}

/** 读取错误响应里的提示。导出接口失败时返回的仍是 JSON，但响应类型是 blob */
async function readBlobMessage(blob: Blob): Promise<string> {
  const text = (await blob.text()).trim()
  if (!text) return '导出失败：后端没有返回文件内容'

  try {
    const parsed = JSON.parse(text) as { message?: unknown }
    if (parsed && typeof parsed.message === 'string' && parsed.message.trim()) {
      return parsed.message
    }
  } catch {
    // 不是 JSON，按纯文本处理
  }

  return text.length > 300 ? `${text.slice(0, 300)}…` : text
}

function fallbackExportFileName(): string {
  const now = new Date()
  const pad = (value: number) => String(value).padStart(2, '0')
  return `BI报表_${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}` +
    `${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}.xlsx`
}

/**
 * 导出 xlsx。
 *
 * 成功时后端返回的是文件流而不是 ApiResult；失败时仍是 JSON（HTTP 400/500）。
 * 因此必须先看响应的 Content-Type 再决定怎么解析，不能无脑当 JSON 读。
 */
export async function exportXlsx(query: BiReportQueryDto): Promise<{ blob: Blob; fileName: string }> {
  try {
    const response = await http.post<Blob>('bi-report/export', query, { responseType: 'blob' })
    const contentType = String(response.headers['content-type'] ?? '')

    if (contentType.includes('json') || contentType.includes('text/')) {
      throw new Error(await readBlobMessage(response.data))
    }

    return {
      blob: response.data,
      fileName: parseFileName(
        String(response.headers['content-disposition'] ?? ''),
        fallbackExportFileName()
      )
    }
  } catch (error) {
    // axios 对非 2xx 的 blob 响应不会帮我们解析 JSON，这里补上
    if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
      throw new Error(await readBlobMessage(error.response.data))
    }
    throw error
  }
}
