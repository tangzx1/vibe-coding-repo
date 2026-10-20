import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    // Element Plus 全量引入，单块约 900KB；gzip 后约 300KB，属预期范围
    chunkSizeWarningLimit: 1000,
    rollupOptions: {
      output: {
        // Element Plus 全量引入后体积较大，单独分包：业务代码改动时不必让用户重新下载 UI 库
        manualChunks: {
          vue: ['vue'],
          'element-plus': ['element-plus', '@element-plus/icons-vue']
        }
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      // 后端没有配置 CORS，开发期一律走代理，浏览器看到的是同源请求。
      // 不做 rewrite：后端路由本身就是 /api/bi-report/...
      '/api': {
        target: 'http://localhost:21022',
        changeOrigin: true
      }
    }
  }
})
