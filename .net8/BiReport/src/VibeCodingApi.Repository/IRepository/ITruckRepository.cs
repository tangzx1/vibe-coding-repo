using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Repository.IRepository;

/// <summary>
/// 矿卡信息表（<c>dispatch.truck</c>）仓储。
/// <para>
/// 基础增删改查直接继承 <see cref="IBaseRepository{T}"/>，这里只声明 Truck 专属的查询。
/// </para>
/// </summary>
public interface ITruckRepository : IBaseRepository<Truck>
{
    /// <summary>
    /// 按查询条件分页查询。
    /// <para>
    /// 条件拼装放在仓储而不是服务层：SqlSugar 的 <c>WhereIF</c> 只能在引用 SqlSugar 的项目里用，
    /// 服务层不该直接依赖 SqlSugar。
    /// </para>
    /// </summary>
    /// <param name="query">查询条件（页码、每页条数、关键字）。</param>
    Task<PagedResult<Truck>> GetPagedListAsync(TruckQueryDto query);

    /// <summary>
    /// 判断自编码是否已被占用。
    /// </summary>
    /// <param name="truckCode">待检查的自编码。</param>
    /// <param name="excludeId">
    /// 需要排除的记录主键。修改场景传当前记录 id，否则会把自己算成重复。
    /// </param>
    Task<bool> ExistsCodeAsync(string truckCode, int? excludeId = null);
}
