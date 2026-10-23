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
    ///换班管理
    ///</summary>
    [SugarTable("shift_type", "wmmetamine_mysql")]
    public class ShiftType
    {
        /// <summary>
        /// Desc:编号
        /// Default:
        /// Nullable:False
        /// </summary>
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// Desc:班次编码
        /// Default:""
        /// </summary>
        [SugarColumn(ColumnName = "shift_type_code")]
        [Display(Name = "班次编码")]
        public int ShiftTypeCode { get; set; }

        /// <summary>
        /// Desc:班次名称
        /// Default:""
        /// </summary>
        [SugarColumn(ColumnName = "shift_type_name")]
        [Display(Name = "班次名称")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string ShiftTypeName { get; set; }

        /// <summary>
        /// Desc:开始时间
        /// Default:""
        /// </summary>
        [SugarColumn(ColumnName = "start_time")]
        [Display(Name = "开始时间")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string StartTime { get; set; }

        /// <summary>
        /// Desc:结束时间
        /// Default:""
        /// </summary>
        [SugarColumn(ColumnName = "end_time")]
        [Display(Name = "结束时间")]
        [StringLength(50, ErrorMessage = "【{0}】不能超过{1}字符长度")]
        public string EndTime { get; set; }
        /// <summary>
        /// Desc:结束时间是否跨天
        /// Default:
        /// Nullable:False
        /// </summary>
        [SugarColumn(ColumnName = "is_across_day")]
        [Display(Name = "结束时间是否跨天")]
        public bool IsAcrossDay { get; set; } = false;
    }
}
