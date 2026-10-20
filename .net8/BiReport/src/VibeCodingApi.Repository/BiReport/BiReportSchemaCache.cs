using System.Data;
using Microsoft.Extensions.Logging;
using SqlSugar;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Repository.BiReport;

/// <summary>
/// 可查询元数据（表 / 字段）的读取与缓存。
/// <para>
/// 表清单来自配置白名单，字段名、类型、注释从 <c>INFORMATION_SCHEMA</c> 实时读取——
/// 配一份表名就够了，字段不用跟着 DDL 手工同步。
/// </para>
/// <para>
/// 缓存用「不可变快照 + 双检锁」：元数据在两次刷新之间是只读的，读路径完全无锁；
/// 缓存过期时并发的 N 个请求里只有第一个真正查库，其余拿到同一份结果。
/// </para>
/// </summary>
public sealed class BiReportSchemaCache
{
    /// <summary>内置强制排除列，转成不区分大小写的集合，避免每个列都重建一次。</summary>
    private static readonly HashSet<string> ForcedExcludeColumns =
        new(BiReportOptions.ForcedExcludeColumns, StringComparer.OrdinalIgnoreCase);

    private readonly ISqlSugarClient _db;
    private readonly BiReportOptions _options;
    private readonly ILogger<BiReportSchemaCache> _logger;

    /// <summary>刷新互斥锁：保证同一时刻只有一次元数据查询在跑。</summary>
    /// 初始许可数为 1、最大许可数也为 1 的 SemaphoreSlim，然后异步等待获取这个许可。它实际上被用作一个异步互斥锁（async mutex）
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private readonly long _ttlMilliseconds;

    /// <summary>当前生效的元数据快照。值和它的时间戳必须一起替换，否则会读到「新数据配旧时间」。</summary>
    private Snapshot? _snapshot;

    /// <summary>构造函数。</summary>
    /// <param name="db">SqlSugar 客户端（根作用域 = 业务库 dispatch）。</param>
    /// <param name="options">BI 报表配置。</param>
    /// <param name="logger">日志组件。</param>
    public BiReportSchemaCache(
        ISqlSugarClient db,
        BiReportOptions options,
        ILogger<BiReportSchemaCache> logger)
    {
        _db = db;
        _options = options;
        _logger = logger;
        // 配成 0 或负数时按「每次都取最新」处理。注意即使 TTL 为 0，也不关掉双检锁——
        // 它保护的是并发击穿，和 TTL 是两件事
        _ttlMilliseconds = Math.Max(0, options.SchemaCacheSeconds) * 1000L;
    }

