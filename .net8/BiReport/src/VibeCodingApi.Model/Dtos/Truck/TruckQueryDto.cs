namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// 矿卡分页查询条件。
/// <para>
/// 刻意保持精简：只有分页和关键字，不提供车队、能源类型等字段级筛选。
/// 需要按某字段取数时先走这里拿到列表再在调用方过滤，接口再多加筛选项就失去了「一个列表接口」的简单性。
/// </para>
/// <para>
/// 字段名沿用 snake_case，与实体 <see cref="Entities.Truck"/> 及接口返回值保持同一套命名，
/// 避免出现「请求用 PascalCase、响应用 snake_case」的割裂。
/// </para>
/// <para>
/// 命名空间刻意定为 <c>VibeCodingApi.Model.Dtos</c>（而不是跟着目录写成 <c>...Dtos.Truck</c>），
/// 因为后者会让命名空间末段与实体类型 <see cref="Entities.Truck"/> 同名，后续引用时容易踩到名称解析的坑。
/// </para>
/// </summary>
public class TruckQueryDto
{
    /// <summary>页码，从 1 开始。小于 1 时按 1 处理。</summary>
    public int page_index { get; set; } = 1;

    /// <summary>每页条数，默认 20；超过 <see cref="MaxPageSize"/> 时被截断，防止一次拉全表。</summary>
    public int page_size { get; set; } = 20;

    /// <summary>
    /// 关键字，只对 <c>truck_code</c>（自编码）和 <c>truck_name</c>（名称）做模糊匹配，
    /// 两者之间是「或」的关系。为空则不参与过滤。
    /// </summary>
    public string? keyword { get; set; }

    /// <summary>每页条数上限。</summary>
    public const int MaxPageSize = 200;
}
