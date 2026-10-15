using Microsoft.AspNetCore.Mvc;
using VibeCodingApi.Api.Filters;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Repository.Extensions;
using VibeCodingApi.Repository.IRepository;
using VibeCodingApi.Repository.Repository;
using VibeCodingApi.Service;

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
