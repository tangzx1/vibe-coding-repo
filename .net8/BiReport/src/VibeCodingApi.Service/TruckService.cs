using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;
using VibeCodingApi.Repository.IRepository;
using VibeCodingApi.Service.Mappers;

namespace VibeCodingApi.Service;

/// <summary>
/// 矿卡信息业务实现。
/// </summary>
public class TruckService : ITruckService
{
    private readonly ITruckRepository _truckRepository;

    /// <summary>构造函数。</summary>
    /// <param name="truckRepository">矿卡仓储。</param>
    public TruckService(ITruckRepository truckRepository)
    {
        _truckRepository = truckRepository;
    }

    /// <inheritdoc />
    public Task<PagedResult<Truck>> GetPagedListAsync(TruckQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _truckRepository.GetPagedListAsync(query);
    }

    /// <inheritdoc />
    public Task<Truck?> GetByIdAsync(int id)
    {
        return _truckRepository.GetByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(TruckCreateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 统一去掉首尾空格再查重、再入库，避免 "T001" 和 "T001 " 被当成两条不同记录
        var truckCode = dto.truck_code.Trim();
        if (await _truckRepository.ExistsCodeAsync(truckCode))
        {
            throw new BusinessException($"矿卡自编码 {truckCode} 已存在");
        }

        var entity = TruckMapper.ToEntity(dto);
        entity.truck_code = truckCode;
        // 创建时间由服务端决定，不接受客户端传入
        entity.create_date = DateTime.Now;

        return await _truckRepository.InsertReturnIdentityAsync(entity);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(int id, TruckUpdateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 先取原记录：既用来判断是否存在（不存在直接回 false 让控制器返回 404），
        // 也作为覆盖的目标——DTO 里没有 create_date，必须保留库里的原值。
        var entity = await _truckRepository.GetByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        var truckCode = dto.truck_code.Trim();
        // 查重时排除自身，否则不修改自编码的保存也会被判成重复
        if (await _truckRepository.ExistsCodeAsync(truckCode, id))
        {
            throw new BusinessException($"矿卡自编码 {truckCode} 已存在");
        }

        TruckMapper.ApplyUpdate(entity, dto);
        entity.truck_code = truckCode;

        await _truckRepository.UpdateAsync(entity);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        var affectedRows = await _truckRepository.DeleteByIdAsync(id);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public Task<int> DeleteBatchAsync(IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        // 仓储按 object 主键接收，是为了兼容非 int 主键的表；这里装箱一次即可
        return _truckRepository.DeleteByIdsAsync(ids.Cast<object>());
    }
}
