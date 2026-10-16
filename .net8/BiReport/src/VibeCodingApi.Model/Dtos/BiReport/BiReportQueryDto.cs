using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// BI 报表即席查询的请求体：前端拼好「选哪些表、怎么关联、输出哪些列、怎么过滤」后整体提交。
/// <para>
/// 本模块<b>不落库报表定义</b>，每次查询都是一次完整自描述请求，服务端不保存任何状态。
/// </para>
/// <para>
/// 字段名沿用 snake_case，与实体及现有接口保持同一套命名（见 <see cref="TruckQueryDto"/> 的说明）。
/// 命名空间定为 <c>VibeCodingApi.Model.Dtos</c>，不带子目录段。
/// </para>
/// </summary>
public class BiReportQueryDto
{
    /// <summary>
    /// 参与查询的表。<b>第一张是主表</b>（join 连通性的起点），其它表都要能通过 <see cref="joins"/> 连到它。
    /// </summary>
    [MinLength(1, ErrorMessage = "请至少指定一张查询表")]
    public List<BiReportTableRefDto> tables { get; set; } = new();

    /// <summary>
    /// 表间关联条件。数量必须恰好是 <c>tables.Count - 1</c>，且整体构成一棵连通树
    /// （少一条说明有表没接上、会形成笛卡尔积，多一条说明存在冗余或环形连接）。
    /// <para>
    /// 单表查询时留空即可。
    /// </para>
    /// </summary>
    public List<BiReportJoinDto> joins { get; set; } = new();

    /// <summary>输出列。<c>alias</c> 就是结果里该列的名字，也是导出 Excel 的表头。</summary>
    [MinLength(1, ErrorMessage = "请至少指定一个输出字段")]
    public List<BiReportSelectDto> select { get; set; } = new();

    /// <summary>过滤条件，多个条件之间是「与」的关系。v1 不支持嵌套「或」。</summary>
    public List<BiReportFilterDto>? where { get; set; }

    /// <summary>
    /// 分组字段（列引用或 select 里的别名）。
    /// <para>
    /// 留空且 select 中同时存在聚合列与非聚合列时，服务端会用非聚合列自动补全；
    /// 只在 select 全是聚合列时才真的不分组。
    /// </para>
    /// </summary>
    public List<string>? group_by { get; set; }

    /// <summary>排序字段（列引用或 select 里的别名）。未指定且无分组时，服务端会按主表主键兜底排序，保证翻页稳定。</summary>
    public List<BiReportOrderByDto>? order_by { get; set; }

    /// <summary>页码，从 1 开始。小于 1 时按 1 处理。</summary>
    public int page_index { get; set; } = 1;

    /// <summary>每页条数，超过配置上限时被截断。</summary>
    public int page_size { get; set; } = 20;

    /// <summary>
    /// 是否统计总记录数。为 false 时响应里的 <c>total</c> 返回 null，可省掉一次 count 查询
    /// （大表且不需要总页数时用）。
    /// </summary>
    public bool need_total { get; set; } = true;
}

/// <summary>参与查询的一张表。</summary>
public class BiReportTableRefDto
{
    /// <summary>表名，必须在元数据接口返回的表清单里。</summary>
    [Required(ErrorMessage = "表名不能为空")]
    public string table { get; set; } = string.Empty;

    /// <summary>
    /// 表别名，供 <see cref="BiReportSelectDto.field"/> 等地方用 <c>别名.列名</c> 引用。留空时默认等于表名。
    /// <para>
    /// 它只是一个<b>引用键</b>：生成 SQL 时会被替换成服务端编排的 t0/t1/t2…，
    /// 用户填的内容一个字符都不会进入 SQL 文本。
    /// </para>
    /// </summary>
    public string? alias { get; set; }
}

/// <summary>一个表间关联条件。</summary>
public class BiReportJoinDto
{
    /// <summary>join 类型：<c>inner</c> 或 <c>left</c>。其它值会被拒绝。</summary>
    public string join_type { get; set; } = "inner";

    /// <summary>左表的表别名。</summary>
    [Required(ErrorMessage = "关联的左表不能为空")]
    public string left_table { get; set; } = string.Empty;

    /// <summary>左表参与关联的列名。</summary>
    [Required(ErrorMessage = "关联的左字段不能为空")]
    public string left_column { get; set; } = string.Empty;

    /// <summary>右表的表别名。</summary>
    [Required(ErrorMessage = "关联的右表不能为空")]
    public string right_table { get; set; } = string.Empty;

    /// <summary>右表参与关联的列名。</summary>
    [Required(ErrorMessage = "关联的右字段不能为空")]
    public string right_column { get; set; } = string.Empty;
}

/// <summary>一个输出列。</summary>
public class BiReportSelectDto
{
    /// <summary>
    /// 字段引用：<c>表别名.列名</c>；单表查询时可以只写列名。
    /// 聚合函数为 <c>count</c> 时允许写 <c>*</c>（即 <c>COUNT(1)</c>）。
    /// </summary>
    [Required(ErrorMessage = "输出字段不能为空")]
    public string field { get; set; } = string.Empty;

    /// <summary>
    /// 聚合函数：<c>count</c> / <c>count_distinct</c> / <c>sum</c> / <c>avg</c> / <c>max</c> / <c>min</c>。
    /// 留空表示直接输出该列。其它值会被拒绝。
    /// </summary>
    public string? aggregate { get; set; }

    /// <summary>
    /// 输出列名（结果对象里的 key）。留空时服务端按 <c>{聚合}_{列名}</c>（无聚合时就是列名）自动生成。
    /// 允许中文；同一查询内不能重复。
    /// </summary>
    public string? alias { get; set; }
}

/// <summary>一个过滤条件。</summary>
public class BiReportFilterDto
{
    /// <summary>字段引用：<c>表别名.列名</c>；单表查询时可以只写列名。</summary>
    [Required(ErrorMessage = "过滤字段不能为空")]
    public string field { get; set; } = string.Empty;

    /// <summary>
    /// 运算符：<c>=</c> <c>!=</c> <c>&gt;</c> <c>&gt;=</c> <c>&lt;</c> <c>&lt;=</c>
    /// <c>like</c> <c>not like</c> <c>in</c> <c>not in</c> <c>between</c> <c>is null</c> <c>is not null</c>。
    /// 大小写与多余空格不敏感。
    /// </summary>
    public string op { get; set; } = "=";

    /// <summary>
    /// 比较值。标量直接写（如 <c>"value": 1</c>），<c>in</c> / <c>between</c> 写数组；
    /// <c>is null</c> / <c>is not null</c> <b>不能</b>带这个字段。
    /// <para>
    /// 用 <see cref="JsonElement"/> 而不是 <c>object</c>：后者会绑定成装箱的 JsonElement，
    /// 处理起来更绕，而且无法区分「没传」与「传了 null」——这两种情况在
    /// <c>is null</c> 之外都应报错，静默当成同一回事会变成「传了值却查不出数据」的幽灵 bug。
    /// </para>
    /// </summary>
    public JsonElement? value { get; set; }
}

/// <summary>一个排序字段。</summary>
public class BiReportOrderByDto
{
    /// <summary>字段引用，或 select 里已定义的别名。</summary>
    [Required(ErrorMessage = "排序字段不能为空")]
    public string field { get; set; } = string.Empty;

    /// <summary>是否倒序。</summary>
    public bool desc { get; set; }
}
