using VibeCodingApi.Model.Dtos;

namespace VibeCodingApi.IService;

/// <summary>
/// BI 报表即席查询业务接口。
/// <para>
/// 本模块<b>只做即席查询</b>：前端每次提交一份完整的查询定义，服务端不保存任何报表状态。
/// 因此这里没有「保存/读取报表」之类的接口。
/// </para>
/// </summary>
public interface IBiReportService
{
    /// <summary>
    /// 取可查询元数据：有哪些表、每张表有哪些字段。
    /// 前端据此渲染查询设计器（拖表、勾字段、连关联、起别名）。
    /// <para>
    /// 元数据里<b>不含关联关系</b>——表与表怎么关联由用户在查询请求里现场指定，服务端不预置。
    /// </para>
    /// </summary>
    /// <param name="forceRefresh">true 表示忽略缓存有效期强制重新读取，DDL 变更后用。</param>
    Task<BiReportSchemaDto> GetSchemaAsync(bool forceRefresh = false);

    /// <summary>执行即席查询，分页返回。</summary>
    /// <param name="query">前端提交的完整查询定义。</param>
    Task<BiReportResultDto> QueryAsync(BiReportQueryDto query);

    /// <summary>
    /// 执行查询并导出 Excel。
    /// <para>
    /// 返回文件字节，<b>文件名由调用方拼</b>——文件名里带时间戳，属于表现层关心的事，
    /// 业务层不该替它决定格式。
    /// </para>
    /// </summary>
    /// <param name="query">前端提交的完整查询定义（分页参数被忽略）。</param>
    Task<byte[]> ExportAsync(BiReportQueryDto query);
}