    /// <summary>
    /// 取元数据。命中缓存直接返回；否则查库并替换快照。
    /// </summary>
    /// <param name="forceRefresh">true 表示忽略 TTL 强制刷新（DDL 变更后用）。</param>
    public async Task<BiReportSchemaDto> GetAsync(bool forceRefresh = false)
    {
        //获取系统启动以来经过的毫秒数，返回一个 64 位整数（long）
        var requestedAt = Environment.TickCount64;

        var snapshot = Volatile.Read(ref _snapshot);
        if (!forceRefresh && IsFresh(snapshot, requestedAt))
        {
            return snapshot!.Schema;
        }

        await _refreshLock.WaitAsync();
        try
        {
            snapshot = Volatile.Read(ref _snapshot);

            // 双检锁：同一个过期窗口里并发打进来的请求，只有一个真的查库。
            // forceRefresh 也走这一步——排队等锁期间别人刚刷过（快照时间戳晚于本次进入的时刻），
            // 直接复用它，免得刷新接口被连点打出一串重复查询
            var reusable = !forceRefresh
                ? IsFresh(snapshot, requestedAt)
                : snapshot is not null && snapshot.CachedAt >= requestedAt;

            if (reusable)
            {
                return snapshot!.Schema;
            }

            var schema = await BuildAsync();
            Volatile.Write(ref _snapshot, new Snapshot(schema, Environment.TickCount64));
            return schema;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// 废弃当前快照。配置热更新后调用，下次请求会重新查库。
    /// </summary>
    public void Invalidate() => Volatile.Write(ref _snapshot, null);

    private bool IsFresh(Snapshot? snapshot, long now) =>
        snapshot is not null && now - snapshot.CachedAt < _ttlMilliseconds;

    private async Task<BiReportSchemaDto> BuildAsync()
    {
        var configured = _options.Tables;

        var parameters = new SugarParameter[configured.Count];
        var placeholders = new string[configured.Count];
        for (var i = 0; i < configured.Count; i++)
        {
            placeholders[i] = "@t" + i;
            // 白名单表名来自配置而非请求，但仍然走参数，免得「配置里多打了个引号」变成语法错误
            parameters[i] = new SugarParameter(placeholders[i], configured[i].Name.Trim());
        }

        var dataTable = await _db.Ado.GetDataTableAsync(BuildMetadataSql(placeholders), parameters);
        var rows = ReadRows(dataTable);

        var schema = new BiReportSchemaDto { generated_at = DateTime.Now };
        var missing = new List<string>();

        foreach (var tableOption in configured)
        {
            var tableName = tableOption.Name.Trim();
            var tableRows = rows
                .Where(row => string.Equals(row.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tableRows.Count == 0)
            {
                missing.Add(tableName);
                continue;
            }

            var table = BuildTable(tableOption, tableRows);
            if (table is not null)
            {
                schema.tables.Add(table);
            }
        }

        // 配置里写了、库里却读不到：最常见的原因是连接账号对 information_schema 的可见性受限——
        // MySQL 只会列出当前账号有权限的对象，且**不报错**，和「表被删了」长得一模一样。
        // 少数表读不到降级跳过（可能只是其中一张被重命名了），一张都读不到则直接失败：
        // 那几乎一定是账号权限或库名配置问题，静默返回空元数据会让前端以为「系统里没有可查的表」。
        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "BI 报表白名单中有 {MissingCount} 张表读不到元数据，已跳过：{MissingTables}。" +
                "请确认表名正确，且连接账号对 information_schema 可见这些表。",
                missing.Count,
                string.Join("、", missing));
        }

        if (schema.tables.Count == 0)
        {
            throw new InvalidOperationException(
                $"BI 报表白名单中的 {configured.Count} 张表全部读不到元数据（{string.Join("、", missing)}）。" +
                "通常是连接账号对 information_schema 的可见性受限，或表名配置有误。");
        }

        _logger.LogInformation(
            "BI 报表元数据已加载：{TableCount} 张表、{ColumnCount} 个字段。",
            schema.tables.Count,
            schema.tables.Sum(table => table.columns.Count));

        return schema;
    }

    private static string BuildMetadataSql(IReadOnlyList<string> placeholders) => $"""
        SELECT t.TABLE_NAME      AS table_name,
               t.TABLE_COMMENT   AS table_comment,
               c.COLUMN_NAME     AS column_name,
               c.DATA_TYPE       AS data_type,
               c.IS_NULLABLE     AS is_nullable,
               c.COLUMN_COMMENT  AS column_comment
        FROM information_schema.TABLES t
        JOIN information_schema.COLUMNS c
          ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
        WHERE t.TABLE_SCHEMA = DATABASE()
          AND t.TABLE_TYPE = 'BASE TABLE'
          AND t.TABLE_NAME IN ({string.Join(", ", placeholders)})
        ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION
        """;

    private static List<MetadataRow> ReadRows(DataTable dataTable)
    {
        // 列名按 OrdinalIgnoreCase 建索引：不同 MySQL 版本/驱动返回的大小写未必一致，
        // 而 DataTable 的列名比较默认区分大小写，写死字符串取列会在某些环境下标错列
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dataTable.Columns.Count; i++)
        {
            indexes[dataTable.Columns[i].ColumnName] = i;
        }

        var rows = new List<MetadataRow>(dataTable.Rows.Count);
        foreach (DataRow row in dataTable.Rows)
        {
            rows.Add(new MetadataRow
            {
                TableName = AsText(row, indexes, "table_name"),
                TableComment = AsText(row, indexes, "table_comment"),
                ColumnName = AsText(row, indexes, "column_name"),
                DataType = AsText(row, indexes, "data_type"),
                // MySQL 的 IS_NULLABLE 是 'YES' / 'NO' 字符串
                IsNullable = string.Equals(AsText(row, indexes, "is_nullable"), "YES", StringComparison.OrdinalIgnoreCase),
                ColumnComment = AsText(row, indexes, "column_comment")
            });
        }

        return rows;
    }

    private static string AsText(DataRow row, Dictionary<string, int> indexes, string column)
    {
        if (!indexes.TryGetValue(column, out var index) || row.IsNull(index))
        {
            return string.Empty;
        }

        return row[index]?.ToString()?.Trim() ?? string.Empty;
    }

