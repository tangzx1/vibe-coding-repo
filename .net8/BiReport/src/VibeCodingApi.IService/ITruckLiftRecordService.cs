using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.IService;

/// <summary>
/// 矿卡举升记录业务接口。
/// <para>
/// 返回的是实体 <see cref="TruckLiftRecord"/> 而不是专门的响应 DTO：接口就是把表数据原样读出，
/// 多一层 OutDto 只会增加一份需要同步维护的字段清单。
/// </para>
/// </summary>
public interface ITruckLiftRecordService
{
    /// <summary>分页查询矿卡举升记录，结果按创建时间倒序。</summary>
    /// <param name="query">查询条件（页码、每页条数、关键字、创建时间范围）。</param>
    Task<PagedResult<TruckLiftRecord>> GetPagedListAsync(TruckLiftRecordQueryDto query);
}
