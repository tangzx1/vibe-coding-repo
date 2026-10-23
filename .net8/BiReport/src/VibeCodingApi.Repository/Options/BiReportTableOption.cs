namespace VibeCodingApi.Repository.Options;

/// <summary>
/// BI 报表可查询表的白名单项，对应 appsettings.json 中 <c>BiQuery.Tables[]</c> 的一个元素。
/// <para>
/// 只有出现在白名单里的表才会进入元数据接口，前端也就只能查到这些表。字段名、类型、注释
/// 不在这里配，而是从数据库的 <c>INFORMATION_SCHEMA</c> 自动读取，避免配一份就得手工同步一份。
/// </para>
/// </summary>
public class BiReportTableOption
{
    /// <summary>分表名默认使用的日期格式（对应 <c>load_shovel_task_record_20250501</c> 这种命名）。</summary>
    public const string DefaultShardNameDateFormat = "yyyyMMdd";

    /// <summary>
    /// 分表名日期格式的白名单。只放行「年 + 月」明确的格式，
    /// 因为服务端要据此把物理表归到某个月，用于按日期裁剪分表。
    /// </summary>
    public static readonly string[] SupportedShardNameDateFormats =
    {
        "yyyyMMdd", "yyyyMM", "yyyy_MM_dd", "yyyy_MM", "yyyy-MM-dd", "yyyy-MM", "yyyy.MM.dd", "yyyy.MM"
    };

    /// <summary>
    /// 数据库表名。与 <c>INFORMATION_SCHEMA</c> 中的名称比对时不区分大小写，
    /// 但输出给前端的永远是数据库里的规范名。
    /// <para>
    /// 对按月分表来说，这里是<b>逻辑表名</b>（如 <c>load_shovel_task_record</c>），
    /// 前端请求里也用它；真正的物理分表由 <see cref="ShardTablePrefix"/> 发现。
    /// </para>
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>前端显示名；为空时退化为数据库里的表注释。</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 物理库名。为空时使用当前连接（<c>DATABASE()</c>）对应的库。
    /// <para>
    /// ⚠ 跨库表必须与默认连接在<b>同一个 MySQL 实例</b>上：BI 查询始终在默认连接上执行，
    /// 依赖 MySQL 的 <c>库名.表名</c> 语法做跨库 join。不同实例的两张表没法在一条 SQL 里关联。
    /// </para>
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// 分表前缀，如 <c>load_shovel_task_record_</c>。非空表示 <see cref="Name"/> 是一张
    /// <b>按月分表</b>的逻辑表：服务端会在 <see cref="Database"/> 里发现所有
    /// 「前缀 + 日期后缀」的物理表，并把它们 UNION ALL 成一个可参与 join 的派生表。
    /// </summary>
    public string? ShardTablePrefix { get; set; }

    /// <summary>
    /// 分表名里的日期后缀格式，默认 <see cref="DefaultShardNameDateFormat"/>。
    /// 例如物理表 <c>load_shovel_task_record_20250501</c> 在前缀
    /// <c>load_shovel_task_record_</c> 下的后缀是 <c>20250501</c>。
    /// 只支持 <see cref="SupportedShardNameDateFormats"/> 里的格式。
    /// </summary>
    public string ShardNameDateFormat { get; set; } = DefaultShardNameDateFormat;

    /// <summary>
    /// 分表日期字段（数据库列名），如 <c>working_date</c>。
    /// <para>
    /// 查询里对该字段的日期过滤（<c>=</c> / <c>&gt;</c> / <c>&gt;=</c> / <c>&lt;</c> / <c>&lt;=</c> /
    /// <c>between</c> / <c>in</c>）会被用来裁剪本次要扫的物理分表；没有这类过滤时扫描全部分表。
    /// 留空或字段不存在时不做裁剪（仍可查询，只是会扫全部分表）。
    /// </para>
    /// </summary>
    public string? ShardDateColumn { get; set; }

    /// <summary>
    /// 额外要排除的列。命中的列不会出现在元数据里，前端根本不知道它们存在。
    /// <para>
    /// 注意：这是「在 <see cref="BiReportOptions.ForcedExcludeColumns"/> 与
    /// <see cref="BiReportOptions.SensitiveColumnKeywords"/> 之外」的补充排除，
    /// 不是唯一防线——敏感列靠代码兜底，不靠这里记得写。
    /// </para>
    /// </summary>
    public List<string> ExcludeColumns { get; set; } = new();

    /// <summary>
    /// 非空时进入「白名单模式」：只暴露这里面列出的列，其余一律排除。
    /// 适合宽表只放开少数几列的场景；为空表示按排除规则放开全部列。
    /// </summary>
    public List<string>? IncludeColumns { get; set; }

    /// <summary>列显示名覆盖：列名 → 显示名。未覆盖的列用数据库里的列注释。</summary>
    public Dictionary<string, string> ColumnDisplayNames { get; set; } = new();
}
