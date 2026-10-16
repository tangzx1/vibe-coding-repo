using VibeCodingApi.Model.Dtos;

namespace VibeCodingApi.Repository.IRepository;

/// <summary>
/// BI 报表即席查询的数据访问接口。
/// <para>
/// 只读：本模块不落库报表定义、不写任何业务表，所以没有增删改方法。
/// </para>
/// </summary>
public interface IBiReportRepository
{
    /// <summary>
    /// 取可查询元数据（表白名单 ∩ 数据库实际结构，含敏感列剔除），带缓存。
    /// </summary>
    /// <param name="forceRefresh">true 表示忽略缓存有效期强制重新读取（DDL 变更后用）。</param>
    Task<BiReportSchemaDto> GetSchemaAsync(bool forceRefresh = false);

    /// <summary>
    /// 执行即席查询并按 <see cref="BiReportQueryDto.page_index"/> / <see cref="BiReportQueryDto.page_size"/> 分页返回。
    /// </summary>
    /// <param name="query">前端提交的完整查询定义。</param>
    Task<BiReportResultDto> QueryAsync(BiReportQueryDto query);

    /// <summary>
    /// 按 <paramref name="maxRows"/> 上限取导出用的数据（不分页）。
    /// <para>
    /// 返回值里最多有 <c>maxRows + 1</c> 行：多出的那一行是「结果超限」的信号，
    /// 是否报错还是截断由业务层决定，数据访问层不做这个判断。
    /// </para>
    /// </summary>
    /// <param name="query">前端提交的完整查询定义（忽略其分页参数）。</param>
    /// <param name="maxRows">最多取多少行。</param>
    Task<BiReportExportDataDto> GetExportDataAsync(BiReportQueryDto query, int maxRows);
}
