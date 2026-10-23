using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VibeCodingApi.Model.Entities
{
    ///<summary>
    ///挖机信息表
    ///</summary>
    [SugarTable("shovel", "WMMETAMINE_MYSQL")]
    public class Shovel
    {
        public Shovel()
        {
        }
        /// <summary>
        /// Desc:编号，自增长
        /// Default:
        /// Nullable:False
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }
        /// <summary>
        /// Desc:名称
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "名称")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_name { get; set; }
        /// <summary>
        /// Desc:自编码
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "自编码")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_code { get; set; }
        /// <summary>
        /// Desc:铲车型号id
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲车型号id")]
        [StringLength(30, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_type_id { get; set; }
        /// <summary>
        /// Desc:型号名称
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "型号名称")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_type_name { get; set; }
        /// <summary>
        /// Desc:铲车品牌
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲车品牌")]
        [StringLength(30, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_brand { get; set; }
        /// <summary>
        /// Desc:生产所在分组
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "生产所在分组")]
        [StringLength(30, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_dispatch_group { get; set; }
        [SugarColumn(IsIgnore = true)]
        public List<int> Shovel_dispatch_group_list
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Shovel_dispatch_group))
                {
                    return new List<int>();
                }
                else
                {
                    return Array.ConvertAll(Shovel_dispatch_group.Split(',', StringSplitOptions.RemoveEmptyEntries), s => int.TryParse(s, out int i) ? i : -1).ToList().FindAll(o => o != -1);
                }
            }
            set
            {
                if (value.IsNullOrEmpty())
                {
                    Shovel_dispatch_group = "";
                }
                else
                {
                    Shovel_dispatch_group = string.Join(',', value);
                }
            }
        }
        /// <summary>
        /// Desc:调度类型
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "调度类型")]
        public int? Shovel_dispatch_type_id { get; set; }
        /// <summary>
        /// Desc:最大装载率(t/h)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "最大装载率")]
        public decimal? Shovel_max_loading { get; set; }
        /// <summary>
        /// Desc:优先级
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "优先级")]
        public string Shovel_level { get; set; }
        /// <summary>
        /// Desc:生产能力(0-1)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "生产能力")]
        public decimal? Shovel_throughput { get; set; }
        /// <summary>
        /// Desc:非LP卡车调度惩罚因子(0-400)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "非LP卡车调度惩罚因子")]
        public short? Shovel_penalty_factor { get; set; }
        /// <summary>
        /// Desc:采样距离
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "采样距离")]
        public decimal? Shovel_sampling_distance { get; set; }
        /// <summary>
        /// Desc:设备无线电ID
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "设备无线电ID")]
        [StringLength(20, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_wiress_id { get; set; }
        /// <summary>
        /// Desc:油箱能力(L)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "油箱能力")]
        [StringLength(255, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_tank_capacity { get; set; }
        /// <summary>
        /// Desc:油耗(L/h)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "油耗")]
        public decimal? Shovel_fuel_consumption { get; set; }
        /// <summary>
        /// Desc:移动终端ID
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "移动终端ID")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_mobile_terminal_id { get; set; }
        /// <summary>
        /// Desc:转向角度(°)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "转向角度")]
        public short? Shovel_steering_angle { get; set; }
        /// <summary>
        /// Desc:铲斗尺寸(m³)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲斗尺寸")]
        public decimal? Shovel_bucket_size { get; set; }
        /// <summary>
        /// Desc:铲斗定位参数
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲斗定位参数")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_bucket_param { get; set; }
        /// <summary>
        /// Desc:铲装类型
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲装类型")]
        [StringLength(20, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Shovel_Shovel_type { get; set; }
        /// <summary>
        /// Desc:大臂长度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "大臂长度")]
        public decimal? Shovel_boom_length { get; set; }
        /// <summary>
        /// Desc:小臂长度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "小臂长度")]
        public decimal? Shovel_jib_length { get; set; }
        /// <summary>
        /// Desc:铲斗长度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲斗长度")]
        public decimal? Shovel_bucket_length { get; set; }
        /// <summary>
        /// Desc:铲斗高度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲斗高度")]
        public decimal? Shovel_bucket_height { get; set; }
        /// <summary>
        /// Desc:大臂与车架绞接点离地距离(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "大臂与车架绞接点离地距离")]
        public decimal? Shovel_boom_ground_distance { get; set; }
        /// <summary>
        /// Desc:生产参数-台阶高度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "生产参数-台阶高度")]
        public decimal? Shovel_produce_step_height { get; set; }
        /// <summary>
        /// Desc:生产参数-安全高度(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "生产参数-安全高度")]
        public decimal? Shovel_produce_safety_height { get; set; }
        /// <summary>
        /// Desc:定位偏移参数-X方向偏移(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "定位偏移参数-X方向偏移")]
        public decimal? Shovel_location_offset_x { get; set; }
        /// <summary>
        /// Desc:定位偏移参数-Y方向偏移(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "定位偏移参数-Y方向偏移")]
        public decimal? Shovel_location_offset_y { get; set; }
        /// <summary>
        /// Desc:定位偏移参数-Z方向偏移(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "定位偏移参数-Z方向偏移")]
        public decimal? Shovel_location_offset_z { get; set; }
        /// <summary>
        /// Desc:铲装位姿-装载模式
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲装位姿-装载模式")]
        public string Shovel_posture_loading_mode { get; set; }
        /// <summary>
        /// Desc:铲装位姿-X偏移(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲装位姿-X偏移")]
        public decimal? Shovel_posture_offset_x { get; set; }
        /// <summary>
        /// Desc:铲装位姿-Y偏移(m)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "铲装位姿-Y偏移")]
        public decimal? Shovel_posture_offset_y { get; set; }
        /// <summary>
        /// Desc:标定参数L
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "标定参数L")]
        public decimal? Shovel_calibrate_param_l { get; set; }
        /// <summary>
        /// Desc:标定参数1
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "标定参数1")]
        public decimal? Shovel_calibrate_param_first { get; set; }
        /// <summary>
        /// Desc:标定参数2
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "标定参数2")]
        public decimal? Shovel_calibrate_param_second { get; set; }
        /// <summary>
        /// Desc:标定参数高程
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "标定参数高程")]
        public decimal? Shovel_calibrate_param_height { get; set; }
        /// <summary>
        /// Desc:新标定参数-r
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "新标定参数-r")]
        public double Shovel_new_calibrate_param_r { get; set; }
        /// <summary>
        /// Desc:新标定参数-theta
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "新标定参数-theta")]
        public double Shovel_new_calibrate_param_theta { get; set; }
        /// <summary>
        /// Desc:新标定参数-beta
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "新标定参数-beta")]
        public double Shovel_new_calibrate_param_beta { get; set; }
        /// <summary>
        /// Desc:新标定参数-lmin
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "新标定参数-lmin")]
        public double Shovel_new_calibrate_param_l_min { get; set; }
        /// <summary>
        /// Desc:新标定参数-lmax
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "新标定参数-lmax")]
        public double Shovel_new_calibrate_param_l_max { get; set; }
        /// <summary>
        /// Desc:位姿标定原始参数
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "位姿标定原始参数")]
        public string Shovel_new_calibrate_base_cal_info { get; set; }
        /// <summary>
        /// Desc:是否标记严重故障
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "是否标记严重故障")]
        public bool? Shovel_major_fault { get; set; }
        /// <summary>
        /// Desc:加油维保类型(0:无  1:加油   2:维保   3:加油维保)
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "加油维保类型")]
        public int Shovel_fuel_repair_type { get; set; }
        /// <summary>
        /// Desc:挖机驾驶室高度
        /// Default:
        /// Nullable:False
        /// </summary>
        [Display(Name = "挖机驾驶室高度")]
        public double shovel_cab_height { get; set; }
        /// <summary>
        /// Desc:CORS账号
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "CORS账号")]
        [StringLength(255, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Cors_name { get; set; }
        /// <summary>
        /// Desc:CORS密码
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "CORS密码")]
        [StringLength(255, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string Cors_password { get; set; }
        /// <summary>
        /// 软件版本
        /// </summary>
        [Display(Name = "软件版本")]
        [StringLength(30, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string soft_version { get; set; }
        /// <summary>
        /// Desc:创建时间
        /// Default:
        /// Nullable:True
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? create_date { get; set; }

        /// <summary>
        /// 出厂时间
        /// </summary>
        public DateTime? production_date { get; set; }

        /// <summary>
        /// 入场时间
        /// </summary>
        public DateTime? entry_date { get; set; }

        /// <summary>
        /// 使用年限
        /// </summary>
        public int? durable_years { get; set; }

        /// <summary>
        /// 年检周期
        /// </summary>
        public int? annual_inspection_cycle { get; set; }

        /// <summary>
        /// 物联网网卡卡号
        /// </summary>
        public string iot_network_card_number { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string iot_ip { get; set; }
    }
}
