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
    /// <summary>
    /// 数据库表名。与 <c>INFORMATION_SCHEMA</c> 中的名称比对时不区分大小写，
    /// 但输出给前端的永远是数据库里的规范名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>前端显示名；为空时退化为数据库里的表注释。</summary>
    public string? DisplayName { get; set; }

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
