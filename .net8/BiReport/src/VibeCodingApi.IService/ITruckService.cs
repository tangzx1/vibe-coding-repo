using VibeCodingApi.Common.Models;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.IService;

/// <summary>
/// 矿卡信息业务接口。
/// <para>
/// 返回的是实体 <see cref="Truck"/> 而不是专门的响应 DTO：这些接口就是把表数据原样读出/写入，
/// 多一层 OutDto 只会增加一份需要同步维护的字段清单。将来若某些字段（如 CORS 密码）需要脱敏，
/// 再为对应接口单独引入响应模型。
/// </para>
/// <para>
/// 失败语义：查不到返回 null / false；违反业务规则（自编码重复）抛 <c>BusinessException</c>。
/// </para>
/// </summary>
public interface ITruckService
{
    /// <summary>分页查询矿卡列表。</summary>
    /// <param name="query">查询条件。</param>
    Task<PagedResult<Truck>> GetPagedListAsync(TruckQueryDto query);

    /// <summary>按主键查询矿卡，不存在返回 null。</summary>
    /// <param name="id">矿卡主键。</param>
    Task<Truck?> GetByIdAsync(int id);

    /// <summary>新增矿卡。</summary>
    /// <param name="dto">新增入参。</param>
    /// <returns>新记录的主键（数据库自增回填）。</returns>
    /// <exception cref="Common.Exceptions.BusinessException">自编码已存在。</exception>
    Task<int> CreateAsync(TruckCreateDto dto);

    /// <summary>修改矿卡（整体覆盖，创建时间保持原值）。</summary>
    /// <param name="id">矿卡主键。</param>
    /// <param name="dto">修改入参。</param>
    /// <returns>目标记录不存在时返回 false。</returns>
    /// <exception cref="Common.Exceptions.BusinessException">自编码被别的记录占用。</exception>
    Task<bool> UpdateAsync(int id, TruckUpdateDto dto);

    /// <summary>删除单条矿卡。</summary>
    /// <param name="id">矿卡主键。</param>
    /// <returns>目标记录不存在时返回 false。</returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>批量删除矿卡。</summary>
    /// <param name="ids">主键集合，空集合返回 0。</param>
    /// <returns>实际删除的条数（不存在的 id 不计入，也不报错）。</returns>
    Task<int> DeleteBatchAsync(IEnumerable<int> ids);
}
