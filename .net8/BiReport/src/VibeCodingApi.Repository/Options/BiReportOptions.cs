namespace VibeCodingApi.Repository.Options;

/// <summary>
/// BI 报表动态查询的配置（appsettings.json 的 <c>BiQuery</c> 节）：可查询表白名单 + 各项限额。
/// <para>
/// 所有限额都有默认值，配置里不用全写；但 <see cref="Tables"/> 必须显式配置——白名单为空时
/// 注册阶段就会抛异常（宁可启动失败，也不要上线一个「什么表都查不到」的模块）。
/// </para>
/// </summary>
public class BiReportOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "BiQuery";

    /// <summary>模块总开关。为 false 时接口仍然存在，但任何查询都会返回业务错误。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>默认每页条数（请求里 page_size &lt; 1 时用它）。</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>每页条数上限，超过则截断。</summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>单次查询最多能选多少输出列。</summary>
    public int MaxSelectColumns { get; set; } = 100;

    /// <summary>单次查询最多能关联多少张表。</summary>
    public int MaxJoinTables { get; set; } = 8;

    /// <summary>单次查询最多多少个过滤条件。</summary>
    public int MaxWhereConditions { get; set; } = 30;

    /// <summary>单个 <c>in</c> 条件最多多少个值。</summary>
    public int MaxInValues { get; set; } = 1000;

    /// <summary>单次查询最多多少个排序字段。</summary>
    public int MaxOrderBy { get; set; } = 5;

    /// <summary>导出最多多少行。</summary>
    public int ExportMaxRows { get; set; } = 20000;

    /// <summary>导出最多多少单元格（行 × 列），防「列多行少」把内存打爆。</summary>
    public int MaxExportCells { get; set; } = 500_000;

    /// <summary>
    /// 导出超过 <see cref="ExportMaxRows"/> 时的行为：false（默认）直接报错让调用方加筛选条件；
    /// true 则截断到上限并在工作簿里标注「数据已截断」。
    /// </summary>
    public bool ExportTruncateWhenExceed { get; set; }

    /// <summary>单条 SQL 的执行时间上限（毫秒），以 MySQL 优化器提示的形式下发；0 表示不限制。</summary>
    public int QueryTimeoutMs { get; set; } = 30_000;

    /// <summary>元数据缓存有效期（秒）。DDL 变更后最长滞后这么久，也可以调刷新接口立即生效。</summary>
    public int SchemaCacheSeconds { get; set; } = 300;

    /// <summary>
    /// 单张按月分表的逻辑表最多合并多少个物理分表。超过则跳过该表并告警，
    /// 防止前缀配错（比如把整个库的表都匹配进来）生成超大 SQL。
    /// 按月分表 240 张约等于 20 年。
    /// </summary>
    public int MaxShardsPerTable { get; set; } = 240;

    /// <summary>是否允许调用元数据刷新接口。无鉴权部署时可关掉。</summary>
    public bool MetadataRefreshEnabled { get; set; } = true;

    /// <summary>允许 BI 查询的表（白名单）。</summary>
    public List<BiReportTableOption> Tables { get; set; } = new();

    /// <summary>
    /// 代码内置的强制排除列：即使配置里没写也会被剔除，且不受 <c>include_columns</c> 影响。
    /// <para>
    /// 数据库里的 <c>cors_password</c>（CORS 账号密码）、<c>cors_name</c>、<c>iot_ip</c> 属于
    /// 一旦进入元数据就等于对外公开的字段，所以用「默认拒绝」而不是「记得配置」。
    /// </para>
    /// </summary>
    public static readonly string[] ForcedExcludeColumns = { "cors_password", "cors_name", "iot_ip" };

    /// <summary>
    /// 列名命中这些关键字（不区分大小写、包含即中）时默认排除，
    /// 除非该表用 <see cref="BiReportTableOption.IncludeColumns"/> 显式放行。
    /// <para>
    /// 这是给「将来新增的敏感字段」兜底的：不指望每个新字段都有人记得加进排除清单。
    /// </para>
    /// </summary>
    public static readonly string[] SensitiveColumnKeywords =
        { "password", "passwd", "pwd", "secret", "token", "private_key" };
}
