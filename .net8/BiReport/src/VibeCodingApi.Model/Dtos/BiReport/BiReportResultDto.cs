namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// BI 报表查询结果。
/// <para>
/// 不继承 <c>Common.Models.PagedResult&lt;T&gt;</c>：<c>VibeCodingApi.Model</c> 不引用
/// <c>VibeCodingApi.Common</c>（Model 是最底层，无任何项目引用），所以字段在这里重列一遍。
/// 字段名与 <c>PagedResult</c> 保持一致，读起来是同一套东西。
/// </para>
/// </summary>
public class BiReportResultDto
{
    /// <summary>
    /// 输出列名，顺序与请求里 <see cref="BiReportQueryDto.select"/> 的顺序一致。
    /// <para>
    /// 之所以显式回传：<see cref="items"/> 里是字典，而字典的枚举顺序是「当前实现恰好保持插入顺序」，
    /// 不是语言契约；前端表格列序与 Excel 表头都需要一个确定的顺序。
    /// </para>
    /// </summary>
    public List<string> columns { get; set; } = new();

    /// <summary>当前页数据，每个元素的 key 就是 <see cref="columns"/> 里的名字。</summary>
    public List<Dictionary<string, object?>> items { get; set; } = new();

    /// <summary>
    /// 符合条件的总记录数。请求里 <c>need_total = false</c> 时为 null。
    /// <para>
    /// <b>null 表示「没统计」，0 表示「确实没有数据」</b>——这两种情况前端要显示的东西不一样
    /// （「-」还是「共 0 条」），所以不用 0 兼作「未知」。
    /// </para>
    /// <para>
    /// 只要统计了就一定是精确值：服务端要么真正跑了 count，要么在第一页没取满的情况下
    /// 直接沿用本页行数（那也已经是精确的）。没有「估算值」这种中间状态。
    /// </para>
    /// </summary>
    public long? total { get; set; }

    /// <summary>当前页码，从 1 开始。</summary>
    public int page_index { get; set; }

    /// <summary>每页条数（已按配置上限夹紧后的实际值）。</summary>
    public int page_size { get; set; }

    /// <summary>总页数，由 <see cref="total"/> 和 <see cref="page_size"/> 推算；未统计总记录数时为 0。</summary>
    public int total_pages => total is null || page_size <= 0
        ? 0
        : (int)((total.Value + page_size - 1) / page_size);
}

/// <summary>
/// 导出取数的原始结果（只在仓储与业务层之间传递，不出现在接口响应里）。
/// <para>
/// 不复用 <see cref="BiReportResultDto"/>：导出不分页、也不需要总记录数，
/// 硬套过去会让「这个对象里哪些字段有意义」变成要靠调用方自己记住的隐性约定。
/// </para>
/// </summary>
public class BiReportExportDataDto
{
    /// <summary>输出列名，顺序与请求里 <c>select</c> 的顺序一致，也就是 Excel 的表头顺序。</summary>
    public List<string> columns { get; set; } = new();

    /// <summary>
    /// 数据行。最多 <c>maxRows + 1</c> 行——多取的那一行是「超限探测」用的信号，
    /// 由业务层负责判断并决定是报错还是截断。
    /// </summary>
    public List<Dictionary<string, object?>> items { get; set; } = new();
}
