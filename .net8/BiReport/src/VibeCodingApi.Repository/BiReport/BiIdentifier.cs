using System.Text.RegularExpressions;
using VibeCodingApi.Common.Exceptions;

namespace VibeCodingApi.Repository.BiReport;

/// <summary>
/// 标识符（表名、列名、输出别名）的守卫，是「用户可控字符串拼进 SQL」的唯一出口。
/// <para>
/// 动态 SQL 里，表名/列名/别名是语法结构，参数化救不了它们，只能拼字符串。
/// 本类的做法是：<b>从字符集上杜绝逃逸</b>——正则显式排除反引号、单双引号、空格、点、括号、分号，
/// 而不是靠「转义危险字符」（转义规则总有想漏的时候）。任何标识符进 SQL 前都必须过
/// <see cref="Quote"/>。
/// </para>
/// </summary>
public static class BiIdentifier
{
    /// <summary>
    /// 合法标识符：字母（含汉字）或下划线开头，后接字母/数字/下划线/汉字，总长 1..64。
    /// <para>
    /// 长度上限 64 与 MySQL 的标识符上限一致；放行汉字（<c>一-龥</c>，CJK 基本区）
    /// 是因为结果别名允许用中文（如 <c>AS `矿卡编码`</c>，已实测可用）。
    /// </para>
    /// <para>
    /// 反引号、单双引号、空格、点、括号、分号都不在字符集里，所以拼进 SQL 后无法闭合标识符。
    /// </para>
    /// </summary>
    private static readonly Regex SafeIdentifier = new(
        "^[A-Za-z_一-龥][A-Za-z0-9_一-龥]{0,63}$",
        RegexOptions.Compiled);

    /// <summary>
    /// 校验标识符并用反引号包裹，返回可直接拼进 SQL 的片段。
    /// </summary>
    /// <param name="identifier">表名、列名或输出别名。</param>
    /// <exception cref="BusinessException">标识符为空或含非法字符。</exception>
    public static string Quote(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifier.IsMatch(identifier))
        {
            throw new BusinessException($"标识符不合法：{identifier}（只允许字母、数字、下划线和汉字，且不能以数字开头）");
        }

        return $"`{identifier}`";
    }

    /// <summary>
    /// 判断标识符是否合法（不抛异常），用于「配置里写错了」这类只应告警不应中断的场景。
    /// </summary>
    public static bool IsValid(string? identifier) =>
        !string.IsNullOrWhiteSpace(identifier) && SafeIdentifier.IsMatch(identifier);

    /// <summary>
    /// MySQL DATA_TYPE 是否数值类型。决定能否 sum / avg，也影响传入值的强转方向。
    /// <para>
    /// <c>year</c> 归在这里（而不是日期类型）：它按 4 位数字比较才有意义，
    /// 当成日期会得到「2026 → 2026-01-01」这种意外结果。
    /// </para>
    /// </summary>
    public static bool IsNumericType(string? dataType) => dataType switch
    {
        "int" or "bigint" or "smallint" or "tinyint" or "mediumint"
            or "decimal" or "numeric" or "float" or "double" or "real" or "bit" or "year" => true,
        _ => false
    };

    /// <summary>
    /// MySQL DATA_TYPE 是否日期时间类型。
    /// <para>
    /// 不含 <c>time</c>：那是「一天中的时刻」，解析成日期反而绕，按字符串比对更贴近用法。
    /// </para>
    /// </summary>
    public static bool IsDateType(string? dataType) => dataType switch
    {
        "datetime" or "timestamp" or "date" => true,
        _ => false
    };

    /// <summary>MySQL DATA_TYPE 是否字符串类型。不在三类中的类型（如 blob）也按字符串处理。</summary>
    public static bool IsStringType(string? dataType) => dataType switch
    {
        "char" or "varchar" or "tinytext" or "text" or "mediumtext" or "longtext"
            or "enum" or "set" or "json" => true,
        _ => false
    };

    /// <summary>
    /// 两个列能否参与同一次 join：数值对数值、日期对日期、字符串对字符串。
    /// <para>
    /// 不允许跨族（如 <c>varchar</c> 对 <c>int</c>）：MySQL 会做隐式转换，既让索引失效，
    /// 又常常说明建模本身有问题（把编码存成两种类型）。这类查询宁可直接拒绝。
    /// </para>
    /// </summary>
    public static bool IsJoinCompatible(string? leftDataType, string? rightDataType)
    {
        if (IsNumericType(leftDataType) && IsNumericType(rightDataType))
        {
            return true;
        }

        if (IsDateType(leftDataType) && IsDateType(rightDataType))
        {
            return true;
        }

        return IsStringType(leftDataType) && IsStringType(rightDataType);
    }
}
