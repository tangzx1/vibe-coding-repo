using System.Data;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SqlSugar;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Repository.BiReport;
using VibeCodingApi.Repository.IRepository;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Repository.Repository;

/// <summary>
/// BI 报表即席查询仓储实现：取元数据 → 编译 SQL → 执行 → 转成字典结果。
/// <para>
/// 与 <see cref="TruckLiftRecordRepository"/> 一样是只读仓储，因此不继承 <c>BaseRepository</c>
/// （那是给实体的增删改查用的，这里查的是用户临时拼出来的、没有对应实体的结果集）。
/// </para>
/// <para>
/// 本类<b>不做查询定义的校验</b>：校验全部在 <see cref="BiReportSqlBuilder"/> 里完成，
/// 那是唯一生成 SQL 的地方，也是权威出口。在这里再校验一遍只会产生两份会各自漂移的规则。
/// </para>
/// </summary>
public class BiReportRepository : IBiReportRepository
{
    private readonly ISqlSugarClient _db;
    private readonly BiReportSchemaCache _schemaCache;
    private readonly BiReportOptions _options;
    private readonly ILogger<BiReportRepository> _logger;

    /// <summary>构造函数。</summary>
    /// <param name="db">SqlSugar 客户端（根作用域 = 业务库 dispatch）。</param>
    /// <param name="schemaCache">元数据缓存。</param>
    /// <param name="options">BI 报表配置。</param>
    /// <param name="logger">日志组件。</param>
    public BiReportRepository(
        ISqlSugarClient db,
        BiReportSchemaCache schemaCache,
        BiReportOptions options,
        ILogger<BiReportRepository> logger)
    {
        _db = db;
        _schemaCache = schemaCache;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BiReportSchemaDto> GetSchemaAsync(bool forceRefresh = false)
    {
        return await _schemaCache.GetAsync(forceRefresh);
    }

    /// <inheritdoc />
    public async Task<BiReportResultDto> QueryAsync(BiReportQueryDto query)
    {
        var schema = await _schemaCache.GetAsync();
        var command = BiReportSqlBuilder.BuildSelect(query, schema, _options);

        var dataTable = await ExecuteAsync(command);
        var items = ToDictionaries(dataTable, command.Columns);

        return new BiReportResultDto
        {
            columns = command.Columns,
            items = items,
            total = await ResolveTotalAsync(query, command, schema, items.Count),
            page_index = command.PageIndex,
            page_size = command.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<BiReportExportDataDto> GetExportDataAsync(BiReportQueryDto query, int maxRows)
    {
        var schema = await _schemaCache.GetAsync();

        // 多取一行：取到 maxRows + 1 行就说明真实结果超过了上限。
        // 比「先 count 再决定」少一次查询，而且不用去猜 count 与取数之间数据有没有变
        var command = BiReportSqlBuilder.BuildExport(query, schema, _options, maxRows + 1);

        var dataTable = await ExecuteAsync(command);

        return new BiReportExportDataDto
        {
            columns = command.Columns,
            items = ToDictionaries(dataTable, command.Columns)
        };
    }

    private async Task<DataTable> ExecuteAsync(BiSqlCommand command)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataTable = await _db.Ado.GetDataTableAsync(command.Sql, command.Parameters.ToArray());
        stopwatch.Stop();

        // 同时记 SQL 全文和耗时：出问题时最想知道的就是「到底跑了什么、跑了多久」。
        // 参数值只在 Parameters 里、不拼进 SQL 文本，所以日志里看不到用户填的查询条件。
        // 走 Debug 级别，排查性能问题时才开
        _logger.LogDebug(
            "BI 查询完成：耗时 {ElapsedMs} ms，{RowCount} 行 × {ColumnCount} 列，参数 {ParameterCount} 个。SQL：{Sql}",
            stopwatch.ElapsedMilliseconds,
            dataTable.Rows.Count,
            dataTable.Columns.Count,
            command.Parameters.Count,
            command.Sql);

        return dataTable;
    }

    /// <summary>
    /// 得出总记录数，尽量少跑一次查询。
    /// </summary>
    private async Task<long?> ResolveTotalAsync(
        BiReportQueryDto query,
        BiSqlCommand command,
        BiReportSchemaDto schema,
        int rowCount)
    {
        if (!query.need_total)
        {
            return null;
        }

        // 第一页就没取满 ⟹ 结果集已经到底了，本页行数就是总数，不用再 count 一次
        if (command.PageIndex == 1 && rowCount < command.PageSize)
        {
            return rowCount;
        }

        var countCommand = BiReportSqlBuilder.BuildCount(query, schema, _options);
        var scalar = await _db.Ado.GetScalarAsync(countCommand.Sql, countCommand.Parameters.ToArray());
        return scalar is null or DBNull ? 0 : Convert.ToInt64(scalar);
    }

    /// <summary>
    /// 把结果集转成「列名 → 值」的字典列表。
    /// </summary>
    private static List<Dictionary<string, object?>> ToDictionaries(DataTable dataTable, List<string> columns)
    {
        // 列数与列名都要对得上：对不上说明结果集和我们的预期错位了，
        // 这时宁可抛异常也不要「按位置硬读」——位置读错不会报错，只会安静地把 A 列的值放进 B 列
        if (dataTable.Columns.Count != columns.Count)
        {
            throw new InvalidOperationException(
                $"查询结果列数与预期不符：预期 {columns.Count} 列，实际 {dataTable.Columns.Count} 列。");
        }

        var indexes = new int[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            indexes[i] = dataTable.Columns.IndexOf(columns[i]);
            if (indexes[i] < 0)
            {
                throw new InvalidOperationException($"查询结果中缺少列 {columns[i]}。");
            }
        }

        var items = new List<Dictionary<string, object?>>(dataTable.Rows.Count);
        foreach (DataRow row in dataTable.Rows)
        {
            var item = new Dictionary<string, object?>(columns.Count, StringComparer.Ordinal);
            for (var i = 0; i < columns.Count; i++)
            {
                var value = row[indexes[i]];
                // DBNull 必须归一成 null：System.Text.Json 把 DBNull.Value 序列化成 {}，
                // 前端拿到的是个空对象而不是 null，所有判空逻辑都会失效
                item[columns[i]] = value is DBNull ? null : value;
            }

            items.Add(item);
        }

        return items;
    }
}
