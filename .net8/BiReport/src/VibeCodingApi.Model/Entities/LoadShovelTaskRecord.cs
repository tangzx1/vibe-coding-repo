using SqlSugar;

namespace VibeCodingApi.Model.Entities;

/// <summary>
/// 挖装铲车历史任务记录表（日志库 dispatch_log，配置 ConnId = <c>WMMETAMINE_MYSQL_2</c>）。
/// <para>
/// 按 <b>月</b> 分表存储，物理表名为 <c>load_shovel_task_record_yyyyMM01</c>
/// （如 2025 年 5 月 → <c>load_shovel_task_record_20250501</c>）。
/// 属性名与数据库列名完全一致（snake_case），因此除主键外无需再写
/// <c>[SugarColumn(ColumnName = ...)]</c>，<c>nameof()</c> 取到的就是列名。
/// </para>
/// <para>
/// ⚠ <b>必须通过日志库作用域访问</b>：分表的表名发现基于"当前连接所在的库"，
/// 用业务库连接查询会得到 <c>-- No table</c>（返回 0 行而非报错，很容易踩坑）。
/// 本类上的 <c>[SugarTable]</c> 第二个参数只是表注释（与数据库里的注释保持一致），
/// <b>不提供</b>自动选库能力（已实测）。
/// </para>
/// <para>
/// 正确用法：
/// <code>
/// var logDb = sqlSugarScope.GetConnectionScope(SqlSugarConfigIds.Log);
///
/// // 指定时间范围，自动跨月扫描相关分表
/// var list = logDb.Queryable&lt;LoadShovelTaskRecord&gt;()
///     .SplitTable(new DateTime(2025, 5, 1), new DateTime(2025, 7, 31))
///     .Where(x =&gt; x.truck_code == "JN0023")
///     .ToList();
///
/// // 不传参数则扫描全部分表
/// var all = logDb.Queryable&lt;LoadShovelTaskRecord&gt;().SplitTable().Count();
/// </code>
/// </para>
/// </summary>
[SplitTable(SplitType.Month)] // 按月分表（SqlSugar 自带分表支持 年、季、月、周、日）
[SugarTable("load_shovel_task_record_{year}{month}{day}", "wmmetamine_mysql_2")] // 生成表名格式，3 个变量必须要有
// 普通索引，按时间检索
[SugarIndex("Index_create_date_", nameof(LoadShovelTaskRecord.create_date), OrderByType.Desc)]
// 复合普通索引，按设备编码 + 时间
[SugarIndex("Index_shovel_code_create_date_", nameof(LoadShovelTaskRecord.shovel_code), OrderByType.Desc,
    nameof(LoadShovelTaskRecord.create_date), OrderByType.Desc)]
public class LoadShovelTaskRecord
{
    /// <summary>编号（雪花ID，非自增）</summary>
    [SugarColumn(IsPrimaryKey = true)]
    public long id { get; set; }

    /// <summary>任务类型编号</summary>
    public int? task_type_id { get; set; }

    /// <summary>任务类型名称</summary>
    public string? task_type_name { get; set; }

    /// <summary>任务编号</summary>
    public int? task_id { get; set; }

    /// <summary>任务名称</summary>
    public string? task_name { get; set; }

    /// <summary>设备编码（铲车）</summary>
    public string? shovel_code { get; set; }

    /// <summary>设备名称（铲车）</summary>
    public string? shovel_name { get; set; }

    /// <summary>铲车司机</summary>
    public string? shovel_driver_name { get; set; }

    /// <summary>矿卡编码</summary>
    public string? truck_code { get; set; }

    /// <summary>矿卡名称</summary>
    public string? truck_name { get; set; }

    /// <summary>矿卡司机</summary>
    public string? truck_driver_name { get; set; }

    /// <summary>结算方</summary>
    public string? settlement_officer { get; set; }

    /// <summary>作业平台名称</summary>
    public string? work_platform_name { get; set; }

    /// <summary>作业爆堆名称</summary>
    public string? blast_name { get; set; }

    /// <summary>配矿单元名称</summary>
    public string? composit_name { get; set; }

    /// <summary>物料</summary>
    public string? material_type { get; set; }

    /// <summary>工作地点</summary>
    public string? work_place { get; set; }

    /// <summary>开始装载时间</summary>
    public DateTime? load_time { get; set; }

    /// <summary>装载完成时间</summary>
    public DateTime? load_complete_time { get; set; }

    /// <summary>装载前照片</summary>
    public string? load_img { get; set; }

    /// <summary>装载后照片</summary>
    public string? load_complete_img { get; set; }

    /// <summary>产量</summary>
    public double? load_capacity { get; set; }

    /// <summary>产量单位</summary>
    public string? output_unit { get; set; }

    /// <summary>班次</summary>
    public int? shift_type { get; set; }

    /// <summary>作业日期（分表字段：决定记录归属哪个月的分表）</summary>
    [SplitField]
    public DateTime working_date { get; set; }

    /// <summary>工作类型</summary>
    public int? working_type { get; set; }

    /// <summary>是否有效（1：有效，0：无效）</summary>
    public bool enabled { get; set; }

    /// <summary>补录用户</summary>
    public string? creator { get; set; }

    /// <summary>补录时间</summary>
    public DateTime? creation_time { get; set; }

    /// <summary>创建时间</summary>
    public DateTime create_date { get; set; }
}
