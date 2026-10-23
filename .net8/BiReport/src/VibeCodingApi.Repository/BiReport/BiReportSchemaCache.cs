using System.Data;
using System.Globalization;
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
        var schema = new BiReportSchemaDto { generated_at = DateTime.Now };
        var missing = new List<string>();

        foreach (var tableOption in _options.Tables)
        {
            var table = await BuildTableAsync(tableOption, missing);
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
                "请确认表名、Database / 分表配置正确，且连接账号对 information_schema 可见这些表。",
                missing.Count,
                string.Join("、", missing));
        }

        if (schema.tables.Count == 0)
        {
            throw new InvalidOperationException(
                $"BI 报表白名单中的 {_options.Tables.Count} 张表全部读不到元数据（{string.Join("、", missing)}）。" +
                "通常是连接账号对 information_schema 的可见性受限，或表名 / 分表配置有误。");
        }

        _logger.LogInformation(
            "BI 报表元数据已加载：{TableCount} 张表、{ColumnCount} 个字段。",
            schema.tables.Count,
            schema.tables.Sum(table => table.columns.Count));

        return schema;
    }

    /// <summary>
    /// 读取一张逻辑表的元数据。普通表读一张物理表；分表先发现物理分表，再取所有分表的列交集。
    /// 读不到 / 配置与库不一致时返回 null 并记日志，由 <see cref="BuildAsync"/> 决定是降级还是整体失败。
    /// </summary>
    private async Task<BiReportTableSchemaDto?> BuildTableAsync(
        BiReportTableOption tableOption,
        List<string> missing)
    {
        var logicalName = tableOption.Name.Trim();
        var database = string.IsNullOrWhiteSpace(tableOption.Database)
            ? null
            : tableOption.Database.Trim();

        // 普通表：逻辑名就是物理表名
        if (string.IsNullOrWhiteSpace(tableOption.ShardTablePrefix))
        {
            var rows = await LoadColumnsAsync(database, new[] { logicalName });
            if (rows.Count == 0)
            {
                missing.Add(logicalName);
                return null;
            }

            return BuildTable(tableOption, logicalName, rows, database, null, null);
        }

        // 分表：先按前缀发现物理表，再读它们的字段
        var prefix = tableOption.ShardTablePrefix.Trim();
        var dateFormat = string.IsNullOrWhiteSpace(tableOption.ShardNameDateFormat)
            ? BiReportTableOption.DefaultShardNameDateFormat
            : tableOption.ShardNameDateFormat.Trim();

        var (shards, shardError) = await LoadShardsAsync(database, prefix, dateFormat, logicalName);
        if (shardError is not null)
        {
            _logger.LogError("BI 报表分表 {Table} 配置无法使用：{Error}，已跳过该表。", logicalName, shardError);
            missing.Add(logicalName);
            return null;
        }

        if (shards.Count == 0)
        {
            missing.Add(logicalName);
            return null;
        }

        if (shards.Count > _options.MaxShardsPerTable)
        {
            _logger.LogError(
                "BI 报表分表 {Table} 匹配到 {ShardCount} 张物理表，超过上限 {MaxShards}，已跳过。" +
                "请清理过期分表，或把 ShardTablePrefix 配得更精确。",
                logicalName,
                shards.Count,
                _options.MaxShardsPerTable);
            missing.Add(logicalName);
            return null;
        }

        var shardRows = await LoadColumnsAsync(database, shards.Select(shard => shard.TableName).ToList());
        var rowsByTable = shardRows
            .GroupBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        // 任何一张分表读不到字段都直接跳过整张逻辑表：只合并一部分分表会让查询结果静默缺数据
        var unreadable = shards.FirstOrDefault(shard => !rowsByTable.ContainsKey(shard.TableName));
        if (unreadable is not null)
        {
            _logger.LogError(
                "BI 报表分表 {Table} 的物理表 {Shard} 读不到字段元数据，已跳过该逻辑表。",
                logicalName,
                unreadable.TableName);
            missing.Add(logicalName);
            return null;
        }

        var shardDateColumn = string.IsNullOrWhiteSpace(tableOption.ShardDateColumn)
            ? null
            : tableOption.ShardDateColumn.Trim();

        if (shardDateColumn is not null)
        {
            var dateColumn = shardRows.FirstOrDefault(row =>
                string.Equals(row.ColumnName, shardDateColumn, StringComparison.OrdinalIgnoreCase));

            // 配了日期字段但库里没有（或类型不是日期）：降级为「不裁剪分表」，仍然可以查询
            if (dateColumn is null || !BiIdentifier.IsDateType(dateColumn.DataType))
            {
                _logger.LogWarning(
                    "BI 报表分表 {Table} 的 ShardDateColumn={Column} 不存在或不是日期类型，分表裁剪已关闭，" +
                    "查询会扫描全部分表。",
                    logicalName,
                    shardDateColumn);
                shardDateColumn = null;
            }
        }

        return BuildTable(tableOption, logicalName, shardRows, database, shards, shardDateColumn);
    }

    /// <summary>
    /// 按前缀发现物理分表，并解析表名里的日期后缀。
    /// 返回错误字符串表示「这张逻辑表不能用」（非法表名、同月多张表等），而不是静默少算数据。
    /// </summary>
    private async Task<(List<ShardTableInfo> Shards, string? Error)> LoadShardsAsync(
        string? database,
        string prefix,
        string dateFormat,
        string logicalName)
    {
        var schemaPredicate = database is null ? "DATABASE()" : "@db";
        var sql = $"""
            SELECT TABLE_NAME    AS table_name,
                   TABLE_COMMENT AS table_comment
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = {schemaPredicate}
              AND TABLE_TYPE = 'BASE TABLE'
              AND LEFT(TABLE_NAME, CHAR_LENGTH(@prefix)) = @prefix
            ORDER BY TABLE_NAME
            """;

        var parameters = new List<SugarParameter>();
        if (database is not null)
        {
            parameters.Add(new SugarParameter("@db", database));
        }

        parameters.Add(new SugarParameter("@prefix", prefix));

        var dataTable = await _db.Ado.GetDataTableAsync(sql, parameters.ToArray());
        var indexes = BuildColumnIndexes(dataTable);
        var shards = new List<ShardTableInfo>();

        foreach (DataRow row in dataTable.Rows)
        {
            var tableName = AsText(row, indexes, "table_name");
            if (tableName.Length == 0)
            {
                continue;
            }

            var suffix = tableName[prefix.Length..];
            if (!DateTime.TryParseExact(suffix, dateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var shardDate))
            {
                // 前缀撞上的备份表 / 人工表，不参与分表是有意为之，用 Debug 免得刷日志
                _logger.LogDebug(
                    "BI 报表分表 {Table} 的前缀匹配到 {PhysicalTable}，但后缀 {Suffix} 不符合日期格式 {Format}，已忽略。",
                    logicalName,
                    tableName,
                    suffix,
                    dateFormat);
                continue;
            }

            if (!BiIdentifier.IsValid(tableName))
            {
                return (new List<ShardTableInfo>(),
                    $"物理分表名 {tableName} 含不受支持的字符，无法安全拼进 SQL");
            }

            shards.Add(new ShardTableInfo
            {
                TableName = tableName,
                TableComment = AsText(row, indexes, "table_comment"),
                PeriodStart = new DateTime(shardDate.Year, shardDate.Month, 1)
            });
        }

        // 一个月只能有一张分表。同月多张表时无法判断该读哪一张，宁可整张逻辑表不可用，
        // 也不要 UNION 两份数据把结果翻倍
        var duplicateMonth = shards
            .GroupBy(shard => (shard.PeriodStart.Year, shard.PeriodStart.Month))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMonth is not null)
        {
            return (new List<ShardTableInfo>(),
                $"{string.Join("、", duplicateMonth.Select(shard => shard.TableName))} 都落在 " +
                $"{duplicateMonth.Key.Year}-{duplicateMonth.Key.Month:00}，一个月只能有一张分表");
        }

        return (shards.OrderBy(shard => shard.PeriodStart).ThenBy(shard => shard.TableName).ToList(), null);
    }

    /// <summary>
    /// 读取指定库、指定物理表的字段元数据（表名走参数，配置多打一个引号也只会变成「查不到」）。
    /// <paramref name="database"/> 为 null 时用 <c>DATABASE()</c>，即当前连接默认库。
    /// </summary>
    private async Task<List<MetadataRow>> LoadColumnsAsync(
        string? database,
        IReadOnlyCollection<string> tableNames)
    {
        if (tableNames.Count == 0)
        {
            return new List<MetadataRow>();
        }

        var parameters = new List<SugarParameter>();
        if (database is not null)
        {
            parameters.Add(new SugarParameter("@db", database));
        }

        var placeholders = new string[tableNames.Count];
        var index = 0;
        foreach (var tableName in tableNames)
        {
            placeholders[index] = "@t" + index;
            parameters.Add(new SugarParameter(placeholders[index], tableName));
            index++;
        }

        var schemaPredicate = database is null ? "DATABASE()" : "@db";
        var sql = $"""
            SELECT t.TABLE_SCHEMA      AS table_schema,
                   t.TABLE_NAME        AS table_name,
                   t.TABLE_COMMENT     AS table_comment,
                   c.COLUMN_NAME       AS column_name,
                   c.DATA_TYPE         AS data_type,
                   c.IS_NULLABLE       AS is_nullable,
                   c.COLUMN_COMMENT    AS column_comment
            FROM information_schema.TABLES t
            JOIN information_schema.COLUMNS c
              ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
            WHERE t.TABLE_SCHEMA = {schemaPredicate}
              AND t.TABLE_TYPE = 'BASE TABLE'
              AND t.TABLE_NAME IN ({string.Join(", ", placeholders)})
            ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION
            """;

        var dataTable = await _db.Ado.GetDataTableAsync(sql, parameters.ToArray());
        return ReadRows(dataTable);
    }

    private static Dictionary<string, int> BuildColumnIndexes(DataTable dataTable)
    {
        // 列名按 OrdinalIgnoreCase 建索引：不同 MySQL 版本/驱动返回的大小写未必一致，
        // 而 DataTable 的列名比较默认区分大小写，写死字符串取列会在某些环境下标错列
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dataTable.Columns.Count; i++)
        {
            indexes[dataTable.Columns[i].ColumnName] = i;
        }

        return indexes;
    }

    private static List<MetadataRow> ReadRows(DataTable dataTable)
    {
        var indexes = BuildColumnIndexes(dataTable);

        var rows = new List<MetadataRow>(dataTable.Rows.Count);
        foreach (DataRow row in dataTable.Rows)
        {
            rows.Add(new MetadataRow
            {
                TableSchema = AsText(row, indexes, "table_schema"),
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

    /// <summary>
    /// 由物理表元数据构建一张逻辑表。
    /// <para>
    /// 分表时取所有物理分表字段的<b>交集</b>：UNION ALL 的内层 SELECT 必须每张表都能提供同一批列，
    /// 老分表缺列时只暴露公共列，而不是让整条查询在数据库端报「Unknown column」。
    /// </para>
    /// </summary>
    private BiReportTableSchemaDto? BuildTable(
        BiReportTableOption tableOption,
        string logicalName,
        List<MetadataRow> rows,
        string? database,
        List<ShardTableInfo>? shards,
        string? shardDateColumn)
    {
        var rowsByTable = rows
            .GroupBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var canonicalTableName = logicalName;
        if (shards is null)
        {
            // 普通表用库里的规范拼写：配置写 truck 还是 TRUCK 都行，输出必须是库里真实的表名
            canonicalTableName = rows[0].TableName;
            if (!BiIdentifier.IsValid(canonicalTableName))
            {
                _logger.LogWarning(
                    "BI 报表白名单中的表 {Table} 的物理表名含不受支持的字符，无法拼进 SQL，已从元数据中剔除。",
                    canonicalTableName);
                return null;
            }
        }

        HashSet<string>? commonColumnNames = null;
        List<MetadataRow> metadataRows;
        if (shards is null)
        {
            metadataRows = rows;
        }
        else
        {
            commonColumnNames = rowsByTable.Values
                .Select(group => group.Select(row => row.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase))
                .Aggregate((left, right) =>
                {
                    left.IntersectWith(right);
                    return left;
                });

            if (commonColumnNames.Count == 0)
            {
                _logger.LogWarning(
                    "BI 报表分表 {Table} 的所有物理分表没有共同字段，已从元数据中剔除。",
                    logicalName);
                return null;
            }

            // 列定义以最新一张分表为准（新加列通常先出现在最新分表上）。
            // 老分表与最新分表的列集合不一时给出提示，但只暴露交集，保证 UNION 一定成立
            var newestTable = shards[^1].TableName;
            metadataRows = rowsByTable[newestTable];

            var unionColumnNames = rowsByTable.Values
                .SelectMany(group => group.Select(row => row.ColumnName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (unionColumnNames.Count != commonColumnNames.Count)
            {
                _logger.LogWarning(
                    "BI 报表分表 {Table} 的各物理分表字段不一致：共 {UnionCount} 个字段，只有 {CommonCount} 个在所有分表中都存在。" +
                    "元数据仅暴露公共字段，缺字段的老分表参与 UNION 时不会报错。",
                    logicalName,
                    unionColumnNames.Count,
                    commonColumnNames.Count);
            }
        }

        var first = metadataRows[0];
        var tableComment = string.IsNullOrWhiteSpace(first.TableComment) && shards is { Count: > 0 }
            ? shards[^1].TableComment
            : first.TableComment;

        var table = new BiReportTableSchemaDto
        {
            table_name = canonicalTableName,
            table_comment = NullIfEmpty(tableComment),
            display_name = FirstNonEmpty(tableOption.DisplayName, tableComment, canonicalTableName),
            columns = new List<BiReportColumnSchemaDto>(),
            database = database,
            shard_date_column = shardDateColumn,
            shards = shards?.Select(shard => new BiReportShardSchemaDto
            {
                table_name = shard.TableName,
                period_start = shard.PeriodStart
            }).ToList() ?? new List<BiReportShardSchemaDto>()
        };

        var includeColumns = ToNameSet(tableOption.IncludeColumns);
        var excludeColumns = ToNameSet(tableOption.ExcludeColumns);
        var displayNames = ToDisplayNameMap(tableOption.ColumnDisplayNames);

        foreach (var row in metadataRows)
        {
            if (row.ColumnName.Length == 0 ||
                (commonColumnNames is not null && !commonColumnNames.Contains(row.ColumnName)) ||
                !IsColumnVisible(row.ColumnName, includeColumns, excludeColumns, table.table_name))
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
        public string TableSchema { get; init; } = string.Empty;

        public string TableName { get; init; } = string.Empty;

        public string TableComment { get; init; } = string.Empty;

        public string ColumnName { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        public bool IsNullable { get; init; }

        public string ColumnComment { get; init; } = string.Empty;
    }

    /// <summary>发现到的一张物理分表。</summary>
    private sealed class ShardTableInfo
    {
        public string TableName { get; init; } = string.Empty;

        public string TableComment { get; init; } = string.Empty;

        /// <summary>该分表覆盖月份的 1 日。</summary>
        public DateTime PeriodStart { get; init; }
    }
}