    private BiReportTableSchemaDto? BuildTable(BiReportTableOption tableOption, List<MetadataRow> rows)
    {
        // 用数据库里的规范拼写，而不是配置里写的那个：配置写 truck 还是 TRUCK 都行，
        // 但输出给前端的必须是库里真实的表名，否则前端拿它回填请求时会与元数据对不上
        var first = rows[0];

        var table = new BiReportTableSchemaDto
        {
            table_name = first.TableName,
            table_comment = NullIfEmpty(first.TableComment),
            display_name = FirstNonEmpty(tableOption.DisplayName, first.TableComment, first.TableName),
            columns = new List<BiReportColumnSchemaDto>()
        };

        var includeColumns = ToNameSet(tableOption.IncludeColumns);
        var excludeColumns = ToNameSet(tableOption.ExcludeColumns);
        var displayNames = ToDisplayNameMap(tableOption.ColumnDisplayNames);

        foreach (var row in rows)
        {
            if (row.ColumnName.Length == 0 || !IsColumnVisible(row.ColumnName, includeColumns, excludeColumns, table.table_name))
            {
                continue;
            }

            displayNames.TryGetValue(row.ColumnName, out var customDisplayName);

            table.columns.Add(new BiReportColumnSchemaDto
            {
                column_name = row.ColumnName,
                display_name = FirstNonEmpty(customDisplayName, row.ColumnComment, row.ColumnName),
                data_type = row.DataType,
                is_nullable = row.IsNullable,
                column_comment = NullIfEmpty(row.ColumnComment),
                is_numeric = BiIdentifier.IsNumericType(row.DataType),
                is_date = BiIdentifier.IsDateType(row.DataType),
                is_string = BiIdentifier.IsStringType(row.DataType)
            });
        }

        if (table.columns.Count == 0)
        {
            // 白名单里声明了一张表，结果它的列被排除规则清空了（例如把列名清单误填成了 IncludeColumns）。
            // 留着它只会让前端显示一张点开没有字段的表，不如剔除并告警
            _logger.LogWarning(
                "BI 报表白名单中的表 {Table} 在应用排除规则后没有可查询的字段，已从元数据中剔除。",
                table.table_name);
            return null;
        }

        return table;
    }

    /// <summary>
    /// 判断一个列是否要暴露给前端。规则按「后面覆盖前面」的顺序层层收窄，
    /// 只有 <see cref="BiReportOptions.ForcedExcludeColumns"/> 不可被配置绕过。
    /// </summary>
    private bool IsColumnVisible(
        string columnName,
        HashSet<string>? includeColumns,
        HashSet<string>? excludeColumns,
        string tableName)
    {
        // 1) include_columns 非空时进入白名单模式：没列进去的一律不可见
        if (includeColumns is not null && !includeColumns.Contains(columnName))
        {
            return false;
        }

        // 2) 代码内置的强制排除，任何配置都改不动。
        //    这是「设计如此」而不是异常，所以走 Debug——否则每刷新一次元数据就刷三条告警，
        //    真正需要注意的日志会被淹掉
        if (ForcedExcludeColumns.Contains(columnName))
        {
            _logger.LogDebug(
                "表 {Table} 的列 {Column} 命中内置的强制排除清单，不会出现在 BI 元数据中。",
                tableName,
                columnName);
            return false;
        }

        // 3) 配置里显式排除的列
        if (excludeColumns?.Contains(columnName) == true)
        {
            return false;
        }

        // 4) 敏感关键字兜底：给将来新增的敏感字段留的后路，不指望每个新字段都有人记得加进排除清单。
        //    include_columns 里显式写过的列算人工确认过，放行（第 1 步已经保证它确实在清单里）
        if (includeColumns is null && IsSensitiveName(columnName))
        {
            _logger.LogWarning(
                "表 {Table} 的列 {Column} 命中敏感字段关键字，默认排除；确有查询需要请在配置的 include_columns 中显式放行。",
                tableName,
                columnName);
            return false;
        }

        // 5) 列名要能通过标识符守卫，否则它无论如何都拼不进 SQL，
        //    与其让前端看到一个「选了就报错」的列，不如根本不提供
        if (!BiIdentifier.IsValid(columnName))
        {
            _logger.LogWarning(
                "表 {Table} 的列 {Column} 含不受支持的字符，BI 查询无法引用它，已从元数据中剔除。",
                tableName,
                columnName);
            return false;
        }

        return true;
    }

    private static bool IsSensitiveName(string columnName) =>
        BiReportOptions.SensitiveColumnKeywords.Any(keyword =>
            columnName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static HashSet<string>? ToNameSet(IReadOnlyCollection<string>? names)
    {
        var set = names?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return set is { Count: > 0 } ? set : null;
    }

    private static Dictionary<string, string> ToDisplayNameMap(Dictionary<string, string>? map)
    {
        if (map is null || map.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // 配置里的 key 未必和库里的拼写完全一致（MySQL 列名不区分大小写），统一归一化后再查
        var normalized = new Dictionary<string, string>(map.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in map)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                normalized[key.Trim()] = value.Trim();
            }
        }

        return normalized;
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim() ?? string.Empty;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 元数据快照。<see cref="CachedAt"/> 是 <see cref="Environment.TickCount64"/>（单调毫秒），
    /// 不用挂钟时间：系统时间被 NTP 校正或人工调整时，挂钟会跳变，TTL 可能瞬间变成负数或超大。
    /// </summary>
    private sealed record Snapshot(BiReportSchemaDto Schema, long CachedAt);

    /// <summary>从 <c>INFORMATION_SCHEMA</c> 读回来的一行（一个列）。</summary>
    private sealed class MetadataRow
    {
        public string TableName { get; init; } = string.Empty;

        public string TableComment { get; init; } = string.Empty;

        public string ColumnName { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        public bool IsNullable { get; init; }

        public string ColumnComment { get; init; } = string.Empty;
    }
}
