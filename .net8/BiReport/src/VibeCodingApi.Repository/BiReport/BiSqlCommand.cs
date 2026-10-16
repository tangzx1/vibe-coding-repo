using SqlSugar;

namespace VibeCodingApi.Repository.BiReport;

/// <summary>
/// <see cref="BiReportSqlBuilder"/> 的编译产物：一条可直接交给 SqlSugar 执行的手工 SQL。
/// <para>
/// 之所以把 SQL 文本与参数一起返回、而不是直接执行：编译是纯函数，可以在没有数据库的情况下
/// 断言生成的 SQL 与参数，安全相关的用例（注入、越权引用敏感列）才能固化进单测。
/// </para>
/// </summary>
public sealed class BiSqlCommand
{
    /// <summary>SQL 文本。表名/列名/别名等结构性片段已按白名单与正则校验后拼入，值全部是占位符。</summary>
    public string Sql { get; init; } = string.Empty;

    /// <summary>与 <see cref="Sql"/> 中占位符一一对应的参数。</summary>
    public List<SugarParameter> Parameters { get; init; } = new();

    /// <summary>输出列名，顺序与 SELECT 子句一致。</summary>
    public List<string> Columns { get; init; } = new();

    /// <summary>实际生效的页码（已按「最小 1」夹紧）。</summary>
    public int PageIndex { get; init; }

    /// <summary>实际生效的每页条数（已按配置上限夹紧）。</summary>
    public int PageSize { get; init; }
}
