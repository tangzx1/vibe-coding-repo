using Newtonsoft.Json;
using SqlSugar;
using VibeCodingApi.Common.Helper;

namespace VibeCodingApi.Model.Entities;

///<summary>
///挖装铲车历史任务记录表
///</summary>
[SplitTable(SplitType.Month)]//按月分表 （自带分表支持 年、季、月、周、日）
[SugarTable("load_shovel_task_record_{year}{month}{day}", "wmmetamine_mysql_2")]//生成表名格式 3个变量必须要有
//普通索引,按时间检索
[SugarIndex("Index_create_date_", nameof(LoadShovelTaskRecord.create_date), OrderByType.Desc)]
//复合普通索引，按设备编码+时间
[SugarIndex("Index_shovel_code_create_date_", nameof(LoadShovelTaskRecord.shovel_code), OrderByType.Desc,
    nameof(LoadShovelTaskRecord.create_date), OrderByType.Desc)]
public class LoadShovelTaskRecord
{
    public LoadShovelTaskRecord()
    {
    }
    /// <summary>
    /// Desc:编号
    /// Default:
    /// Nullable:False
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, ColumnDescription = "编号")]
    [JsonConverter(typeof(IdToStringConverter))]
    public long id { get; set; }
    /// <summary>
    /// Desc:任务类型编号
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "任务类型编号")]
    public int? task_type_id { get; set; }
    /// <summary>
    /// Desc:任务类型名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "任务类型名称")]
    public string task_type_name { get; set; }
    /// <summary>
    /// Desc:任务编号
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "任务编号")]
    public int? task_id { get; set; }
    /// <summary>
    /// Desc:任务名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "任务名称")]
    public string task_name { get; set; }
    /// <summary>
    /// Desc:设备编码
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "设备编码")]
    public string shovel_code { get; set; }
    /// <summary>
    /// Desc:设备名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "设备名称")]
    public string shovel_name { get; set; }
    /// <summary>
    /// Desc:铲车司机
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "铲车司机")]
    public string shovel_driver_name { get; set; }
    /// <summary>
    /// Desc:矿卡编码
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "矿卡编码")]
    public string truck_code { get; set; }
    /// <summary>
    /// Desc:矿卡名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "矿卡名称")]
    public string truck_name { get; set; }
    /// <summary>
    /// Desc:矿卡司机
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "矿卡司机")]
    public string truck_driver_name { get; set; }
    /// <summary>
    /// Desc:结算方
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "结算方")]
    public string settlement_officer { get; set; }
    /// <summary>
    /// Desc:作业平台名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "作业平台名称")]
    public string work_platform_name { get; set; }
    /// <summary>
    /// Desc:作业爆堆名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "作业爆堆名称")]
    public string blast_name { get; set; }
    /// <summary>
    /// Desc:配矿单元名称
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "配矿单元名称")]
    public string composit_name { get; set; }
    /// <summary>
    /// 物料类型
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "物料类型")]
    public string material_type { get; set; }
    /// <summary>
    /// Desc:工作地点，选填
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "工作地点")]
    public string work_place { get; set; }
    /// <summary>
    /// Desc:开始装载时间
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "开始装载时间")]
    public DateTime? load_time { get; set; }
    /// <summary>
    /// Desc:装载完成时间
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "装载完成时间")]
    public DateTime? load_complete_time { get; set; }
    /// <summary>
    /// Desc:装载前照片
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "装载前照片")]
    public string load_img { get; set; }
    /// <summary>
    /// Desc:装载后照片
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "装载后照片")]
    public string load_complete_img { get; set; }
    /// <summary>
    /// Desc:产量
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "产量")]
    public double? load_capacity { get; set; }
    /// <summary>
    /// Desc:产量单位
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "产量单位")]
    public string output_unit { get; set; }
    /// <summary>
    /// 班次
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "班次")]
    public int? shift_type { get; set; }

    /// <summary>
    /// 作业日期
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "作业日期")]
    public DateTime? working_date { get; set; }
    /// <summary>
    /// Desc:工作类型
    /// Default:
    /// Nullable:True
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "工作类型")]
    public enumShovelWorkingType? working_type { get; set; }
    /// <summary>
    /// Desc:是否有效（1：有效，0：无效）
    /// Default:
    /// Nullable:False
    /// </summary>
    [SugarColumn(ColumnDescription = "是否有效（1：有效，0：无效）")]
    public bool enabled { get; set; } = true;
    /// <summary>
    /// 补录用户
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "补录用户")]
    public string creator { get; set; }
    /// <summary>
    /// 补录时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "补录时间")]
    public DateTime? creation_time { get; set; }
    /// <summary>
    /// Desc:创建时间
    /// Default:
    /// Nullable:False
    /// </summary>
    [SplitField]
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime create_date { get; set; }
}

public enum enumShovelWorkingType
{
    /// <summary>
    /// 智能调度
    /// </summary>
    Task = 0,
    /// <summary>
    /// 人工调度
    /// </summary>
    Manual = 1
}
