using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;
using VibeCodingApi.Repository.IRepository;

namespace VibeCodingApi.Service;

/// <summary>
/// 矿卡举升记录业务实现。
/// <para>
/// 目前是纯查询，没有业务规则要落，服务层只做一次空参校验后转发给仓储；
/// 之所以仍然保留这一层，是为了让控制器不直接依赖仓储（分层约定），后续加分项规则也有落点。
/// </para>
/// </summary>
public class TruckLiftRecordService : ITruckLiftRecordService
{
    private readonly ITruckLiftRecordRepository _repository;

    /// <summary>构造函数。</summary>
    /// <param name="repository">矿卡举升记录仓储。</param>
    public TruckLiftRecordService(ITruckLiftRecordRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<PagedResult<TruckLiftRecord>> GetPagedListAsync(TruckLiftRecordQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _repository.GetPagedListAsync(query);
    }
}
