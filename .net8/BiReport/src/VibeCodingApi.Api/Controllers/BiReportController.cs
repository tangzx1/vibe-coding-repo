using Microsoft.AspNetCore.Mvc;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;

namespace VibeCodingApi.Api.Controllers;

/// <summary>
/// BI 报表即席查询接口：先取元数据给前端画查询设计器，再执行用户拼好的关联查询。
/// <para>
/// 本模块<b>只做即席查询</b>：每次查询都是一份完整自描述请求，服务端不保存任何报表定义，
/// 也不新增任何数据库表。
/// </para>
/// <para>
/// 可查询的表由配置白名单决定（appsettings.json 的 <c>BiQuery.Tables</c>），
/// 字段名、类型、注释从数据库的 <c>INFORMATION_SCHEMA</c> 自动读取。
/// 敏感列（如 <c>cors_password</c>）在元数据阶段就被剔除，前端根本看不到它们。
/// </para>
/// </summary>
[ApiController]
[Route("api/bi-report")]
public class BiReportController : ControllerBase
{
    /// <summary>导出文件的 MIME 类型。</summary>
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IBiReportService _biReportService;

    /// <summary>构造函数。</summary>
    /// <param name="biReportService">BI 报表业务服务。</param>
    public BiReportController(IBiReportService biReportService)
    {
        _biReportService = biReportService;
    }

    /// <summary>
    /// 取可查询元数据：有哪些表、每张表有哪些字段。
    /// <para>
    /// <b>不含关联关系</b>：表和表怎么关联由用户在 /query 的 joins 里现场指定，服务端不预置也不推荐。
    /// 前端画关联连线时，候选字段就是这里返回的字段，同族的两个字段才允许互连。
    /// </para>
    /// <para>
    /// 一个接口整包返回（而不是拆成表 / 字段两个）：白名单当前只有两张表、一百多列，
    /// 拆开会让前端要合并两次响应、任一次失败就拿不到完整模型。
    /// </para>
    /// </summary>
    [HttpGet("metadata")]
    public async Task<ActionResult<ApiResult<BiReportSchemaDto>>> GetMetadata()
    {
        var schema = await _biReportService.GetSchemaAsync();
        return Ok(ApiResult<BiReportSchemaDto>.Ok(schema));
    }

    /// <summary>
    /// 强制刷新元数据缓存（DDL 变更后用，不必等缓存自然过期）。
    /// 用 POST 而不是 GET：它有副作用（打一次 information_schema），不该被当成可缓存的读操作。
    /// </summary>
    [HttpPost("metadata/refresh")]
    public async Task<ActionResult<ApiResult<BiReportSchemaDto>>> RefreshMetadata()
    {
        var schema = await _biReportService.GetSchemaAsync(forceRefresh: true);
        return Ok(ApiResult<BiReportSchemaDto>.Ok(schema));
    }

    /// <summary>
    /// 执行即席查询，分页返回。
    /// <para>
    /// 请求体就是完整查询定义：选表、关联、输出列（可带别名）、过滤、分组、排序、分页。
    /// 具体字段见 <see cref="BiReportQueryDto"/>。
    /// </para>
    /// </summary>
    /// <param name="query">查询定义。</param>
    [HttpPost("query")]
    public async Task<ActionResult<ApiResult<BiReportResultDto>>> Query([FromBody] BiReportQueryDto query)
    {
        var result = await _biReportService.QueryAsync(query);
        return Ok(ApiResult<BiReportResultDto>.Ok(result));
    }

    /// <summary>
    /// 执行查询并导出 xlsx。
    /// <para>
    /// <b>成功时返回的是文件流，不是 ApiResult</b>（HTTP 200 + xlsx 二进制）；
    /// 失败时仍由全局异常过滤器返回 ApiResult 的 JSON（HTTP 400 或 500）。
    /// 前端必须<b>先看响应的 Content-Type 再决定怎么解析</b>，不能无脑 <c>response.json()</c>。
    /// </para>
    /// <para>
    /// 请求体与 <c>POST /api/bi-report/query</c> 完全相同，但 <c>page_index</c> / <c>page_size</c>
    /// 会被忽略——导出取的是全部结果，上限由配置的 <c>ExportMaxRows</c> 决定。
    /// </para>
    /// </summary>
    /// <param name="query">查询定义。</param>
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] BiReportQueryDto query)
    {
        var content = await _biReportService.ExportAsync(query);

        // 文件名带时间戳，避免用户连着导几次后在下载目录里分不清哪个是哪个。
        // 中文文件名由框架按 RFC 6266 编码成 filename*=UTF-8''…，不需要手工转义
        var fileName = $"BI报表_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return File(content, XlsxContentType, fileName);
    }
}
