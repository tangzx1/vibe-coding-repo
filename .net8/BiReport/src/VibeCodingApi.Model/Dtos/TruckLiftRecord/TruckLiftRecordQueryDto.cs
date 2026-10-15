namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// 矿卡举升记录分页查询条件。
/// <para>
/// 刻意保持精简：只有分页、关键字和时间范围三项，不提供司机、平台、任务类型等字段级筛选，
/// 需要按某字段取数时先走这里拿到列表再在调用方过滤。
/// </para>
/// <para>
/// 字段名沿用 snake_case，与实体 <see cref="Entities.TruckLiftRecord"/> 及接口返回值保持同一套命名，
/// 避免出现「请求用 PascalCase、响应用 snake_case」的割裂。
/// </para>
/// <para>
/// 命名空间定为 <c>VibeCodingApi.Model.Dtos</c>（而不是跟着目录写成 <c>...Dtos.TruckLiftRecord</c>），
/// 后者会让命名空间末段与实体类型同名，后续引用时容易踩到名称解析的坑。
/// </para>
/// </summary>
public class TruckLiftRecordQueryDto
{
    /// <summary>页码，从 1 开始。小于 1 时按 1 处理。</summary>
    public int page_index { get; set; } = 1;

    /// <summary>每页条数，默认 20；超过 <see cref="MaxPageSize"/> 时被截断，防止一次拉全表。</summary>
    public int page_size { get; set; } = 20;

    /// <summary>
    /// 关键字，只对 <c>truck_code</c>（矿卡编码）和 <c>truck_name</c>（矿卡名称）做模糊匹配，
    /// 两者之间是「或」的关系。为空则不参与过滤。
    /// </summary>
    public string? keyword { get; set; }

    /// <summary>
    /// 创建时间范围下界（含）。为空则不限下界。
    /// </summary>
    public DateTime? create_date_begin { get; set; }

    /// <summary>
    /// 创建时间范围上界。
    /// <para>
    /// 只给到日期（时间部分为 0 点）时按「包含当天」处理，即 <c>2026-10-15</c> 等价于
    /// <c>&lt; 2026-10-16 00:00:00</c>；给了具体时间则按闭区间（含该时刻）处理。
    /// 每天的记录都是 0 点以后写入的，若按字面的「≤ 当天 0 点」会整天查不到数据，所以做了这个转换。
    /// </para>
    /// </summary>
    public DateTime? create_date_end { get; set; }

    /// <summary>每页条数上限。</summary>
    public const int MaxPageSize = 200;
}
