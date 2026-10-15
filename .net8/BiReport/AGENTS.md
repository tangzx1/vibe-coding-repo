# AGENTS.md

本文件定义本仓库中 AI 编码助手（Claude Code、Codex、Cursor、Gemini CLI 等）
需要遵循的项目结构约定与编码规范。所有自动化改动都必须遵守本文件。


## 1. 项目概览

- 技术栈：.NET 8 + ASP.NET Core + SqlSugar
- 日志：Serilog
- 依赖管理：NuGet（使用 `Directory.Packages.props` 集中管理版本）

## 2. 解决方案结构

解决方案文件：`VibeCodingApi.sln`（位于本目录）。项目分三层：表现层（Api）、业务层（Service / IService）、
数据与模型层（Repository / Model / Common）。**依赖方向单向向下，禁止反向引用或循环引用。**

### 2.1 项目与依赖关系

```
VibeCodingApi.Api ──────────────┬──> VibeCodingApi.Service ──┬──> VibeCodingApi.IService ──┬──> VibeCodingApi.Model
   （表现层）                    │      （业务实现）           │      （业务接口）            └──> VibeCodingApi.Common
                                │                             ├──> VibeCodingApi.Repository ─┬──> VibeCodingApi.Model
                                │                             │      （数据访问）            └──> VibeCodingApi.Common
                                │                             ├──> VibeCodingApi.Model
                                │                             └──> VibeCodingApi.Common
                                └──> VibeCodingApi.Common

tests/VibeCodingApi.Tests ──> VibeCodingApi.Api
```

| 项目 | 职责 | 允许引用 |
| --- | --- | --- |
| `src/VibeCodingApi.Api` | HTTP 入口：Controller、全局过滤器、DI 注册与中间件管道 | IService、Service、Common |
| `src/VibeCodingApi.Service` | 业务实现：编排仓储、DTO ↔ 实体映射、业务规则校验 | IService、Repository、Model、Common |
| `src/VibeCodingApi.IService` | 业务接口定义（只有接口，没有实现） | Model、Common |
| `src/VibeCodingApi.Repository` | 数据访问：SqlSugar 注册、泛型仓储、实体专属仓储 | Model、Common |
| `src/VibeCodingApi.Model` | 数据模型：数据库实体与接口 DTO | 无 |
| `src/VibeCodingApi.Common` | 跨层公共类型：`ApiResult`、`PagedResult`、`BusinessException` | 无 |
| `tests/VibeCodingApi.Tests` | xunit 单元测试 | Api |

> ⚠ `VibeCodingApi.IService` **不引用 Repository**，所以业务接口里不能出现仓储类型。
> 业务接口需要返回分页结果时应使用 `Common` 里的 `PagedResult<T>`。

### 2.2 目录约定

新增同类文件必须放进对应目录，不要平铺在项目根下：

```
src/VibeCodingApi.Api/
├── Controllers/          控制器，路由统一为 api/{资源名}
├── Filters/              全局过滤器（异常、模型校验）
└── Program.cs            DI 注册 + 中间件管道

src/VibeCodingApi.Common/
├── Exceptions/           业务异常（BusinessException）
└── Models/               统一返回模型（ApiResult、PagedResult）

src/VibeCodingApi.IService/
└── IXxxService.cs        业务接口，直接放项目根

src/VibeCodingApi.Model/
├── Entities/             SqlSugar 实体，与数据库表一一对应
└── Dtos/                 接口入参 DTO，按资源分子目录（如 Dtos/Truck/）

src/VibeCodingApi.Repository/
├── Extensions/           SqlSugar 注册扩展（AddSqlSugar、SqlSugarConfigIds）
├── Options/              配置项模型（DbConnectionOption）
├── IRepository/          仓储接口（IBaseRepository<T>、IXxxRepository）
└── Repository/           仓储实现（BaseRepository<T>、XxxRepository）

src/VibeCodingApi.Service/
├── Mappers/              DTO ↔ 实体映射（手写，不使用 AutoMapper）
└── XxxService.cs         业务实现，直接放项目根
```

### 2.3 一个资源的完整链路（以 truck 为例）

新增一张表的分页 CRUD，按下列顺序落地，共 7 处：

| 步骤 | 落点 | 示例 |
| --- | --- | --- |
| 1 | `Model/Entities/` | `Truck.cs`（表已存在时，实体只做映射，不建表） |
| 2 | `Model/Dtos/{资源}/` | `TruckCreateDto.cs`、`TruckUpdateDto.cs`、`TruckQueryDto.cs` |
| 3 | `Repository/IRepository/` + `Repository/Repository/` | `ITruckRepository.cs`、`TruckRepository.cs` |
| 4 | `IService/` | `ITruckService.cs` |
| 5 | `Service/`（含 `Mappers/`） | `TruckService.cs`、`Mappers/TruckMapper.cs` |
| 6 | `Api/Controllers/` | `TruckController.cs` |
| 7 | `Api/Program.cs` | 手工注册 `AddScoped<ITruckRepository, TruckRepository>()` 等 |

> ⚠ 本项目**没有引入自动扫描组件**（无 Autofac / Scrutor），第 7 步必须手工登记，
> 漏了会在运行时抛「无法解析服务」，编译期不报错。

### 2.4 分层职责边界

- **Controller 不写业务逻辑**：只做参数接收、调用 Service、把结果包成 `ApiResult` 并映射 HTTP 状态码
- **Service 不用 SqlSugar**：数据访问一律通过仓储接口；业务规则（如唯一性校验、服务端字段赋值）放这一层
- **Repository 不写业务规则**：只负责查询构造与持久化，查询条件的拼装（`WhereIF`）在这一层
- **Model / Common 不引用任何业务项目**，是纯粹的类型定义
