using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.Common.Models;

namespace VibeCodingApi.Api.Filters;

/// <summary>
/// 全局异常过滤器：把异常翻译成统一的 <see cref="ApiResult"/> 响应。
/// <para>
/// 没有它的话，未捕获异常会由 ASP.NET Core 返回 ProblemDetails，
/// 和正常返回的 ApiResult 结构不一致，前端要写两套解析逻辑。
/// </para>
/// <para>
/// 映射规则：
/// <list type="bullet">
/// <item>业务异常 → HTTP 400（请求没问题，是业务规则不允许）</item>
/// <item>其他异常 → HTTP 500，且不外泄异常细节，避免把表结构、SQL 等信息暴露给调用方</item>
/// </list>
/// </para>
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    /// <summary>构造函数。</summary>
    /// <param name="logger">日志组件。</param>
    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BusinessException businessException)
        {
            // 业务异常属于预期内的失败，记 Warning 即可；记 Error 会让真正的故障被淹没
            _logger.LogWarning(businessException, "业务异常：{Message}", businessException.Message);

            context.Result = new BadRequestObjectResult(
                ApiResult.Fail(businessException.Message, businessException.Code));
        }
        else
        {
            _logger.LogError(
                context.Exception,
                "未处理异常：{Method} {Path}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            context.Result = new ObjectResult(ApiResult.Fail("服务器内部错误"))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        // 标记已处理，阻止异常继续向上冒泡成默认的错误响应
        context.ExceptionHandled = true;
    }
}
