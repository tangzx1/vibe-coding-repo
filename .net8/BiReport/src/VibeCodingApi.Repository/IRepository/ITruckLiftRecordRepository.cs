using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Repository.IRepository;

/// <summary>
/// 矿卡举升记录表（<c>dispatch.truck_lift_record</c>）仓储。
/// <para>
/// 与 <see cref="ITruckRepository"/> 不同，这里<b>不</b>继承 <see cref="IBaseRepository{T}"/>：
/// 该表是作业过程产生的大流水表，接口目前只做只读查询，不需要把整套增删改查挂上来
/// （挂上来等于给调用方开放了一组没人用、也没人测过的写入口）。将来真要做写操作时再继承也不迟。
/// </para>
/// </summary>
public interface ITruckLiftRecordRepository
{
    /// <summary>
    /// 按查询条件分页查询，结果按创建时间倒序（同一时刻再按主键倒序）。
    /// <para>
    /// 条件拼装放在仓储而不是服务层：SqlSugar 的 <c>WhereIF</c> 只能在引用 SqlSugar 的项目里用，
    /// 服务层不该直接依赖 SqlSugar。
    /// </para>
    /// </summary>
    /// <param name="query">查询条件（页码、每页条数、关键字、创建时间范围）。</param>
    Task<PagedResult<TruckLiftRecord>> GetPagedListAsync(TruckLiftRecordQueryDto query);
}
