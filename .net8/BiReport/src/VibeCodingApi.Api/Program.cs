using Microsoft.AspNetCore.Mvc;
using VibeCodingApi.Api.Filters;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Repository.BiReport;
using VibeCodingApi.Repository.Extensions;
using VibeCodingApi.Repository.IRepository;
using VibeCodingApi.Repository.Repository;
using VibeCodingApi.Service;
using VibeCodingApi.Service.BiReport;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// SqlSugar：数据访问层（连接见 appsettings.json 的 DbConnections 数组，ConnId = WMMETAMINE_MYSQL / WMMETAMINE_MYSQL_2）
builder.Services.AddSqlSugar(builder.Configuration);

// 仓储与业务服务。
// 底层注册的是线程安全的单例 SqlSugarScope，这里用 Scoped 是为了让每个请求拿到独立的仓储/服务实例，
// 便于将来在服务里按请求持有状态（如当前用户），也符合 ASP.NET Core 的默认生命周期习惯。
// 本项目没有引入自动扫描组件（Autofac/Scrutor），新增服务需要在这里手动登记。
builder.Services.AddScoped<ITruckRepository, TruckRepository>();
builder.Services.AddScoped<ITruckService, TruckService>();
builder.Services.AddScoped<ITruckLiftRecordRepository, TruckLiftRecordRepository>();
builder.Services.AddScoped<ITruckLiftRecordService, TruckLiftRecordService>();

// BI 报表即席查询。配置在注册期解析并校验：白名单为空、表名为空、表名重复、表名含非法字符，
// 都会在启动时直接抛异常，不等某个请求打进来才发现。
// 注意配置里只有「哪些表可查」，没有关联关系——关联条件由前端随每次查询提交。
builder.Services.AddBiReport(builder.Configuration);
// 元数据缓存跨请求共享，必须是单例——注册成 Scoped 的话每个请求都会重新打一次 information_schema，
// 缓存等于没做
builder.Services.AddSingleton<BiReportSchemaCache>();
builder.Services.AddScoped<IBiReportRepository, BiReportRepository>();
builder.Services.AddScoped<BiExcelExporter>();
builder.Services.AddScoped<IBiReportService, BiReportService>();

builder.Services
    .AddControllers(options =>
    {
        // 全局异常过滤器：让异常响应也是统一的 ApiResult 结构（业务异常 → 400，其他 → 500）
        options.Filters.Add<ApiExceptionFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // 模型校验（DataAnnotations）失败时，默认返回的是 ValidationProblemDetails，
        // 结构和其他接口不一致，这里统一改写成 ApiResult。
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = string.Join("；", context.ModelState.Values
                .SelectMany(state => state.Errors)
                .Select(error => error.ErrorMessage)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

            return new BadRequestObjectResult(ApiResult.Fail(
                string.IsNullOrWhiteSpace(message) ? "请求参数不合法" : message,
                StatusCodes.Status400BadRequest));
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
