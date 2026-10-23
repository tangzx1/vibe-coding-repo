using System.Text.Json.Serialization;

namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// BI 报表可用元数据：有哪些表、每张表有哪些字段。
/// 前端据此渲染查询设计器，用户在里面拖表、选字段、连关联、命名别名。
/// <para>
/// <b>这里不返回关联关系</b>：表与表怎么关联由用户在查询请求的 <c>joins</c> 里现场指定，
/// 服务端不做任何预置也不推荐。前端画关联连线时，两端可选的字段就是各表已返回的字段，
/// 服务端在生成 SQL 时会拒绝类型不同族的组合（见 <see cref="BiReportColumnSchemaDto"/>）。
/// </para>
/// <para>
/// 一个接口整包返回（而不是拆成表/字段两个）：白名单当前只有两张表、约 134 列，
/// 含中文注释也就十几 KB，拆开反而让前端要合并两次响应、任一次失败就拿到半截模型。
/// 白名单增长到几十张表时再拆分成 <c>GET /api/bi-report/tables</c> 与
/// <c>GET /api/bi-report/tables/{name}/columns</c>。
/// </para>
/// <para>
/// 敏感列（如 <c>cors_password</c>）在构建元数据时就被剔除，这里不会出现，
/// 前端也就无从拼出对它们的查询。
/// </para>
/// </summary>
public class BiReportSchemaDto
{
    /// <summary>元数据的生成时间，前端可据此展示「表结构版本」。</summary>
    public DateTime generated_at { get; set; }

    /// <summary>可查询的表。</summary>
    public List<BiReportTableSchemaDto> tables { get; set; } = new();
}

/// <summary>一张可查询表的元数据。</summary>
public class BiReportTableSchemaDto
{
    /// <summary>数据库里的规范表名，请求里的 <c>table</c> 就填这个。</summary>
    public string table_name { get; set; } = string.Empty;

    /// <summary>前端显示名（配置的显示名，未配时用表注释）。</summary>
    public string display_name { get; set; } = string.Empty;

    /// <summary>数据库里的表注释。</summary>
    public string? table_comment { get; set; }

    /// <summary>字段列表，按数据库里的列顺序排列。</summary>
    public List<BiReportColumnSchemaDto> columns { get; set; } = new();

    /// <summary>
    /// 物理库名；null 表示当前连接默认库。跨库表生成 SQL 时会拼成 <c>`库`.`表`</c>。
    /// <para>仅服务端编译 SQL 使用，不下发给前端：前端的请求只认识逻辑表名。</para>
    /// </summary>
    [JsonIgnore]
    public string? database { get; set; }

    /// <summary>
    /// 按月分表的物理表清单，按月份升序；空表示普通表（直接引用 <see cref="table_name"/>）。
    /// <para>仅服务端编译 SQL 使用，不下发给前端。</para>
    /// </summary>
    [JsonIgnore]
    public List<BiReportShardSchemaDto> shards { get; set; } = new();

    /// <summary>
    /// 分表日期字段（数据库列名），用于按 WHERE 里的日期过滤裁剪要扫的物理分表；
    /// 非分表或未配置时为 null。仅服务端使用，不下发给前端。
    /// </summary>
    [JsonIgnore]
    public string? shard_date_column { get; set; }
}

/// <summary>
/// 一张物理分表的元数据（仅服务端使用，不参与接口序列化）。
/// </summary>
public class BiReportShardSchemaDto
{
    /// <summary>物理表名，如 <c>load_shovel_task_record_20250501</c>。</summary>
    public string table_name { get; set; } = string.Empty;

    /// <summary>该分表覆盖月份的 1 日 00:00:00，用于和查询的日期范围做交集。</summary>
    public DateTime period_start { get; set; }
}

/// <summary>
/// 一个可查询字段的元数据。
/// <para>
/// 三个类型标志（<see cref="is_numeric"/> / <see cref="is_date"/> / <see cref="is_string"/>）
/// 同时也是 join 的兼容性判据：服务端只允许<b>同族</b>的两个字段互相关联。
/// 前端让用户连关联时，目标字段的候选集就是其它表上同族的字段——把不兼容的选项灰掉，
/// 比让用户提交后被 400 打回来体验好。
/// </para>
/// </summary>
public class BiReportColumnSchemaDto
{
    /// <summary>数据库里的规范列名，请求里引用列时用它。</summary>
    public string column_name { get; set; } = string.Empty;

    /// <summary>前端显示名（配置的显示名，未配时用列注释，都没有时退化为列名）。</summary>
    public string display_name { get; set; } = string.Empty;

    /// <summary>MySQL 的 DATA_TYPE，如 <c>varchar</c> / <c>int</c> / <c>datetime</c> / <c>decimal</c>。</summary>
    public string data_type { get; set; } = string.Empty;

    /// <summary>是否允许为空。</summary>
    public bool is_nullable { get; set; }

    /// <summary>数据库里的列注释。</summary>
    public string? column_comment { get; set; }

    /// <summary>
    /// 是否数值类型。前端据此决定聚合下拉框里是否放 <c>sum</c> / <c>avg</c>。
    /// <para>
    /// 没有单独的「可否聚合」标志：<c>count</c> / <c>count_distinct</c> / <c>max</c> / <c>min</c>
    /// 对任意类型的列都成立，所以「可聚合」恒为真。三个类型标志已经足够推出可用聚合函数全集。
    /// </para>
    /// </summary>
    public bool is_numeric { get; set; }

    /// <summary>是否日期时间类型。</summary>
    public bool is_date { get; set; }

    /// <summary>是否字符串类型。</summary>
    public bool is_string { get; set; }
}
