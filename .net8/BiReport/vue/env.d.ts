/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 接口基地址。开发期用 /api（由 vite proxy 转发到后端），生产期走同源反向代理。 */
  readonly VITE_API_BASE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
