using SqlSugar;
using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;
using VibeCodingApi.Repository.IRepository;

namespace VibeCodingApi.Repository.Repository;

/// <summary>
/// 矿卡信息表仓储实现。基础 CRUD 全部复用 <see cref="BaseRepository{T}"/>，
/// 这里只有分页筛选和自编码唯一性检查两条专属逻辑。
/// </summary>
public class TruckRepository : BaseRepository<Truck>, ITruckRepository
{
    /// <summary>构造函数。</summary>
    /// <param name="db">SqlSugar 客户端（根作用域 = 业务库 dispatch）。</param>
    public TruckRepository(ISqlSugarClient db) : base(db)
    {
    }

    /// <inheritdoc />
    public async Task<PagedResult<Truck>> GetPagedListAsync(TruckQueryDto query)
    {
        // 页码/每页条数在这里兜底：这两个值最终决定 SQL 的 LIMIT，属于数据访问的边界，
        // 不适合依赖调用方一定传对。每页条数封顶可以避免被一次性拉走整张表。
        var pageIndex = query.page_index < 1 ? 1 : query.page_index;
        var pageSize = query.page_size switch
        {
            < 1 => 20,
            > TruckQueryDto.MaxPageSize => TruckQueryDto.MaxPageSize,
            _ => query.page_size
        };

        var keyword = query.keyword?.Trim();

        // WhereIF：条件为 false 时整个条件不拼进 SQL，因此不需要在 C# 里手工拼表达式树。
        // Contains 会被 SqlSugar 翻译成 LIKE '%kw%'。
        RefAsync<int> total = 0;
        var items = await Db.Queryable<Truck>()
            .WhereIF(!string.IsNullOrEmpty(keyword),
                x => x.truck_name.Contains(keyword!) || x.truck_code.Contains(keyword!))
            // 按主键倒序，保证分页结果稳定（没有 ORDER BY 时 MySQL 不保证行序，翻页可能出现重复/漏行）
            .OrderByDescending(x => x.id)
            .ToPageListAsync(pageIndex, pageSize, total);

        return new PagedResult<Truck>
        {
            Items = items,
            Total = total.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool> ExistsCodeAsync(string truckCode, int? excludeId = null)
    {
        // 自编码非空（DTO 上标了 Required），所以不做 null 特判；这里只排除修改时的自身记录
        return await Db.Queryable<Truck>()
            .Where(x => x.truck_code == truckCode && (excludeId == null || x.id != excludeId))
            .AnyAsync();
    }
}
