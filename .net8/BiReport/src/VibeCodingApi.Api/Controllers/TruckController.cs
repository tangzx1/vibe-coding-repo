using Microsoft.AspNetCore.Mvc;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Api.Controllers;

/// <summary>
/// 矿卡信息（<c>dispatch.truck</c>）CRUD 接口。
/// </summary>
[ApiController]
[Route("api/truck")]
public class TruckController : ControllerBase
{
    private readonly ITruckService _truckService;

    /// <summary>构造函数。</summary>
    /// <param name="truckService">矿卡业务服务。</param>
    public TruckController(ITruckService truckService)
    {
        _truckService = truckService;
    }

    /// <summary>分页查询矿卡列表，关键字只匹配自编码与名称。</summary>
    /// <param name="query">查询条件（页码、每页条数、关键字）。</param>
    /// <returns>分页结果，包含当前页数据与符合条件的总记录数。</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<Truck>>>> GetPagedList([FromQuery] TruckQueryDto query)
    {
        var result = await _truckService.GetPagedListAsync(query);
        return Ok(ApiResult<PagedResult<Truck>>.Ok(result));
    }

    /// <summary>按主键查询矿卡详情。</summary>
    /// <param name="id">矿卡主键。</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResult<Truck>>> GetById(int id)
    {
        var truck = await _truckService.GetByIdAsync(id);
        if (truck is null)
        {
            return NotFound(ApiResult<Truck>.Fail($"矿卡 {id} 不存在", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResult<Truck>.Ok(truck));
    }

    /// <summary>新增矿卡。</summary>
    /// <param name="dto">新增入参。</param>
    /// <returns>新记录的主键。</returns>
    [HttpPost]
    public async Task<ActionResult<ApiResult<int>>> Create([FromBody] TruckCreateDto dto)
    {
        var id = await _truckService.CreateAsync(dto);
        return Ok(ApiResult<int>.Ok(id));
    }

    /// <summary>修改矿卡（整体覆盖，创建时间保持原值）。</summary>
    /// <param name="id">矿卡主键。</param>
    /// <param name="dto">修改入参。</param>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResult>> Update(int id, [FromBody] TruckUpdateDto dto)
    {
        var updated = await _truckService.UpdateAsync(id, dto);
        if (!updated)
        {
            return NotFound(ApiResult.Fail($"矿卡 {id} 不存在", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResult.Ok());
    }

    /// <summary>删除单条矿卡。</summary>
    /// <param name="id">矿卡主键。</param>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResult>> Delete(int id)
    {
        var deleted = await _truckService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(ApiResult.Fail($"矿卡 {id} 不存在", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResult.Ok());
    }

    /// <summary>
    /// 批量删除矿卡。
    /// <para>
    /// 主键走查询串（<c>DELETE /api/truck?ids=1&amp;ids=2</c>），而不是请求体：
    /// 带 body 的 DELETE 虽然语法允许，但部分代理、网关和前端 HTTP 客户端会直接丢掉 body，
    /// 表现为「接口返回成功但一条都没删」。
    /// </para>
    /// </summary>
    /// <param name="ids">待删除的主键集合。</param>
    /// <returns>实际删除的条数。</returns>
    [HttpDelete]
    public async Task<ActionResult<ApiResult<int>>> DeleteBatch([FromQuery] List<int>? ids)
    {
        // 空集合直接拒绝：否则会返回「成功，删除 0 条」，调用方容易误以为请求生效了
        if (ids is null || ids.Count == 0)
        {
            return BadRequest(ApiResult<int>.Fail("请至少指定一个要删除的矿卡 id", StatusCodes.Status400BadRequest));
        }

        var deletedRows = await _truckService.DeleteBatchAsync(ids);
        return Ok(ApiResult<int>.Ok(deletedRows));
    }
}
