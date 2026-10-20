import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import App from './App.vue'
import './styles/index.css'

const app = createApp(App)

// 单页应用，全量引入 Element Plus：少一层构建配置，代价是包体稍大。
// 如果要按需引入，再引入 unplugin-vue-components / unplugin-auto-import。
app.use(ElementPlus, { locale: zhCn })

app.mount('#app')
