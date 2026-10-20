import axios from 'axios'
import type { ApiResult } from '../types/biReport'

/**
 * 统一的 axios 实例。
 *
 * 开发期 `VITE_API_BASE = /api`，由 vite proxy 转发到 http://localhost:21022，
 * 浏览器看到的是同源请求——后端当前没有配置 CORS，直连会被浏览器拦下。
 */
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE ?? '/api',
  // 后端单条查询有 QueryTimeoutMs（默认 30s），导出会跑一次不分页查询；
  // 这里留足时间，真正的上限仍由服务端控制
  timeout: 120_000
})

/** 解包 ApiResult：`code !== 0` 视为业务失败 */
export function unwrap<T>(result: ApiResult<T>): T {
  if (!result || typeof result.code !== 'number') {
    throw new Error('接口返回格式不正确')
  }
  if (result.code !== 0) {
    throw new Error(result.message || '请求失败')
  }
  return result.data as T
}

function pickMessage(data: unknown): string | null {
  if (typeof data === 'string') {
    const text = data.trim()
    // HTML 错误页不值得原样展示给用户
    if (text && text.length <= 300 && !text.startsWith('<')) return text
    return null
  }

  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>
    if (typeof record.message === 'string' && record.message.trim()) return record.message
    if (typeof record.title === 'string' && record.title.trim()) return record.title
  }

  return null
}

/** 把任意异常转成可展示的中文提示 */
export function extractMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const message = pickMessage(error.response?.data)
    if (message) return message

    if (error.code === 'ECONNABORTED') return '请求超时，请稍后重试'
    if (!error.response) return '无法连接后端服务，请确认后端已启动且接口地址正确'
    return `请求失败（HTTP ${error.response.status}）`
  }

  if (error instanceof Error) return error.message
  return String(error)
}
