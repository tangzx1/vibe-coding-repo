using Microsoft.AspNetCore.Mvc;
using VibeCodingApi.Common.Models;
using VibeCodingApi.IService;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Api.Controllers;

/// <summary>
/// 矿卡举升记录（<c>dispatch.truck_lift_record</c>）查询接口。
/// </summary>
[ApiController]
[Route("api/truck-lift-record")]
public class TruckLiftRecordController : ControllerBase
{
    private readonly ITruckLiftRecordService _service;

    /// <summary>构造函数。</summary>
    /// <param name="service">矿卡举升记录业务服务。</param>
    public TruckLiftRecordController(ITruckLiftRecordService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询矿卡举升记录。
    /// <para>
    /// 关键字只匹配矿卡编码（<c>truck_code</c>）与矿卡名称（<c>truck_name</c>）；
    /// 时间范围过滤 <c>create_date</c>；结果按 <c>create_date</c> 倒序。
    /// </para>
    /// </summary>
    /// <param name="query">查询条件（页码、每页条数、关键字、创建时间范围）。</param>
    /// <returns>分页结果，包含当前页数据与符合条件的总记录数。</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<TruckLiftRecord>>>> GetPagedList(
        [FromQuery] TruckLiftRecordQueryDto query)
    {
        var result = await _service.GetPagedListAsync(query);
        return Ok(ApiResult<PagedResult<TruckLiftRecord>>.Ok(result));
    }
}
