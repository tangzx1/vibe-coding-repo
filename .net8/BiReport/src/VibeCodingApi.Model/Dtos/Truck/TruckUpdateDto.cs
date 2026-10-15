using System.ComponentModel.DataAnnotations;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Model.Dtos;

/// <summary>
/// 修改矿卡的入参。请求路由 <c>/api/truck/{id}</c> 上已带主键，因此这里同样不含 <c>id</c>。
/// <para>
/// 语义是<b>整体覆盖</b>（PUT）：没传的可空字段会被写成 NULL，而不是保留原值。
/// 需要「只改某几个字段」的语义时应该用 PATCH，不要在这里做字段级的部分更新。
/// </para>
/// <para>
/// 字段集当前与 <see cref="TruckCreateDto"/> 完全一致，之所以不复用同一个类、也不做继承，
/// 是为了让两个接口契约能各自演进——例如后续若要禁止修改 <c>truck_code</c>，
/// 只需在这里去掉该属性，不会波及新增接口。
/// </para>
/// </summary>
public class TruckUpdateDto
{
    /// <summary>名称</summary>
    [Required(ErrorMessage = "矿卡名称不能为空")]
    [StringLength(255, ErrorMessage = "矿卡名称长度不能超过 255")]
    public string truck_name { get; set; } = string.Empty;

    /// <summary>自编码（业务唯一，服务层会校验重复并排除自身）</summary>
    [Required(ErrorMessage = "矿卡自编码不能为空")]
    [StringLength(255, ErrorMessage = "矿卡自编码长度不能超过 255")]
    public string truck_code { get; set; } = string.Empty;

    /// <summary>型号id</summary>
    public string? truck_type_id { get; set; }

    /// <summary>型号名称</summary>
    public string? truck_type_name { get; set; }

    /// <summary>品牌</summary>
    public string? truck_brand { get; set; }

    /// <summary>所属车队(有人驾驶、无人驾驶)</summary>
    public int? truck_fleet_id { get; set; }

    /// <summary>远程驾驶控制器SN码</summary>
    public string? truck_cru_sn { get; set; }

    /// <summary>tbox编号</summary>
    public string? truck_tbox_sn { get; set; }

    /// <summary>卡车调度模式(有人驾驶、无人驾驶)</summary>
    public string? truck_model { get; set; }

    /// <summary>卡车硬件模式(有人车、无人车)</summary>
    public string? truck_hardware_model { get; set; }

    /// <summary>结构类型(字典项)</summary>
    public string? truck_struct_type { get; set; }

    /// <summary>关联的充电桩编码</summary>
    public string? charging_pile_code { get; set; }

    /// <summary>生产所在分组</summary>
    public string? truck_dispatch_group { get; set; }

    /// <summary>调度类型</summary>
    public int? truck_dispatch_type_id { get; set; }

    /// <summary>任务类型编号</summary>
    public int? task_type_id { get; set; }

    /// <summary>卡车长度</summary>
    public double? truck_length { get; set; }

    /// <summary>卡车宽度</summary>
    public double? truck_width { get; set; }

    /// <summary>卡车高度</summary>
    public double? truck_height { get; set; }

    /// <summary>整备质量</summary>
    public double? truck_curb_weight { get; set; }

    /// <summary>载重能力(t)</summary>
    public double? truck_load_capacity { get; set; }

    /// <summary>组合导航高度（实体上不可空，用可空类型 + [Required] 以便漏传时能被校验拦下）</summary>
    [Required(ErrorMessage = "组合导航高度不能为空")]
    public double? truck_ins_height { get; set; }

    /// <summary>空车称重最低值</summary>
    public double? empty_weight_min_range { get; set; }

    /// <summary>空车称重最高值</summary>
    public double? empty_weight_max_range { get; set; }

    /// <summary>重车称重最低值</summary>
    public double? travel_weight_min_range { get; set; }

    /// <summary>重车称重最高值</summary>
    public double? travel_weight_max_range { get; set; }

    /// <summary>设备无线电ID</summary>
    public string? truck_wireless_id { get; set; }

    /// <summary>油箱能力(L)</summary>
    public double? truck_tank_capacity { get; set; }

    /// <summary>油耗(L/km)</summary>
    public double? truck_fuel_consumption { get; set; }

    /// <summary>移动终端ID</summary>
    public string? truck_mobile_terminal_id { get; set; }

    /// <summary>转向角度(°)</summary>
    public short? truck_steering_angle { get; set; }

    /// <summary>电池容量</summary>
    public double? truck_battery_capacity { get; set; }

    /// <summary>充电速率</summary>
    public double? truck_charging_rate { get; set; }

    /// <summary>天线离后轴距离</summary>
    public double? truck_antenna_back_axle_distance { get; set; }

    /// <summary>天线离右侧距离</summary>
    public double? truck_antenna_right_distance { get; set; }

    /// <summary>后轴离车尾距离</summary>
    public double? truck_back_axle_trail_distance { get; set; }

    /// <summary>航向角补偿</summary>
    public double? truck_offset_heading { get; set; }

    /// <summary>高程补偿</summary>
    public double? truck_offset_height { get; set; }

    /// <summary>车架号</summary>
    public string? truck_vin { get; set; }

    /// <summary>加油维保类型(0:无 1:加油 2:维保 3:加油维保)</summary>
    public short? truck_fuel_repair_type { get; set; }

    /// <summary>RFID</summary>
    public string? truck_rfid { get; set; }

    /// <summary>OBU编码</summary>
    public string? truck_obu_code { get; set; }

    /// <summary>CORS账号</summary>
    public string? cors_name { get; set; }

    /// <summary>CORS密码</summary>
    public string? cors_password { get; set; }

    /// <summary>ACU版本号</summary>
    public string? acu_version { get; set; }

    /// <summary>RCU版本号</summary>
    public string? rcu_version { get; set; }

    /// <summary>VCU版本号</summary>
    public string? vcu_version { get; set; }

    /// <summary>中台版本号</summary>
    public string? zt_version { get; set; }

    /// <summary>地图版本号</summary>
    public string? hm_version { get; set; }

    /// <summary>全局规划版本号</summary>
    public string? gp_version { get; set; }

    /// <summary>DCU版本号</summary>
    public string? dcu_version { get; set; }

    /// <summary>移动终端版本</summary>
    public string? soft_version { get; set; }

    /// <summary>出厂时间</summary>
    public DateTime? production_date { get; set; }

    /// <summary>入厂时间</summary>
    public DateTime? entry_date { get; set; }

    /// <summary>使用年限</summary>
    public int? durable_years { get; set; }

    /// <summary>年检周期</summary>
    public int? annual_inspection_cycle { get; set; }

    /// <summary>物联网网卡卡号</summary>
    public string? iot_network_card_number { get; set; }

    /// <summary>IP</summary>
    public string? iot_ip { get; set; }

    /// <summary>主油箱容积（单位L）</summary>
    public double? master_fuel_capacity { get; set; }

    /// <summary>副油箱容积（单位L）</summary>
    public double? slave_fuel_capacity { get; set; }

    /// <summary>能源类型(0：增程 1：纯电 2：换电)</summary>
    [Required(ErrorMessage = "能源类型不能为空")]
    [Range(0, 2, ErrorMessage = "能源类型只能是 0(增程)、1(纯电)、2(换电)")]
    public int? energy_type { get; set; }
}
