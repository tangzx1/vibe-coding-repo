using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Model.Entities;

namespace VibeCodingApi.Service.Mappers;

/// <summary>
/// Truck 的 DTO ↔ 实体映射。
/// <para>
/// 手写赋值，不引入 AutoMapper：字段名两边完全一致，映射规则一目了然；
/// 而且映射出错时是编译期报错，不是运行期才发现。
/// </para>
/// </summary>
internal static class TruckMapper
{
    /// <summary>
    /// 由新增入参构造实体。<c>id</c> 交给数据库自增，<c>create_date</c> 由服务层补当前时间。
    /// </summary>
    /// <param name="dto">新增入参。</param>
    public static Truck ToEntity(TruckCreateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        RequireNotNullFields(dto.truck_ins_height, dto.energy_type);

        var entity = new Truck();
        ApplyCreateFields(entity, dto);
        return entity;
    }

    /// <summary>
    /// 把修改入参覆盖到已有实体上。
    /// <para>
    /// 之所以是「在已有实体上覆盖」而不是「new 一个再整体替换」，是因为 DTO 里没有
    /// <c>create_date</c>，整体替换会把它清成 NULL；而且该接口是 PUT 语义，
    /// 未出现在 DTO 里的字段（如创建时间）应保持原值。
    /// </para>
    /// </summary>
    /// <param name="entity">从库里查出来的实体。</param>
    /// <param name="dto">修改入参。</param>
    public static void ApplyUpdate(Truck entity, TruckUpdateDto dto)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);
        RequireNotNullFields(dto.truck_ins_height, dto.energy_type);

        entity.truck_name = dto.truck_name;
        entity.truck_code = dto.truck_code;
        entity.truck_type_id = dto.truck_type_id;
        entity.truck_type_name = dto.truck_type_name;
        entity.truck_brand = dto.truck_brand;
        entity.truck_fleet_id = dto.truck_fleet_id;
        entity.truck_cru_sn = dto.truck_cru_sn;
        entity.truck_tbox_sn = dto.truck_tbox_sn;
        entity.truck_model = dto.truck_model;
        entity.truck_hardware_model = dto.truck_hardware_model;
        entity.truck_struct_type = dto.truck_struct_type;
        entity.charging_pile_code = dto.charging_pile_code;
        entity.truck_dispatch_group = dto.truck_dispatch_group;
        entity.truck_dispatch_type_id = dto.truck_dispatch_type_id;
        entity.task_type_id = dto.task_type_id;
        entity.truck_length = dto.truck_length;
        entity.truck_width = dto.truck_width;
        entity.truck_height = dto.truck_height;
        entity.truck_curb_weight = dto.truck_curb_weight;
        entity.truck_load_capacity = dto.truck_load_capacity;
        entity.truck_ins_height = dto.truck_ins_height!.Value;
        entity.empty_weight_min_range = dto.empty_weight_min_range;
        entity.empty_weight_max_range = dto.empty_weight_max_range;
        entity.travel_weight_min_range = dto.travel_weight_min_range;
        entity.travel_weight_max_range = dto.travel_weight_max_range;
        entity.truck_wireless_id = dto.truck_wireless_id;
        entity.truck_tank_capacity = dto.truck_tank_capacity;
        entity.truck_fuel_consumption = dto.truck_fuel_consumption;
        entity.truck_mobile_terminal_id = dto.truck_mobile_terminal_id;
        entity.truck_steering_angle = dto.truck_steering_angle;
        entity.truck_battery_capacity = dto.truck_battery_capacity;
        entity.truck_charging_rate = dto.truck_charging_rate;
        entity.truck_antenna_back_axle_distance = dto.truck_antenna_back_axle_distance;
        entity.truck_antenna_right_distance = dto.truck_antenna_right_distance;
        entity.truck_back_axle_trail_distance = dto.truck_back_axle_trail_distance;
        entity.truck_offset_heading = dto.truck_offset_heading;
        entity.truck_offset_height = dto.truck_offset_height;
        entity.truck_vin = dto.truck_vin;
        entity.truck_fuel_repair_type = dto.truck_fuel_repair_type;
        entity.truck_rfid = dto.truck_rfid;
        entity.truck_obu_code = dto.truck_obu_code;
        entity.cors_name = dto.cors_name;
        entity.cors_password = dto.cors_password;
        entity.acu_version = dto.acu_version;
        entity.rcu_version = dto.rcu_version;
        entity.vcu_version = dto.vcu_version;
        entity.zt_version = dto.zt_version;
        entity.hm_version = dto.hm_version;
        entity.gp_version = dto.gp_version;
        entity.dcu_version = dto.dcu_version;
        entity.soft_version = dto.soft_version;
        entity.production_date = dto.production_date;
        entity.entry_date = dto.entry_date;
        entity.durable_years = dto.durable_years;
        entity.annual_inspection_cycle = dto.annual_inspection_cycle;
        entity.iot_network_card_number = dto.iot_network_card_number;
        entity.iot_ip = dto.iot_ip;
        entity.master_fuel_capacity = dto.master_fuel_capacity;
        entity.slave_fuel_capacity = dto.slave_fuel_capacity;
        entity.energy_type = dto.energy_type!.Value;
    }

    /// <summary>
    /// 新增入参的字段赋值。与 <see cref="ApplyUpdate"/> 的字段集当前完全一致，
    /// 保持两份是为了让新增/修改两个契约能各自演进；若修改某一侧，记得同步这里。
    /// </summary>
    private static void ApplyCreateFields(Truck entity, TruckCreateDto dto)
    {
        entity.truck_name = dto.truck_name;
        entity.truck_code = dto.truck_code;
        entity.truck_type_id = dto.truck_type_id;
        entity.truck_type_name = dto.truck_type_name;
        entity.truck_brand = dto.truck_brand;
        entity.truck_fleet_id = dto.truck_fleet_id;
        entity.truck_cru_sn = dto.truck_cru_sn;
        entity.truck_tbox_sn = dto.truck_tbox_sn;
        entity.truck_model = dto.truck_model;
        entity.truck_hardware_model = dto.truck_hardware_model;
        entity.truck_struct_type = dto.truck_struct_type;
        entity.charging_pile_code = dto.charging_pile_code;
        entity.truck_dispatch_group = dto.truck_dispatch_group;
        entity.truck_dispatch_type_id = dto.truck_dispatch_type_id;
        entity.task_type_id = dto.task_type_id;
        entity.truck_length = dto.truck_length;
        entity.truck_width = dto.truck_width;
        entity.truck_height = dto.truck_height;
        entity.truck_curb_weight = dto.truck_curb_weight;
        entity.truck_load_capacity = dto.truck_load_capacity;
        entity.truck_ins_height = dto.truck_ins_height!.Value;
        entity.empty_weight_min_range = dto.empty_weight_min_range;
        entity.empty_weight_max_range = dto.empty_weight_max_range;
        entity.travel_weight_min_range = dto.travel_weight_min_range;
        entity.travel_weight_max_range = dto.travel_weight_max_range;
        entity.truck_wireless_id = dto.truck_wireless_id;
        entity.truck_tank_capacity = dto.truck_tank_capacity;
        entity.truck_fuel_consumption = dto.truck_fuel_consumption;
        entity.truck_mobile_terminal_id = dto.truck_mobile_terminal_id;
        entity.truck_steering_angle = dto.truck_steering_angle;
        entity.truck_battery_capacity = dto.truck_battery_capacity;
        entity.truck_charging_rate = dto.truck_charging_rate;
        entity.truck_antenna_back_axle_distance = dto.truck_antenna_back_axle_distance;
        entity.truck_antenna_right_distance = dto.truck_antenna_right_distance;
        entity.truck_back_axle_trail_distance = dto.truck_back_axle_trail_distance;
        entity.truck_offset_heading = dto.truck_offset_heading;
        entity.truck_offset_height = dto.truck_offset_height;
        entity.truck_vin = dto.truck_vin;
        entity.truck_fuel_repair_type = dto.truck_fuel_repair_type;
        entity.truck_rfid = dto.truck_rfid;
        entity.truck_obu_code = dto.truck_obu_code;
        entity.cors_name = dto.cors_name;
        entity.cors_password = dto.cors_password;
        entity.acu_version = dto.acu_version;
        entity.rcu_version = dto.rcu_version;
        entity.vcu_version = dto.vcu_version;
        entity.zt_version = dto.zt_version;
        entity.hm_version = dto.hm_version;
        entity.gp_version = dto.gp_version;
        entity.dcu_version = dto.dcu_version;
        entity.soft_version = dto.soft_version;
        entity.production_date = dto.production_date;
        entity.entry_date = dto.entry_date;
        entity.durable_years = dto.durable_years;
        entity.annual_inspection_cycle = dto.annual_inspection_cycle;
        entity.iot_network_card_number = dto.iot_network_card_number;
        entity.iot_ip = dto.iot_ip;
        entity.master_fuel_capacity = dto.master_fuel_capacity;
        entity.slave_fuel_capacity = dto.slave_fuel_capacity;
        entity.energy_type = dto.energy_type!.Value;
    }

    /// <summary>
    /// 校验实体上不可空的两个字段。
    /// <para>
    /// DTO 里它们是可空类型 + <c>[Required]</c>，正常情况下会被 <c>[ApiController]</c> 的模型校验提前拦下。
    /// 这里再兜一次是因为 Service 不保证只被 HTTP 调用（将来可能被后台任务直接调），
    /// 与其在下面 <c>.Value</c> 处抛一个语焉不详的 InvalidOperationException，不如给出明确的业务错误。
    /// </para>
    /// </summary>
    private static void RequireNotNullFields(double? insHeight, int? energyType)
    {
        if (insHeight is null)
        {
            throw new BusinessException("组合导航高度不能为空");
        }

        if (energyType is null)
        {
            throw new BusinessException("能源类型不能为空");
        }
    }
}
