using System.Text;
using SqlSugar;
using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Repository.BiReport;

/// <summary>
/// 把前端的查询定义编译成参数化的 MySQL 语句（本模块的核心，也是唯一生成 SQL 的地方）。
/// <para>
/// <b>安全不变量</b>：SQL 文本里出现的每一段，要么是白名单里枚举出来的常量，
/// 要么是经 <see cref="BiIdentifier.Quote"/> 正则守卫并由反引号包裹的标识符；
/// 任何来自请求的「值」一律走 <see cref="SugarParameter"/>。
/// 表别名、运算符、聚合函数、join 类型都是查表得到的固定文本，请求里的字符串永不回显。
/// </para>
/// <para>
/// 编译是纯函数（不碰数据库、不依赖日志组件），因此可以在没有数据库的情况下断言
/// 「生成了什么 SQL、带了什么参数」，注入类用例才能固化进单测。
/// </para>
/// <para>
/// v1 明确不支持：OR 条件、HAVING、DISTINCT、日期函数与表达式列、跨库表。这些都需要把
/// 用户提供的字符串拼进表达式，是注入面的主要来源。将来要支持时，做法是加<b>枚举型</b>参数
/// （如 <c>{ "transform": "month" }</c> 由服务端生成 <c>DATE_FORMAT(...)</c>），
/// 而不是接受自由表达式。
/// </para>
/// </summary>
public static class BiReportSqlBuilder
{
    /// <summary>OFFSET 上限：再深就该让调用方加筛选条件（MySQL 深翻页要扫掉前面的行）。</summary>
    public const int MaxOffset = 10_000_000;

    /// <summary>简单比较运算符 → SQL 文本。键是规范化之后的形式，仍是查表而非直接拼接。</summary>
    private static readonly Dictionary<string, string> ComparisonOperatorSql = new(StringComparer.Ordinal)
    {
        ["="] = "=",
        ["<>"] = "<>",
        [">"] = ">",
        [">="] = ">=",
        ["<"] = "<",
        ["<="] = "<="
    };

    /// <summary>
    /// 聚合函数白名单 → SQL 函数名。<c>count_distinct</c> 走 COUNT，生成时另补 DISTINCT。
    /// </summary>
    private static readonly Dictionary<string, string> AggregateSql = new(StringComparer.OrdinalIgnoreCase)
    {
        ["count"] = "COUNT",
        ["count_distinct"] = "COUNT",
        ["sum"] = "SUM",
        ["avg"] = "AVG",
        ["max"] = "MAX",
        ["min"] = "MIN"
    };

