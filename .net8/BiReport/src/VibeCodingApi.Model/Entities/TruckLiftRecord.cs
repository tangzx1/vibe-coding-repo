using SqlSugar;

namespace VibeCodingApi.Model.Entities;

/// <summary>
/// 矿卡举升记录表（业务库 dispatch，配置 ConnId = <c>WMMETAMINE_MYSQL</c>）。
/// <para>
/// 属性名与数据库列名完全一致（snake_case），因此除主键外无需再写
/// <c>[SugarColumn(ColumnName = ...)]</c>。
/// </para>
/// <para>
/// 表里共 75 列，当前只有分页查询接口，但实体仍按「与数据库表一一对应」写全：
/// 只映射用得到的列，将来要按其它列检索时就得回来补，反而更容易漏。
/// </para>
/// </summary>
[SugarTable("truck_lift_record", "矿卡举升记录表")]
public class TruckLiftRecord
{
    /// <summary>编号，自增长</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long id { get; set; }

    /// <summary>矿卡编码</summary>
    public string truck_code { get; set; } = string.Empty;

    /// <summary>矿卡名称</summary>
    public string? truck_name { get; set; }

    /// <summary>装载点</summary>
    public string? load_location_name { get; set; }

    /// <summary>卸载点</summary>
    public string? dump_location_name { get; set; }

    /// <summary>物料类型</summary>
    public string? material_type { get; set; }

    /// <summary>装载到达时间</summary>
    public DateTime? load_arrive_time { get; set; }

    /// <summary>装载时间</summary>
    public DateTime? load_time { get; set; }

    /// <summary>装载完成时间</summary>
    public DateTime? load_complete_time { get; set; }

    /// <summary>装载高程</summary>
    public double? load_altitude { get; set; }

    /// <summary>卸载时间</summary>
    public DateTime? data_time { get; set; }

    /// <summary>卸载完成时间</summary>
    public DateTime? dump_complete_time { get; set; }

    /// <summary>卸载高程</summary>
    public double? dump_altitude { get; set; }

    /// <summary>载重</summary>
    public decimal? capacity { get; set; }

    /// <summary>产量单位</summary>
    public string? output_unit { get; set; }

    /// <summary>空车载重</summary>
    public decimal? empty_weight_capacity { get; set; }

    /// <summary>空车称重时间</summary>
    public DateTime? empty_weight_time { get; set; }

    /// <summary>空车称重地磅</summary>
    public string? empty_weigh_bridge { get; set; }

    /// <summary>重车称重</summary>
    public decimal? travel_weight_capacity { get; set; }

    /// <summary>重车称重时间</summary>
    public DateTime? travel_weight_time { get; set; }

    /// <summary>重车称重地磅</summary>
    public string? travel_weigh_bridge { get; set; }

    /// <summary>皮重称重</summary>
    public decimal? tare_weight_capacity { get; set; }

    /// <summary>皮重称重时间</summary>
    public DateTime? tare_weight_time { get; set; }

    /// <summary>皮重称重地磅</summary>
    public string? tare_weigh_bridge { get; set; }

    /// <summary>任务类型</summary>
    public string? task_type_name { get; set; }

    /// <summary>任务名称</summary>
    public string? task_name { get; set; }

    /// <summary>工作平台编号</summary>
    public int? work_platform_id { get; set; }

    /// <summary>工作平台名称</summary>
    public string? work_platform_name { get; set; }

    /// <summary>爆堆编号</summary>
    public int? blast_id { get; set; }

    /// <summary>爆堆名称</summary>
    public string? blast_name { get; set; }

    /// <summary>caO含量（列名就是这种大小写混排，保持一致）</summary>
    public decimal? caO_capacity { get; set; }

    /// <summary>so3含量</summary>
    public decimal? so3_capacity { get; set; }

    /// <summary>镁含量</summary>
    public decimal? mg_capacity { get; set; }

    /// <summary>班次</summary>
    public int? shift_type { get; set; }

    /// <summary>最小运距</summary>
    public double? min_distance { get; set; }

