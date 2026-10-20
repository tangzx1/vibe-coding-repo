/**
 * 解析 Content-Disposition 里的文件名。
 *
 * ASP.NET Core 对中文文件名会按 RFC 6266 输出
 * `attachment; filename=...; filename*=UTF-8''BI%E6%8A%A5%E8%A1%A8_....xlsx`，
 * 优先取 `filename*`；没有时退回普通 `filename`；都没有就用调用方给的兜底名。
 */
export function parseFileName(contentDisposition: string | undefined, fallback: string): string {
  if (!contentDisposition) return fallback

  const encoded = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(contentDisposition)
  if (encoded?.[1]) {
    const raw = encoded[1].trim().replace(/^"|"$/g, '')
    try {
      return decodeURIComponent(raw)
    } catch {
      return raw
    }
  }

  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(contentDisposition)
  if (plain?.[1]) {
    const raw = plain[1].trim()
    try {
      return decodeURIComponent(raw)
    } catch {
      return raw
    }
  }

  return fallback
}

/** 把后端返回的二进制内容触发为浏览器下载 */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.style.display = 'none'
  document.body.appendChild(anchor)
  anchor.click()
  document.body.removeChild(anchor)
  // 延迟回收：部分浏览器在 click() 返回后才会真正开始读取 blob
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
