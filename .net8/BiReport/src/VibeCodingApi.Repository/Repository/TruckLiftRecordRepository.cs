using SqlSugar;
using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;
using VibeCodingApi.Repository.IRepository;

namespace VibeCodingApi.Repository.Repository;

/// <summary>
/// 矿卡举升记录表仓储实现。只做分页查询，见 <see cref="ITruckLiftRecordRepository"/> 的说明。
/// </summary>
public class TruckLiftRecordRepository : ITruckLiftRecordRepository
{
    private readonly ISqlSugarClient _db;

    /// <summary>构造函数。</summary>
    /// <param name="db">SqlSugar 客户端（根作用域 = 业务库 dispatch）。</param>
    public TruckLiftRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TruckLiftRecord>> GetPagedListAsync(TruckLiftRecordQueryDto query)
    {
        // 页码/每页条数在这里兜底：这两个值最终决定 SQL 的 LIMIT，属于数据访问的边界，
        // 不适合依赖调用方一定传对。每页条数封顶可以避免被一次性拉走整张表。
        var pageIndex = query.page_index < 1 ? 1 : query.page_index;
        var pageSize = query.page_size switch
        {
            < 1 => 20,
            > TruckLiftRecordQueryDto.MaxPageSize => TruckLiftRecordQueryDto.MaxPageSize,
            _ => query.page_size
        };

        var keyword = query.keyword?.Trim();

        // 时间范围先算成普通局部变量再进表达式树：SqlSugar 对闭包里的字段取值是运行时解析的，
        // 提前算好既少一次表达式求值，也让下面 WhereIF 的布尔条件能复用同一份判断。
        var hasBegin = query.create_date_begin.HasValue;
        var begin = query.create_date_begin ?? default;

        var hasEnd = query.create_date_end.HasValue;
        var end = query.create_date_end ?? default;
        // 上界只给到日期（时间部分为 0 点）时按「包含当天」处理：换成次日 0 点的开区间。
        // 记录都是当天 0 点以后写入的，若按字面「≤ 当天 0 点」会整天查不到数据。
        var endIsDateOnly = hasEnd && end.TimeOfDay == TimeSpan.Zero;
        if (endIsDateOnly)
        {
            end = end.Date.AddDays(1);
        }

        // WhereIF：条件为 false 时整个条件不拼进 SQL，因此不需要在 C# 里手工拼表达式树。
        // Contains 会被 SqlSugar 翻译成 LIKE '%kw%'；truck_name 可为 NULL，
        // 但 SQL 里 NULL LIKE ... 结果为 NULL（不等于 true），本来就不会命中，所以不必额外判空。
        RefAsync<int> total = 0;
        var items = await _db.Queryable<TruckLiftRecord>()
            .WhereIF(!string.IsNullOrEmpty(keyword),
                x => x.truck_code.Contains(keyword!) || x.truck_name!.Contains(keyword!))
            .WhereIF(hasBegin, x => x.create_date >= begin)
            .WhereIF(endIsDateOnly, x => x.create_date < end)
            .WhereIF(hasEnd && !endIsDateOnly, x => x.create_date <= end)
            .OrderByDescending(x => x.create_date)
            // create_date 只到秒，同一次批量写入的记录时间会重复；再按主键倒序，
            // 保证翻页时行序稳定（顺序不确定时 MySQL 可能让同一行在两页里都出现）
            .OrderByDescending(x => x.id)
            .ToPageListAsync(pageIndex, pageSize, total);

        return new PagedResult<TruckLiftRecord>
        {
            Items = items,
            Total = total.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