    /// <summary>最大运距</summary>
    public double? max_distance { get; set; }

    /// <summary>计划运距</summary>
    public double? plan_distance { get; set; }

    /// <summary>实际运距</summary>
    public double? actual_distance { get; set; }

    /// <summary>能耗</summary>
    public double? energy_consumption { get; set; }

    /// <summary>挖机到达用时(单位：秒)</summary>
    public double? shovel_arrive_time { get; set; }

    /// <summary>装载排队时长(单位：秒)</summary>
    public double? load_queuing_time { get; set; }

    /// <summary>卸载排队时长(单位：秒)</summary>
    public double? dump_queuing_time { get; set; }

    /// <summary>平均速度</summary>
    public double? avg_speed { get; set; }

    /// <summary>本次任务开始时间</summary>
    public DateTime? task_start_time { get; set; }

    /// <summary>任务时长</summary>
    public long total_task_time { get; set; }

    /// <summary>挖装记录id</summary>
    public long? shovel_load_task_record_id { get; set; }

    /// <summary>挖机司机</summary>
    public string? shovel_driver_name { get; set; }

    /// <summary>矿卡司机</summary>
    public string? truck_driver_name { get; set; }

    /// <summary>结算方</summary>
    public string? settlement_officer { get; set; }

    /// <summary>数据模式（1：生产 0：补录）</summary>
    public int? data_model { get; set; }

    /// <summary>异常状态：1：无重车称重 2：无空车称重 3：重车重量异常 4：空车重量异常 5：重复台账</summary>
    public string? production_state { get; set; }

    /// <summary>装载挖机经度</summary>
    public double? load_shovel_longitude { get; set; }

    /// <summary>装载挖机纬度</summary>
    public double? load_shovel_latitude { get; set; }

    /// <summary>装载挖机高程</summary>
    public double? load_shovel_height { get; set; }

    /// <summary>里程</summary>
    public double? truck_odometer { get; set; }

    /// <summary>挖机调度模式(1：自由调度 0：绑定调度)</summary>
    public bool? shovel_dispatch_type { get; set; }

    /// <summary>破碎站调度模式(1：自由调度 0：绑定调度)</summary>
    public bool? crush_station_dispatch_type { get; set; }

    /// <summary>卡车硬件模式(0:无人,1:有人)</summary>
    public string? hardware_model { get; set; }

    /// <summary>开始里程</summary>
    public double? odometer_start { get; set; }

    /// <summary>结束里程</summary>
    public double? odometer_end { get; set; }

    /// <summary>开始电量</summary>
    public double? power_start { get; set; }

    /// <summary>结束电量</summary>
    public double? power_end { get; set; }

    /// <summary>记录状态：1：已有任务 2：正在装载 3：装载完成 4：已称重车 5：正在卸载 6：卸载完成 7：已称空车 8：已称皮重</summary>
    public int? record_status { get; set; }

    /// <summary>是否为自动卸载</summary>
    public bool auto_dump { get; set; }

    /// <summary>是否有效(1：有效 0：无效)</summary>
    public bool? enabled { get; set; }

    /// <summary>备注</summary>
    public string? remark { get; set; }

    /// <summary>补录用户</summary>
    public string? creator { get; set; }

    /// <summary>补录时间</summary>
    public DateTime? creation_time { get; set; }

    /// <summary>更新用户</summary>
    public string? updater { get; set; }

    /// <summary>更新时间</summary>
    public DateTime? update_time { get; set; }

    /// <summary>创建时间（列表按此列倒序）</summary>
    public DateTime create_date { get; set; }

    /// <summary>能源类型(0：增程 1：纯电 2：换电)</summary>
    public int energy_type { get; set; }

    /// <summary>装载挖机编码</summary>
    public string? load_shovel_code { get; set; }

    /// <summary>卸载点编码</summary>
    public string? dump_crush_station_code { get; set; }

    /// <summary>装载里程</summary>
    public double? odometer_load { get; set; }
}
