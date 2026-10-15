namespace VibeCodingApi.Common.Models;

/// <summary>
/// 分页查询结果。放在 Common 里是因为仓储层（<c>IBaseRepository</c>）和接口层都要用到它，
/// 而 <c>VibeCodingApi.IService</c> 引用不到仓储项目，只能共享这个最底层的公共项目。
/// </summary>
/// <typeparam name="T">列表元素类型。</typeparam>
public class PagedResult<T>
{
    /// <summary>当前页数据。</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>符合条件的总记录数（不是当前页条数）。</summary>
    public int Total { get; set; }

    /// <summary>当前页码，从 1 开始。</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数。</summary>
    public int PageSize { get; set; }

    /// <summary>总页数，由 <see cref="Total"/> 和 <see cref="PageSize"/> 推算，不参与序列化以外的逻辑。</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (Total + PageSize - 1) / PageSize;
}
