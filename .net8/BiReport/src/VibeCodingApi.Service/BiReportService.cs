using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Repository.IRepository;
using VibeCodingApi.Repository.Options;
using VibeCodingApi.Service.BiReport;

namespace VibeCodingApi.Service;

/// <summary>
/// BI 报表即席查询业务实现。
/// <para>
/// 保持薄转发：查询定义的语义校验（白名单、列归属、join 连通性、分组合法性、运算符与值形状）
/// 全部在 <c>BiReportSqlBuilder</c> 里做一次——那是唯一生成 SQL 的地方，也是权威出口。
/// 在这里再校验一遍只会得到两份会各自漂移的规则，而且拦不住任何它拦得住的东西。
/// </para>
/// <para>
/// 这一层只负责「写 SQL 之外的事」：模块开关、导出的行数与单元格上限、调用 Excel 生成器。
/// </para>
/// </summary>
public class BiReportService : IBiReportService
{
    private readonly IBiReportRepository _repository;
    private readonly BiReportOptions _options;
    private readonly BiExcelExporter _excelExporter;

    /// <summary>构造函数。</summary>
    /// <param name="repository">BI 报表仓储。</param>
    /// <param name="options">BI 报表配置。</param>
    /// <param name="excelExporter">Excel 生成器。</param>
    public BiReportService(
        IBiReportRepository repository,
        BiReportOptions options,
        BiExcelExporter excelExporter)
    {
        _repository = repository;
        _options = options;
        _excelExporter = excelExporter;
    }

    /// <inheritdoc />
    public Task<BiReportSchemaDto> GetSchemaAsync(bool forceRefresh = false)
    {
        EnsureEnabled();

        // 刷新接口在无鉴权部署下等于「任何人都能让服务端去打一次 information_schema」，
        // 所以给它一个独立开关；关掉后仍按 TTL 自然过期
        if (forceRefresh && !_options.MetadataRefreshEnabled)
        {
            throw new BusinessException("元数据刷新接口已关闭，请等待缓存自动过期。");
        }

        return _repository.GetSchemaAsync(forceRefresh);
    }

    /// <inheritdoc />
    public Task<BiReportResultDto> QueryAsync(BiReportQueryDto query)
    {
        EnsureEnabled();
        // 查询语句的编译过程本身会再检查一次开关，这里提前拦是为了让「关了模块」这件事
        // 在任何一条路径上都有同一句错误提示
        return _repository.QueryAsync(query);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportAsync(BiReportQueryDto query)
    {
        EnsureEnabled();

        var maxRows = _options.ExportMaxRows;
        if (maxRows < 1)
        {
            // 配置错误而不是用户输入错误，抛非业务异常走 500：这是运维要改配置，不是调用方改请求
            throw new InvalidOperationException(
                $"BiQuery.ExportMaxRows 配置为 {maxRows}，导出功能无法使用，请检查 appsettings.json。");
        }

        var data = await _repository.GetExportDataAsync(query, maxRows);

        // 仓储按「上限 + 1」取数，多出来的那一行就是超限信号——比先 count 再取数少一次查询，
        // 也避免了两次查询之间数据变动导致 count 与取数结果对不上
        var exceeded = data.items.Count > maxRows;
        if (exceeded && !_options.ExportTruncateWhenExceed)
        {
            throw new BusinessException(
                $"查询结果超过 {maxRows} 行，无法一次导出，请增加筛选条件后重试。");
        }

        var rows = exceeded ? data.items.Take(maxRows).ToList() : data.items;

        // 第二道上限：单看行数或列数都不觉得危险，行 × 列才会把内存打爆
        var cellCount = (long)rows.Count * data.columns.Count;
        if (cellCount > _options.MaxExportCells)
        {
            throw new BusinessException(
                $"导出数据量过大（{rows.Count} 行 × {data.columns.Count} 列 = {cellCount} 个单元格，" +
                $"上限 {_options.MaxExportCells}），请增加筛选条件或减少输出字段。");
        }

        var note = exceeded ? $"查询结果超过 {maxRows} 行，本次导出已截断为前 {maxRows} 行。" : null;
        return _excelExporter.Build(data.columns, rows, note);
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new BusinessException("BI 报表查询模块已关闭。");
        }
    }
}