    /// <summary>
    /// join 类型白名单。
    /// <para>
    /// 刻意<b>不支持 right join</b>：它与 left join 只差表的左右位置，而本生成器会按主表把 join
    /// 重排成树（见 <see cref="Compiler.BuildJoinSql"/>），「右表保留全部行」在这套重排下语义会走样。
    /// 需要 right join 的语义时，把两张表在 <c>tables</c> 里的顺序对调、改用 left join 即可。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> JoinTypeSql = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inner"] = "INNER JOIN",
        ["left"] = "LEFT JOIN"
    };

    /// <summary>编译分页查询语句（含 LIMIT / OFFSET）。页码与每页条数在这里夹紧，调用方不必自己兜底。</summary>
    public static BiSqlCommand BuildSelect(
        BiReportQueryDto request,
        BiReportSchemaDto schema,
        BiReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var compiled = new Compiler(request, schema, options).Compile();

        var pageIndex = request.page_index < 1 ? 1 : request.page_index;
        var pageSize = request.page_size switch
        {
            < 1 => options.DefaultPageSize,
            _ => request.page_size > options.MaxPageSize ? options.MaxPageSize : request.page_size
        };
        // 配置里把默认页大小写成 0 或负数时的兜底：否则 LIMIT 0 永远返回空，看着像查不到数据
        if (pageSize < 1)
        {
            pageSize = 1;
        }

        var offset = (long)(pageIndex - 1) * pageSize;
        if (offset > MaxOffset)
        {
            throw new BusinessException("页码过大，请增加筛选条件后重试");
        }

        var parameters = new List<SugarParameter>(compiled.Parameters);
        var limitName = AddParameter(parameters, pageSize);
        var offsetName = AddParameter(parameters, (int)offset);

        return new BiSqlCommand
        {
            Sql = $"{TimeoutHint(options)}{SelectClause(compiled)} LIMIT {limitName} OFFSET {offsetName}",
            Parameters = parameters,
            Columns = compiled.OutputColumns,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 编译总数统计语句。
    /// <para>
    /// 无分组时直接 <c>COUNT(1)</c>（能吃索引）；有分组时套一层派生表数「有多少组」，
    /// 内层去掉 ORDER BY 与 LIMIT——排序纯属浪费，LIMIT 更会直接改变计数语义。
    /// </para>
    /// </summary>
    public static BiSqlCommand BuildCount(
        BiReportQueryDto request,
        BiReportSchemaDto schema,
        BiReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var compiled = new Compiler(request, schema, options).Compile();
        var hint = TimeoutHint(options);

        var sql = compiled.GroupBySql.Length == 0
            ? $"{hint}SELECT COUNT(1) {compiled.FromSql}{compiled.JoinSql}{compiled.WhereSql}"
            // MySQL 5.7 要求派生表必须有别名（Every derived table must have its own alias），固定写 t
            : $"{hint}SELECT COUNT(1) FROM (SELECT 1 AS x {compiled.FromSql}{compiled.JoinSql}{compiled.WhereSql}{compiled.GroupBySql}) t";

        return new BiSqlCommand
        {
            Sql = sql,
            Parameters = compiled.Parameters
        };
    }

    /// <summary>
    /// 编译导出取数语句：不分页，只取 <paramref name="maxRows"/> 行。
    /// 调用方传 <c>上限 + 1</c>，多出来的那一行就是「结果超限」的信号。
    /// </summary>
    public static BiSqlCommand BuildExport(
        BiReportQueryDto request,
        BiReportSchemaDto schema,
        BiReportOptions options,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (maxRows < 1)
        {
            throw new BusinessException("导出上限必须大于 0");
        }

        var compiled = new Compiler(request, schema, options).Compile();

        var parameters = new List<SugarParameter>(compiled.Parameters);
        var limitName = AddParameter(parameters, maxRows);

        return new BiSqlCommand
        {
            Sql = $"{TimeoutHint(options)}{SelectClause(compiled)} LIMIT {limitName}",
            Parameters = parameters,
            Columns = compiled.OutputColumns
        };
    }

    private static string SelectClause(CompiledQuery compiled) =>
        $"SELECT {compiled.SelectListSql} {compiled.FromSql}{compiled.JoinSql}{compiled.WhereSql}{compiled.GroupBySql}{compiled.OrderBySql}";

    /// <summary>在 SELECT 后插入 MySQL 优化器提示，给单条语句一个执行时间上限；0 表示不限制。</summary>
    private static string TimeoutHint(BiReportOptions options) =>
        options.QueryTimeoutMs > 0 ? $"/*+ MAX_EXECUTION_TIME({options.QueryTimeoutMs}) */ " : string.Empty;

    /// <summary>按顺序追加一个参数并返回它的占位符名，保证名字与参数列表下标严格对齐。</summary>
    private static string AddParameter(List<SugarParameter> parameters, object? value)
    {
        var name = "@p" + parameters.Count;
        parameters.Add(new SugarParameter(name, value));
        return name;
    }

    /// <summary>编译产物，按片段拆开保存，好让 SELECT 与 COUNT 两条语句复用同一份 FROM / WHERE 生成结果。</summary>
    private sealed class CompiledQuery
    {
        /// <summary>SELECT 的列清单部分（不含 <c>SELECT</c> 关键字与尾部空格）。</summary>
        public string SelectListSql { get; set; } = string.Empty;

        /// <summary><c>FROM `truck` AS t0</c>。</summary>
        public string FromSql { get; set; } = string.Empty;

        /// <summary>各 JOIN 子句，每段以空格开头；没有关联时为空串。</summary>
        public string JoinSql { get; set; } = string.Empty;

        /// <summary><c> WHERE …</c>（含前导空格），没有过滤条件时为空串。</summary>
        public string WhereSql { get; set; } = string.Empty;

        /// <summary><c> GROUP BY …</c>（含前导空格），不分组时为空串。</summary>
        public string GroupBySql { get; set; } = string.Empty;

        /// <summary><c> ORDER BY …</c>（含前导空格），没有排序（且无兜底排序）时为空串。</summary>
        public string OrderBySql { get; set; } = string.Empty;

        public List<SugarParameter> Parameters { get; } = new();

        public List<string> OutputColumns { get; } = new();
    }

    /// <summary>参与查询的一张表在本次编译里的绑定关系。</summary>
    private sealed class TableBinding
    {
        /// <summary>本次查询实际引用到的列（select / where / join / group by / order by）。</summary>
        private readonly Dictionary<string, BiReportColumnSchemaDto> _usedColumns =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>在请求 <c>tables</c> 数组里的下标，用于生成 t0 / t1 / t2…</summary>
        public int Index { get; init; }

        /// <summary>服务端生成、真正写进 SQL 的表别名（t0 / t1 / t2…）。</summary>
        public string SqlAlias { get; init; } = string.Empty;

        /// <summary>用户指定的别名，只是引用键，一个字符都不会进 SQL。</summary>
        public string UserAlias { get; init; } = string.Empty;

        public BiReportTableSchemaDto Schema { get; init; } = null!;

        /// <summary>按 WHERE 里的日期过滤裁剪后、本次查询要扫的物理分表；普通表为空。</summary>
        public IReadOnlyList<BiReportShardSchemaDto> SelectedShards { get; set; } =
            Array.Empty<BiReportShardSchemaDto>();

        /// <summary>日期范围与所有分表都不相交时为 true，生成一个恒假分支让查询返回空集。</summary>
        public bool EmptyShardRange { get; set; }

        /// <summary>记录一个列被引用，供分表只 SELECT 需要的列。</summary>
        public void MarkUsed(BiReportColumnSchemaDto column) =>
            _usedColumns.TryAdd(column.column_name, column);

        /// <summary>
        /// 生成 FROM / JOIN 里的数据源表达式。
        /// <para>
        /// 普通表：<c>`库`.`表`</c>（未配置库时只写表名）。
        /// 分表：把选中的物理分表 UNION ALL 成派生表，外层仍然用 t0 / t1… 引用，
        /// 因此 join / 过滤 / 聚合的写法与普通表完全一致。
        /// </para>
        /// <para>
        /// 内层只 SELECT 本次查询真正引用到的列：分表通常很宽，SELECT * 会让 UNION 变重，
        /// 也会把未暴露的敏感列带进派生表（虽然外层引用不到）。
        /// </para>
        /// </summary>
        public string BuildSourceSql()
        {
            if (Schema.shards is not { Count: > 0 })
            {
                return BiIdentifier.QuoteTable(Schema.database, Schema.table_name);
            }

            var columns = Schema.columns
                .Where(column => _usedColumns.ContainsKey(column.column_name))
                .Select(column => BiIdentifier.Quote(column.column_name))
                .ToList();
            var selectList = columns.Count > 0 ? string.Join(", ", columns) : "1 AS x";

            IReadOnlyList<BiReportShardSchemaDto> shards = SelectedShards;
            var empty = EmptyShardRange;

            if (shards.Count == 0)
            {
                // 日期范围与任何分表都不相交：仍然要生成一条合法 SQL，
                // 用最后一张分表加恒假条件，结果为空且不会扫全表
                shards = new[] { Schema.shards[^1] };
                empty = true;
            }

            var branches = shards.Select(shard =>
            {
                var source = BiIdentifier.QuoteTable(Schema.database, shard.table_name);
                return empty
                    ? $"SELECT {selectList} FROM {source} WHERE 1=0"
                    : $"SELECT {selectList} FROM {source}";
            });

            return "(" + string.Join(" UNION ALL ", branches) + ")";
        }
    }

    /// <summary>已解析到具体表与列的字段引用。</summary>
    private sealed class ResolvedColumn
    {
        public ResolvedColumn(TableBinding table, BiReportColumnSchemaDto column)
        {
            Table = table;
            Column = column;
            // 在这里就把反引号拼好：列名来自元数据、别名来自服务端下标，都是可信的，
            // 而且只算一次，避免在拼接过程中反复校验
            Qualified = $"{table.SqlAlias}.{BiIdentifier.Quote(column.column_name)}";
            // 同时登记到表绑定上：分表的内层 SELECT 只需要带这些列
            table.MarkUsed(column);
        }

        public TableBinding Table { get; }

        public BiReportColumnSchemaDto Column { get; }

        /// <summary>可直接拼进 SQL 的限定列引用，如 <c>t0.`truck_code`</c>。</summary>
        public string Qualified { get; }
    }

    /// <summary>一个已校验的关联条件。</summary>
    private sealed class ValidatedJoin
    {
        public ResolvedColumn LeftColumn { get; init; } = null!;

        public ResolvedColumn RightColumn { get; init; } = null!;

        /// <summary><c>INNER JOIN</c> / <c>LEFT JOIN</c>，取自白名单字典。</summary>
        public string JoinKeyword { get; init; } = string.Empty;
    }

    /// <summary>一个输出列的编译结果，供 ORDER BY / GROUP BY 按别名反查表达式。</summary>
    private sealed class SelectItemResult
    {
        public string Alias { get; init; } = string.Empty;

        /// <summary>该输出列对应的 SQL 表达式（已限定表别名）。</summary>
        public string Expression { get; init; } = string.Empty;

        public bool IsAggregate { get; init; }
    }

    /// <summary>SELECT 列表的编译结果。</summary>
    private sealed class SelectResult
    {
        public string Sql { get; set; } = string.Empty;

        public List<string> OutputColumns { get; } = new();

        public List<SelectItemResult> Items { get; } = new();

        /// <summary>SELECT 里的非聚合列，用于分组完整性校验与自动补全分组。</summary>
        public List<ResolvedColumn> NonAggregateColumns { get; } = new();

        public bool HasAggregate { get; set; }
    }

    /// <summary>
    /// 一次编译的上下文。做成实例而不是一串静态方法的参数，
    /// 是因为分组/排序/输出列的校验互相依赖，需要共享已解析的表与列。
    /// </summary>
    private sealed class Compiler
    {
        private readonly BiReportQueryDto _request;
        private readonly BiReportSchemaDto _schema;
        private readonly BiReportOptions _options;
        private readonly List<SugarParameter> _parameters = new();

        private List<TableBinding> _bindings = new();
        private SelectResult _select = new();
        private List<string> _groupByExpressions = new();

        public Compiler(BiReportQueryDto request, BiReportSchemaDto schema, BiReportOptions options)
        {
            _request = request ?? throw new BusinessException("查询条件不能为空");
            _schema = schema ?? throw new BusinessException("表结构元数据不可用");
            _options = options;
        }

        public CompiledQuery Compile()
        {
            if (!_options.Enabled)
            {
                throw new BusinessException("BI 报表查询模块已关闭");
            }

            _bindings = BindTables();

            // 先完成所有列引用解析（join / select / where / group / order），
            // 才能知道每张分表需要 SELECT 哪些列；FROM / JOIN 文本放到最后生成。
            var joinOrder = BuildJoinOrder();
            _select = BuildSelectList();
            var whereSql = BuildWhere();
            var groupBySql = BuildGroupBy();
            var orderBySql = BuildOrderBy();

            // WHERE 里的日期条件已经解析完，再据此裁剪分表；不会影响已经生成的 WHERE 文本
            SelectShards();

            var compiled = new CompiledQuery
            {
                SelectListSql = _select.Sql,
                FromSql = $"FROM {_bindings[0].BuildSourceSql()} AS {_bindings[0].SqlAlias}",
                JoinSql = BuildJoinSql(joinOrder),
                WhereSql = whereSql,
                GroupBySql = groupBySql,
                OrderBySql = orderBySql
            };

            compiled.OutputColumns.AddRange(_select.OutputColumns);
            compiled.Parameters.AddRange(_parameters);
            return compiled;
        }

        /// <summary>解析参与查询的表，为每张表分配 SQL 别名（t0 / t1 / …）。</summary>
        private List<TableBinding> BindTables()
        {
            var references = _request.tables;
            if (references is null || references.Count == 0)
            {
                throw new BusinessException("请至少指定一张查询表");
            }

            if (references.Count > _options.MaxJoinTables)
            {
                throw new BusinessException($"查询涉及的表过多（最多 {_options.MaxJoinTables} 张）");
            }

            var bindings = new List<TableBinding>(references.Count);
            var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                if (reference is null || string.IsNullOrWhiteSpace(reference.table))
                {
                    throw new BusinessException($"第 {i + 1} 张表的表名不能为空");
                }

                var tableName = reference.table.Trim();
                var tableSchema = _schema.tables.FirstOrDefault(table =>
                    string.Equals(table.table_name, tableName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new BusinessException($"表 {tableName} 不在可查询的表清单中");

                var alias = string.IsNullOrWhiteSpace(reference.alias)
                    ? tableSchema.table_name
                    : reference.alias.Trim();

                // 别名只是引用键，但它参与 "别名.列名" 的切分，所以不能含点号或空白
                if (alias.Contains('.') || alias.Any(char.IsWhiteSpace))
                {
                    throw new BusinessException($"表别名 {alias} 不能包含点号或空白字符");
                }

                if (alias.Length > 30)
                {
                    throw new BusinessException($"表别名 {alias} 过长（最多 30 个字符）");
                }

                if (!usedAliases.Add(alias))
                {
                    throw new BusinessException($"表别名 {alias} 重复，请为每张表指定不同的别名");
                }

                bindings.Add(new TableBinding
                {
                    Index = i,
                    SqlAlias = "t" + i,
                    UserAlias = alias,
                    Schema = tableSchema
                });
            }

            return bindings;
        }

        /// <summary>
        /// 校验关联关系并按主表定序，返回已排序的关联边（真正拼 JOIN 子句在
        /// <see cref="BuildJoinSql"/>，那时才知道分表要 SELECT 哪些列）。
        /// <para>
        /// 判据是「无向连通图 + 边数 = 节点数 − 1」⟺ 一棵树。树上没有环，从主表按 BFS 顺序输出，
        /// 每个 ON 引用的表必定已经出现在前面的 FROM/JOIN 里，SQL 语法必然成立，
        /// 同时也就杜绝了笛卡尔积。
        /// </para>
        /// <para>
        /// 注意 <c>left</c> 的语义：重排后新加入的总是 JOIN 右侧，所以「左连接」在这里一律表示
        /// 「保留先出现那一侧的全部行」。链式 left join 时，保留的是主表的全部行——这正是
        /// 「以主表为基准补明细」的直觉行为。
        /// </para>
        /// </summary>
        private List<(ValidatedJoin Join, TableBinding Added)> BuildJoinOrder()
        {
            var joins = _request.joins ?? new List<BiReportJoinDto>();

            if (_bindings.Count == 1)
            {
                if (joins.Count > 0)
                {
                    throw new BusinessException("只查询一张表时不需要指定关联条件");
                }

                return new List<(ValidatedJoin, TableBinding)>();
            }

            if (joins.Count != _bindings.Count - 1)
            {
                throw new BusinessException(joins.Count < _bindings.Count - 1
                    ? $"关联条件不足：{_bindings.Count} 张表需要 {_bindings.Count - 1} 个关联条件，当前只有 {joins.Count} 个" +
                      "（有表没和主表连上，会形成笛卡尔积）"
                    : $"关联条件过多：{_bindings.Count} 张表需要 {_bindings.Count - 1} 个关联条件，当前有 {joins.Count} 个" +
                      "（存在冗余或环形关联）");
            }

            var validated = new List<ValidatedJoin>(joins.Count);
            for (var i = 0; i < joins.Count; i++)
            {
                validated.Add(ValidateJoin(joins[i], i));
            }

            // 无向邻接表
            var adjacency = new List<List<(int Neighbor, int JoinIndex)>>(_bindings.Count);
            for (var i = 0; i < _bindings.Count; i++)
            {
                adjacency.Add(new List<(int, int)>());
            }

            for (var i = 0; i < validated.Count; i++)
            {
                var left = validated[i].LeftColumn.Table.Index;
                var right = validated[i].RightColumn.Table.Index;
                adjacency[left].Add((right, i));
                adjacency[right].Add((left, i));
            }

            var visited = new bool[_bindings.Count];
            var emitted = new List<(ValidatedJoin Join, TableBinding Added)>(validated.Count);
            var queue = new Queue<int>();
            visited[0] = true;
            queue.Enqueue(0);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var (neighbor, joinIndex) in adjacency[current])
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    emitted.Add((validated[joinIndex], _bindings[neighbor]));
                    queue.Enqueue(neighbor);
                }
            }

            if (emitted.Count != validated.Count)
            {
                var unreachable = _bindings.Where(binding => !visited[binding.Index])
                    .Select(binding => binding.UserAlias);
                throw new BusinessException(
                    $"表 {string.Join("、", unreachable)} 没有和主表 {_bindings[0].UserAlias} 连接，会形成笛卡尔积");
            }

            return emitted;
        }

        /// <summary>
        /// 把已定序的关联边渲染成 JOIN 子句。必须在所有列引用解析完之后调用：
        /// 分表的数据源表达式取决于本次查询实际用到了哪些列（见 <see cref="TableBinding.BuildSourceSql"/>）。
        /// </summary>
        private static string BuildJoinSql(List<(ValidatedJoin Join, TableBinding Added)> emitted)
        {
            var builder = new StringBuilder();
            foreach (var (join, added) in emitted)
            {
                // ON 的两侧都用限定列引用：它们各自带着自己那张表的 tN 前缀，
                // 所以无论 BFS 从哪个方向走到这条边，等式的两边都是对的
                builder.Append(' ').Append(join.JoinKeyword)
                    .Append(' ').Append(added.BuildSourceSql())
                    .Append(" AS ").Append(added.SqlAlias)
                    .Append(" ON ").Append(join.LeftColumn.Qualified)
                    .Append(" = ").Append(join.RightColumn.Qualified);
            }

            return builder.ToString();
        }

        private ValidatedJoin ValidateJoin(BiReportJoinDto? join, int index)
        {
            var position = index + 1;
            if (join is null)
            {
                throw new BusinessException($"第 {position} 个关联条件为空");
            }

            var joinKeyword = JoinTypeSql.TryGetValue((join.join_type ?? string.Empty).Trim().ToLowerInvariant(), out var keyword)
                ? keyword
                : throw new BusinessException($"第 {position} 个关联条件的类型不支持：{join.join_type}（只支持 inner / left）");

            var leftTable = FindBinding(join.left_table)
                ?? throw new BusinessException($"第 {position} 个关联条件的左表别名 {join.left_table} 不存在");
            var rightTable = FindBinding(join.right_table)
                ?? throw new BusinessException($"第 {position} 个关联条件的右表别名 {join.right_table} 不存在");

            if (leftTable.Index == rightTable.Index)
            {
                // 自关联是合法需求，但必须用两个别名引用同一张表，否则两端指向同一行、条件恒真
                throw new BusinessException(
                    $"第 {position} 个关联条件的两端都是 {leftTable.UserAlias}；" +
                    "自关联请把同一张表写成两个不同的别名");
            }

            var leftColumn = FindColumn(leftTable.Schema, join.left_column)
                ?? throw new BusinessException(
                    $"第 {position} 个关联条件：表 {leftTable.Schema.table_name} 中没有字段 {join.left_column}");
            var rightColumn = FindColumn(rightTable.Schema, join.right_column)
                ?? throw new BusinessException(
                    $"第 {position} 个关联条件：表 {rightTable.Schema.table_name} 中没有字段 {join.right_column}");

            if (!BiIdentifier.IsJoinCompatible(leftColumn.data_type, rightColumn.data_type))
            {
                throw new BusinessException(
                    $"第 {position} 个关联条件的字段类型不匹配：" +
                    $"{leftTable.UserAlias}.{leftColumn.column_name}（{leftColumn.data_type}）与 " +
                    $"{rightTable.UserAlias}.{rightColumn.column_name}（{rightColumn.data_type}）");
            }

            // 归一 ON 两边的书写顺序（按表的声明顺序），让生成的 SQL 与「用户先写了哪一边」无关。
            // 等号两侧都是限定列引用，换个方向语义不变，只是读起来更稳定
            var left = new ResolvedColumn(leftTable, leftColumn);
            var right = new ResolvedColumn(rightTable, rightColumn);
            var (first, second) = leftTable.Index <= rightTable.Index ? (left, right) : (right, left);

            return new ValidatedJoin
            {
                LeftColumn = first,
                RightColumn = second,
                JoinKeyword = joinKeyword
            };
        }

        private SelectResult BuildSelectList()
        {
            var items = _request.select;
            if (items is null || items.Count == 0)
            {
                throw new BusinessException("请至少指定一个输出字段");
            }

            if (items.Count > _options.MaxSelectColumns)
            {
                throw new BusinessException($"输出字段过多（最多 {_options.MaxSelectColumns} 个）");
            }

            var result = new SelectResult();
            var parts = new List<string>(items.Count);
            // 输出别名会成为结果字典的 key，重名会让后面的悄悄覆盖前面的，必须拦住
            var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var position = i + 1;

                if (item is null || string.IsNullOrWhiteSpace(item.field))
                {
                    throw new BusinessException($"第 {position} 个输出字段为空");
                }

                var aggregate = NormalizeAggregate(item.aggregate, position);
                var field = item.field.Trim();

                ResolvedColumn? column = null;
                string expression;
                string defaultAlias;

                if (aggregate is null)
                {
                    column = ResolveColumn(field);
                    expression = column.Qualified;
                    defaultAlias = column.Column.column_name;
                    result.NonAggregateColumns.Add(column);
                }
                else
                {
                    result.HasAggregate = true;

                    if (field == "*")
                    {
                        if (aggregate != "count")
                        {
                            throw new BusinessException($"第 {position} 个输出字段：只有 count 允许用 * 作为字段");
                        }

                        expression = "COUNT(1)";
                        defaultAlias = "count";
                    }
                    else
                    {
                        column = ResolveColumn(field);

                        // 在 varchar 上做 sum/avg 是典型的口径错误（要么报错、要么算出让人困惑的结果），
                        // 这里直接拒掉；max/min/count_distinct 不限类型（日期取最大最小值是常见需求）
                        if (aggregate is "sum" or "avg" && !BiIdentifier.IsNumericType(column.Column.data_type))
                        {
                            throw new BusinessException(
                                $"第 {position} 个输出字段：{column.Column.column_name} 是 {column.Column.data_type} 类型，不能做 {aggregate} 聚合");
                        }

                        expression = aggregate == "count_distinct"
                            ? $"COUNT(DISTINCT {column.Qualified})"
                            : $"{AggregateSql[aggregate]}({column.Qualified})";
                        defaultAlias = $"{aggregate}_{column.Column.column_name}";
                    }
                }

                var alias = string.IsNullOrWhiteSpace(item.alias) ? defaultAlias : item.alias.Trim();
                if (!usedAliases.Add(alias))
                {
                    throw new BusinessException($"输出字段名 {alias} 重复，请为每个输出字段指定不同的别名");
                }

                parts.Add($"{expression} AS {BiIdentifier.Quote(alias)}");
                result.OutputColumns.Add(alias);
                result.Items.Add(new SelectItemResult
                {
                    Alias = alias,
                    Expression = expression,
                    IsAggregate = aggregate is not null
                });
            }

            result.Sql = string.Join(", ", parts);
            return result;
        }

        private string BuildWhere()
        {
            var filters = _request.where;
            if (filters is null || filters.Count == 0)
            {
                return string.Empty;
            }

            if (filters.Count > _options.MaxWhereConditions)
            {
                throw new BusinessException($"过滤条件过多（最多 {_options.MaxWhereConditions} 个）");
            }

            var parts = new List<string>(filters.Count);
            foreach (var filter in filters)
            {
                parts.Add(BuildCondition(filter));
            }

            return " WHERE " + string.Join(" AND ", parts);
        }

        private string BuildCondition(BiReportFilterDto? filter)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.field))
            {
                throw new BusinessException("过滤条件的字段不能为空");
            }

            var column = ResolveColumn(filter.field.Trim());
            var op = NormalizeOperator(filter.op);
            var qualified = column.Qualified;
            var dataType = column.Column.data_type;

            switch (op)
            {
                case "=":
                case "<>":
                case ">":
                case ">=":
                case "<":
                case "<=":
                {
                    var value = BiValueCoercion.CoerceScalar(filter.value, dataType, filter.field);
                    return $"{qualified} {ComparisonOperatorSql[op]} {AddParameter(_parameters, value)}";
                }

                case "like":
                case "not like":
                {
                    var pattern = BiValueCoercion.CoerceLikePattern(filter.value, filter.field);
                    return $"{qualified} {(op == "like" ? "LIKE" : "NOT LIKE")} {AddParameter(_parameters, pattern)}";
                }

                case "in":
                case "not in":
                {
                    // 空数组不静默短路成 1=0：那会让「参数没传对」表现成「本该有数据却查不到」
                    var values = BiValueCoercion.CoerceArray(
                        filter.value, dataType, filter.field, 1, _options.MaxInValues);

                    var names = new string[values.Count];
                    for (var i = 0; i < values.Count; i++)
                    {
                        names[i] = AddParameter(_parameters, values[i]);
                    }

                    return $"{qualified} {(op == "in" ? "IN" : "NOT IN")} ({string.Join(", ", names)})";
                }

                case "between":
                {
                    var values = BiValueCoercion.CoerceArray(filter.value, dataType, filter.field, 2, 2);
                    var lower = AddParameter(_parameters, values[0]);
                    var upper = AddParameter(_parameters, values[1]);
                    return $"{qualified} BETWEEN {lower} AND {upper}";
                }

                case "is null":
                    EnsureNoValue(filter);
                    return $"{qualified} IS NULL";

                case "is not null":
                    EnsureNoValue(filter);
                    return $"{qualified} IS NOT NULL";

                default:
                    throw new BusinessException($"不支持的运算符：{filter.op}");
            }
        }

        /// <summary>运算符大小写与多余空白不敏感：<c>IS  Not  NULL</c> 也认。</summary>
        private static string NormalizeOperator(string? op)
        {
            if (string.IsNullOrWhiteSpace(op))
            {
                return "=";
            }

            var normalized = string
                .Join(' ', op.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();

            // SQL 里 != 与 &lt;&gt; 等价，归一成一个，后面就不必写两遍
            return normalized == "!=" ? "<>" : normalized;
        }

        private static string? NormalizeAggregate(string? aggregate, int position)
        {
            if (string.IsNullOrWhiteSpace(aggregate))
            {
                return null;
            }

            var normalized = aggregate.Trim().ToLowerInvariant();
            return AggregateSql.ContainsKey(normalized)
                ? normalized
                : throw new BusinessException(
                    $"第 {position} 个输出字段的聚合函数不支持：{aggregate}（只支持 count / count_distinct / sum / avg / max / min）");
        }

        /// <summary>
        /// 生成 GROUP BY。未显式指定但 SELECT 里混了聚合列与非聚合列时，用非聚合列自动补全
        /// （「按这几列汇总」的直觉行为）。
        /// </summary>
        private string BuildGroupBy()
        {
            var items = _request.group_by;
            if (items is { Count: > 0 })
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        throw new BusinessException("分组字段不能为空");
                    }

                    var expression = ResolveOutputReference(item, isGroupBy: true);
                    if (!_groupByExpressions.Contains(expression, StringComparer.OrdinalIgnoreCase))
                    {
                        _groupByExpressions.Add(expression);
                    }
                }
            }
            else if (_select.HasAggregate && _select.NonAggregateColumns.Count > 0)
            {
                foreach (var column in _select.NonAggregateColumns)
                {
                    if (!_groupByExpressions.Contains(column.Qualified, StringComparer.OrdinalIgnoreCase))
                    {
                        _groupByExpressions.Add(column.Qualified);
                    }
                }
            }

            if (_groupByExpressions.Count == 0)
            {
                return string.Empty;
            }

            // ONLY_FULL_GROUP_BY（MySQL 5.7 默认开启，本项目实例也是）下，SELECT 里的非聚合列
            // 必须出现在 GROUP BY 中，否则数据库直接报 1055「not in GROUP BY clause」。
            // 在这之前拦下来，错误信息才说得出「该加分组还是该加聚合」。
            foreach (var column in _select.NonAggregateColumns)
            {
                if (!_groupByExpressions.Contains(column.Qualified, StringComparer.OrdinalIgnoreCase))
                {
                    throw new BusinessException(
                        $"字段 {column.Column.column_name} 既没有聚合也没有出现在分组字段中，" +
                        "请把它加入 group_by 或改用聚合函数");
                }
            }

            return " GROUP BY " + string.Join(", ", _groupByExpressions);
        }

        private string BuildOrderBy()
        {
            var items = _request.order_by;
            var expressions = new List<string>();
            var requested = items is { Count: > 0 };

            if (requested)
            {
                if (items!.Count > _options.MaxOrderBy)
                {
                    throw new BusinessException($"排序字段过多（最多 {_options.MaxOrderBy} 个）");
                }

                foreach (var item in items)
                {
                    if (item is null || string.IsNullOrWhiteSpace(item.field))
                    {
                        throw new BusinessException("排序字段不能为空");
                    }

                    expressions.Add($"{ResolveOutputReference(item.field, isGroupBy: false)} {(item.desc ? "DESC" : "ASC")}");
                }

                if (_groupByExpressions.Count > 0)
                {
                    // 有分组时，排序字段必须是分组字段或聚合结果，否则 ONLY_FULL_GROUP_BY 报 1055
                    var allowed = new HashSet<string>(_groupByExpressions, StringComparer.OrdinalIgnoreCase);
                    allowed.UnionWith(_select.Items.Where(item => item.IsAggregate).Select(item => item.Expression));

                    foreach (var item in items)
                    {
                        var expression = ResolveOutputReference(item!.field, isGroupBy: false);
                        if (!allowed.Contains(expression))
                        {
                            throw new BusinessException(
                                $"排序字段 {item.field} 既不是分组字段也不是聚合结果，分组查询下无法按其排序");
                        }
                    }
                }
            }
            else if (_groupByExpressions.Count > 0)
            {
                // 分组查询按分组字段兜底排序，保证翻页顺序稳定
                expressions.AddRange(_groupByExpressions.Select(expression => expression + " ASC"));
            }
            else
            {
                // 明细查询按主键兜底：没有 ORDER BY 时 MySQL 不保证行序，翻页可能重复或漏行
                var idColumn = FindColumn(_bindings[0].Schema, "id");
                if (idColumn is not null)
                {
                    // 走 ResolvedColumn 而不是手拼字符串：它会把这个列登记到表绑定上，
                    // 分表的内层 SELECT 才会带上排序用的 id
                    expressions.Add($"{new ResolvedColumn(_bindings[0], idColumn).Qualified} ASC");
                }
            }

            return expressions.Count == 0 ? string.Empty : " ORDER BY " + string.Join(", ", expressions);
        }

        /// <summary>
        /// 按 WHERE 里对分表日期字段的过滤，裁剪本次实际要扫的物理分表。
        /// <para>
        /// 只做「月份级」裁剪，且刻意偏保守：<c>&gt;</c> / <c>&lt;</c> 也把边界所在月保留进来，
        /// 因为日期可能带时分秒。裁剪只影响扫哪些物理表，不改变查询语义——真正的精确过滤仍然由
        /// WHERE 子句执行。没有任何可用日期条件时扫描全部分表。
        /// </para>
        /// <para>
        /// 注意这里的 <see cref="ResolveColumn"/> 调用发生在 <see cref="BuildWhere"/> 之后：
        /// 非法字段/运算符已经在 WHERE 构建时给出准确错误，这里即使解析失败也只是保守地不裁剪。
        /// </para>
        /// </summary>
        private void SelectShards()
        {
            foreach (var binding in _bindings)
            {
                var shards = binding.Schema.shards;
                if (shards is not { Count: > 0 })
                {
                    continue;
                }

                if (shards.Count == 1 || string.IsNullOrWhiteSpace(binding.Schema.shard_date_column))
                {
                    binding.SelectedShards = shards;
                    continue;
                }

                var dateColumn = FindColumn(binding.Schema, binding.Schema.shard_date_column);
                if (dateColumn is null || !BiIdentifier.IsDateType(dateColumn.data_type))
                {
                    binding.SelectedShards = shards;
                    continue;
                }

                int? minMonth = null;
                int? maxMonth = null;
                HashSet<int>? includedMonths = null;

                foreach (var filter in _request.where ?? new List<BiReportFilterDto>())
                {
                    if (filter is null || string.IsNullOrWhiteSpace(filter.field))
                    {
                        continue;
                    }

                    ResolvedColumn resolved;
                    try
                    {
                        resolved = ResolveColumn(filter.field.Trim());
                    }
                    catch (BusinessException)
                    {
                        continue;
                    }

                    if (resolved.Table.Index != binding.Index ||
                        !string.Equals(resolved.Column.column_name, dateColumn.column_name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    switch (NormalizeOperator(filter.op))
                    {
                        case "=":
                        {
                            var month = MonthIndexOf(dateColumn, filter);
                            minMonth = MaxMonth(minMonth, month);
                            maxMonth = MinMonth(maxMonth, month);
                            break;
                        }

                        case ">":
                        case ">=":
                            minMonth = MaxMonth(minMonth, MonthIndexOf(dateColumn, filter));
                            break;

                        case "<":
                        case "<=":
                            maxMonth = MinMonth(maxMonth, MonthIndexOf(dateColumn, filter));
                            break;

                        case "between":
                        {
                            var values = BiValueCoercion.CoerceArray(
                                filter.value, dateColumn.data_type, filter.field, 2, 2);
                            minMonth = MaxMonth(minMonth, MonthIndexOf(values[0]));
                            maxMonth = MinMonth(maxMonth, MonthIndexOf(values[1]));
                            break;
                        }

                        case "in":
                        {
                            var values = BiValueCoercion.CoerceArray(
                                filter.value, dateColumn.data_type, filter.field, 1, _options.MaxInValues);
                            var months = values.Select(MonthIndexOf).ToHashSet();
                            includedMonths = includedMonths is null
                                ? months
                                : includedMonths.Intersect(months).ToHashSet();
                            break;
                        }
                    }
                }

                var selected = shards.Where(shard =>
                {
                    var month = MonthIndexOf(shard.period_start);
                    return (minMonth is null || month >= minMonth) &&
                           (maxMonth is null || month <= maxMonth) &&
                           (includedMonths is null || includedMonths.Contains(month));
                }).ToList();

                binding.SelectedShards = selected;
                binding.EmptyShardRange = selected.Count == 0;
            }
        }

        private static int MonthIndexOf(BiReportColumnSchemaDto dateColumn, BiReportFilterDto filter) =>
            MonthIndexOf(BiValueCoercion.CoerceScalar(filter.value, dateColumn.data_type, filter.field));

        private static int MonthIndexOf(object value) => value is DateTime date
            ? date.Year * 12 + date.Month - 1
            : throw new BusinessException("分表日期字段的过滤值不是日期类型");

        private static int? MaxMonth(int? current, int value) =>
            current is null ? value : Math.Max(current.Value, value);

        private static int? MinMonth(int? current, int value) =>
            current is null ? value : Math.Min(current.Value, value);

        /// <summary>
        /// 解析 GROUP BY / ORDER BY 里的一个引用：先按输出别名匹配，再当作字段引用解析。
        /// <para>
        /// 别名优先与 MySQL 自身的规则一致——它解析不带表前缀的名字时也是先看 SELECT 列表。
        /// BI 场景下「按我起的列名分组/排序」也是最常见的写法。
        /// </para>
        /// </summary>
        private string ResolveOutputReference(string reference, bool isGroupBy)
        {
            var text = reference.Trim();

            var matched = _select.Items.FirstOrDefault(item =>
                string.Equals(item.Alias, text, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                if (isGroupBy && matched.IsAggregate)
                {
                    throw new BusinessException($"聚合字段 {text} 不能作为分组字段");
                }

                return matched.Expression;
            }

            return ResolveColumn(text).Qualified;
        }

        private TableBinding? FindBinding(string? alias) =>
            string.IsNullOrWhiteSpace(alias)
                ? null
                : _bindings.FirstOrDefault(binding =>
                    string.Equals(binding.UserAlias, alias.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 解析字段引用：<c>表别名.列名</c>，或单表/无歧义时只写列名。
        /// <para>
        /// 比较一律用 <see cref="StringComparison.OrdinalIgnoreCase"/>：MySQL 的列名比较本就不区分大小写
        /// （本实例 <c>lower_case_table_names = 1</c>），用默认的序数比较会把 <c>Cao_Capacity</c>
        /// 判成「列不存在」。输出则一律用元数据里的规范名，所以像 <c>caO_capacity</c>
        /// 这种大小写混排的列名永远按库里的真实拼写出现。
        /// </para>
        /// </summary>
        private ResolvedColumn ResolveColumn(string field)
        {
            var text = field.Trim();
            var dot = text.IndexOf('.');

            if (dot >= 0)
            {
                var alias = text[..dot].Trim();
                var columnName = text[(dot + 1)..].Trim();

                if (alias.Length == 0 || columnName.Length == 0)
                {
                    throw new BusinessException($"字段引用格式不正确：{field}（应写成 表别名.列名）");
                }

                var table = FindBinding(alias)
                    ?? throw new BusinessException(
                        $"表别名 {alias} 不存在（可用别名：{string.Join("、", _bindings.Select(b => b.UserAlias))}）");

                var column = FindColumn(table.Schema, columnName)
                    ?? throw new BusinessException($"表 {table.Schema.table_name} 中没有字段 {columnName}");

                return new ResolvedColumn(table, column);
            }

            // 不带表前缀：只在唯一命中时才接受，否则没法知道用户指的是哪张表
            var matches = _bindings
                .Select(binding => (Binding: binding, Column: FindColumn(binding.Schema, text)))
                .Where(match => match.Column is not null)
                .ToList();

            return matches.Count switch
            {
                0 => throw new BusinessException($"字段 {text} 不存在"),
                > 1 => throw new BusinessException(
                    $"字段 {text} 在多张表中都存在，请写成 表别名.列名 指定"),
                _ => new ResolvedColumn(matches[0].Binding, matches[0].Column!)
            };
        }

        private static BiReportColumnSchemaDto? FindColumn(BiReportTableSchemaDto table, string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return null;
            }

            var name = columnName.Trim();
            return table.columns.FirstOrDefault(column =>
                string.Equals(column.column_name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// <c>is null</c> / <c>is not null</c> 不该带值。传了值却静默忽略，
        /// 会让「我明明填了值」变成永远查不出数据的幽灵 bug，所以直接拒绝。
        /// （显式的 <c>"value": null</c> 放行——那是客户端把可空字段原样序列化出来的常态。）
        /// </summary>
        private static void EnsureNoValue(BiReportFilterDto filter)
        {
            if (filter.value is null || filter.value.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                return;
            }

            throw new BusinessException($"运算符 {filter.op} 不接受比较值");
        }
    }
}
