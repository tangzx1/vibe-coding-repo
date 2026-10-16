using System.Globalization;
using System.Text.Json;
using VibeCodingApi.Common.Exceptions;

namespace VibeCodingApi.Repository.BiReport;

/// <summary>
/// 把请求里的 JSON 值按目标列的类型转成 CLR 值，用于构造参数。
/// <para>
/// 转换方向由列类型决定，而不是由 JSON 里写的是什么决定。最重要的一条：
/// <b>字符串列收到的数字一律转成字符串</b>——像 <c>truck_code</c> 这种列，库里存着
/// <c>"15566"</c> 这样的纯数字字符串，若拿数字去比会被 MySQL 做隐式转换，
/// 索引失效之外语义也可能不同（前导零、超长数字精度）。
/// </para>
/// </summary>
public static class BiValueCoercion
{
    /// <summary>LIKE 模式串的长度上限，防「一个模式串撑爆查询」。</summary>
    public const int MaxLikeLength = 500;

    /// <summary>日期时间可接受的文本格式（MySQL 客户端常用的几种）。</summary>
    private static readonly string[] DateTimeFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd",
        "yyyyMMddHHmmss",
        "yyyyMMdd"
    };

    /// <summary>
    /// 把单个值按列类型转成 CLR 值。
    /// </summary>
    /// <param name="value">请求里的值。</param>
    /// <param name="dataType">目标列的 MySQL DATA_TYPE。</param>
    /// <param name="field">字段名，仅用于错误提示。</param>
    /// <exception cref="BusinessException">值为空（应改用 is null）、类型不符、或数值/日期解析失败。</exception>
    public static object CoerceScalar(JsonElement? value, string dataType, string field)
    {
        if (value is null || value.Value.ValueKind == JsonValueKind.Null)
        {
            // 不静默忽略：col = NULL 在 SQL 里恒为 NULL（查不到任何行），
            // 让调用方改用 is null，比返回空结果好排查
            throw new BusinessException($"字段 {field} 的比较值不能为空，判断空值请使用 is null 运算符");
        }

        var element = value.Value;

        // 开关值（true/false）只对数值列有意义，MySQL 里没有布尔类型，用 tinyint 存
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            if (BiIdentifier.IsNumericType(dataType))
            {
                return element.GetBoolean() ? 1 : 0;
            }

            throw new BusinessException($"字段 {field} 不接受布尔值，请按 {dataType} 类型传值");
        }

        if (BiIdentifier.IsNumericType(dataType))
        {
            return ToNumber(element, dataType, field);
        }

        if (BiIdentifier.IsDateType(dataType))
        {
            return ToDateTime(element, dataType, field);
        }

        // 其余（字符串列以及 blob 之类的未知类型）统一转字符串
        return ToText(element, dataType, field);
    }

    /// <summary>
    /// 把数组值转成 CLR 值列表，供 <c>in</c> / <c>between</c> 使用。
    /// </summary>
    /// <param name="value">请求里的值，必须是 JSON 数组。</param>
    /// <param name="dataType">目标列的 MySQL DATA_TYPE。</param>
    /// <param name="field">字段名，仅用于错误提示。</param>
    /// <param name="minCount">最少几个元素。</param>
    /// <param name="maxCount">最多几个元素。</param>
    /// <exception cref="BusinessException">不是数组、元素个数不在范围内、或元素转换失败。</exception>
    public static List<object> CoerceArray(
        JsonElement? value,
        string dataType,
        string field,
        int minCount,
        int maxCount)
    {
        if (value is null || value.Value.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException($"字段 {field} 的值必须是数组");
        }

        var element = value.Value;
        var count = element.GetArrayLength();
        if (count < minCount || count > maxCount)
        {
            throw new BusinessException(minCount == maxCount
                ? $"字段 {field} 需要恰好 {minCount} 个值，实际 {count} 个"
                : $"字段 {field} 需要 {minCount}~{maxCount} 个值，实际 {count} 个");
        }

        var values = new List<object>(count);
        foreach (var item in element.EnumerateArray())
        {
            // 逐个复用标量转换，让「数组里混了 null」也能得到和单值一致的报错
            values.Add(CoerceScalar(item, dataType, field));
        }

        return values;
    }

    /// <summary>
    /// 把值转成 LIKE 用的字符串。<c>%</c> / <c>_</c> 由调用方自己写，
    /// 服务端不额外包裹（「精确前缀匹配」和「前后模糊」是两种不同的意图，不该替用户猜）。
    /// </summary>
    /// <exception cref="BusinessException">值不是字符串、或长度超限。</exception>
    public static string CoerceLikePattern(JsonElement? value, string field)
    {
        if (value is null || value.Value.ValueKind != JsonValueKind.String)
        {
            throw new BusinessException($"字段 {field} 的模糊匹配值必须是字符串");
        }

        var text = value.Value.GetString() ?? string.Empty;
        if (text.Length > MaxLikeLength)
        {
            throw new BusinessException($"字段 {field} 的模糊匹配值过长（最多 {MaxLikeLength} 个字符）");
        }

        return text;
    }

    private static object ToNumber(JsonElement element, string dataType, string field)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            // 按 JSON 里实际的写法挑 CLR 类型：整数不带小数、大数保精度
            if (element.TryGetInt64(out var asLong))
            {
                return asLong;
            }

            return element.TryGetDecimal(out var asDecimal)
                ? asDecimal
                : element.GetDouble();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString() ?? string.Empty;
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
            {
                return parsedLong;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDecimal))
            {
                return parsedDecimal;
            }

            throw new BusinessException($"字段 {field} 需要 {dataType} 类型的值，收到 \"{text}\"");
        }

        throw new BusinessException($"字段 {field} 需要 {dataType} 类型的值，收到 {element.ValueKind}");
    }

    private static DateTime ToDateTime(JsonElement element, string dataType, string field)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new BusinessException($"字段 {field} 需要 {dataType} 类型的值，日期请写成字符串");
        }

        var text = (element.GetString() ?? string.Empty).Trim();

        if (DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        // 兜底接 ISO-8601（2026-01-01T00:00:00、带 Z 或时区偏移）
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        throw new BusinessException($"字段 {field} 的日期格式无法识别：\"{text}\"（支持 yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss）");
    }

    private static string ToText(JsonElement element, string dataType, string field) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        // 数字/布尔写成字符串：字符串列就该按字符串比，别让 MySQL 去做隐式转换
        JsonValueKind.Number => element.GetRawText(),
        _ => throw new BusinessException($"字段 {field} 需要 {dataType} 类型的值，收到 {element.ValueKind}")
    };
}
