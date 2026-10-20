# BI 报表查询前端（Vue 3）

对接后端 `api/bi-report` 的即席查询前端：从元数据接口读取可查询的表与字段，
用户自由勾选字段、拼装关联 / 过滤 / 分组 / 排序，执行查询并导出 Excel。
服务端不保存任何报表定义，本前端也不保存——刷新页面即重置。

## 技术栈

- Vite 7 + Vue 3.5 + TypeScript 5.9
- Element Plus 2.11（全量引入，`zh-cn` 语言包）
- axios 1.8

无 vue-router（单页）、无 Pinia（状态集中在 `useQueryBuilder` 一个 composable）。

## 启动

### 1. 先启动后端

在 `BiReport/` 目录（本目录的上一级）执行：

```bash
dotnet run --project src/VibeCodingApi.Api
```

后端监听 `http://localhost:21022`（见 `Properties/launchSettings.json` 的 `http` profile）。
可直接访问 `http://localhost:21022/swagger` 确认。

### 2. 启动前端

```bash
cd vue
npm install
npm run dev
```

打开 `http://localhost:5173`。

开发期由 Vite 代理把 `/api` 转发到 `http://localhost:21022`（见 `vite.config.ts`），
**不需要后端配置 CORS**。如果后端端口变了，同步改 `vite.config.ts` 里的 `target`。

其他脚本：

```bash
npm run type-check   # vue-tsc 类型检查
npm run build        # 类型检查 + 生产构建，产物在 dist/
npm run preview      # 本地预览 dist/
```

## 生产部署

`VITE_API_BASE` 默认是 `/api`，即前端假定**与后端同源**。Vite 的代理只在开发期生效，
所以生产环境必须满足下面任一条，否则浏览器会因跨域被拦截：

1. **同源反向代理（推荐）**：用 Nginx 等把 `dist/` 作为静态站点，并把 `/api/` 反代到后端。

   ```nginx
   server {
       listen 80;
       root /var/www/bi-report;   # dist/ 的内容

       location / {
           try_files $uri $uri/ /index.html;
       }

       location /api/ {
           proxy_pass http://127.0.0.1:21022;   # 注意保留 /api 前缀
           proxy_set_header Host $host;
           proxy_read_timeout 120s;             # 导出大报表可能较慢
       }
   }
   ```

2. **给后端开 CORS**，并在构建时把 `VITE_API_BASE` 设为后端的完整地址：

   ```bash
   VITE_API_BASE=http://api.example.com/api npm run build
   ```

后端连接串当前使用 `root`，生产部署建议给 BI 查询单独配只读账号
（元数据与查询都只做 `SELECT`）。

## 使用流程

1. 页面加载时自动请求 `GET /api/bi-report/metadata`，左侧列出白名单内的表与字段。
   元数据有 TTL 缓存；改过表结构后点工具栏「刷新元数据」。
2. 在左侧勾选字段 → 字段进入右侧「输出字段」，所在表自动加入查询。
   也可以在表标题上点「加入查询」先加表（桥接表可能没有输出字段，只参与关联）。
3. 多表时在「关联条件」里填写两端字段。右侧候选只列出与左侧字段**同族**
   （数值 / 日期 / 字符串）的列。后端会按主表（第一张表）把关联重排成树，
   `LEFT` 表示保留主表一侧的全部行。
4. 「输出字段」里给每个字段选聚合（`sum` / `avg` 只对数值列开放）、改别名、选排序。
   别名就是结果列名与 Excel 表头。
5. 需要时添加过滤条件；`IS NULL` / `IS NOT NULL` 不需要填值。
6. 选好分页参数后点「查询」。`need_total` 关闭时后端不统计总数，
   结果区只提供上一页 / 下一页。
7. 「请求预览」可以看到即将发送的 JSON（与后端 DTO 逐字段一致）。
8. 「导出 Excel」按当前条件导出（上限见下），导出期间条件被改过会先确认。

## 与后端的契约要点

- 路由：`GET /metadata`、`POST /metadata/refresh`、`POST /query`、`POST /export`，
  前缀均为 `/api/bi-report`。
- 请求与响应字段**一律 snake_case**（`page_index`、`join_type`、`group_by` …），
  与后端 DTO 保持一致。
- 统一返回 `{ code, message, data }`，`code === 0` 为成功；HTTP 400 的参数错误
  同样带 `message`。
- 导出的成功响应是 xlsx 二进制（`Content-Type` 为表格类型），失败时返回的仍是
  JSON 的 `ApiResult`——前端按 `Content-Type` 区分。
- 前端校验只是提交前的 UX 提示，**服务端 `BiReportSqlBuilder` 仍是唯一权威校验**。

## 限额（与后端 `appsettings.json` 的 `BiQuery` 节保持一致）

| 项 | 值 |
| --- | --- |
| 每页条数 | 1 – 200（默认 20） |
| 输出字段 | ≤ 100 |
| 关联表 | ≤ 8，且 `joins` 数 = 表数 − 1 |
| 过滤条件 | ≤ 30 |
| `IN` 列表 | ≤ 1000 项 |
| 排序字段 | ≤ 5 |
| `LIKE` 模式 | ≤ 500 字符 |
| 最大偏移 | 1000 万 |
| 导出 | ≤ 20000 行 且 ≤ 50 万单元格 |

修改后端配置后，记得同步 `src/config/limits.ts`，否则会出现前端放行、后端拒绝的情况。

## 已知限制

- 表别名固定等于表名，**不支持自关联**。
- 只能按已选输出字段排序，最多 5 个。
- 不支持自由表达式列（OR / HAVING / DISTINCT / 日期函数 / 跨库表），
  与后端能力一致；需要扩展时由后端增加枚举型参数。
- 不保存查询定义，刷新页面即丢失。
- 浏览器用 IEEE 754 双精度解析 JSON，超过 `2^53` 的 `bigint` / 大 `decimal`
  会丢精度。本前端不引入 `json-bigint`；导出走服务端，不受影响。
- 全量引入 Element Plus，首屏 JS 约 900KB（gzip 后约 300KB）。
  在意体积可改用按需引入插件。

## 目录结构

```
src/
├── api/            # axios 实例与 4 个接口封装
├── components/     # SchemaPanel / FieldEditor / JoinEditor / FilterEditor /
│                   # GroupEditor / ResultPanel / RequestPreview
├── composables/    # useQueryBuilder：状态机、请求构建、提交前校验
├── config/         # limits.ts：镜像后端的限额常量
├── types/          # 与后端 DTO 对齐的 snake_case 类型
├── utils/          # 标识符守卫、值转换、格式化、下载
├── App.vue         # 布局与页面编排
├── main.ts         # Element Plus + zh-cn
└── styles/         # 全局样式
```
