using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VibeCodingApi.Common.Helper
{
    public class IdToStringConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                JToken jt = JValue.ReadFrom(reader);

                // 处理 null 或缺失的字段
                if (jt == null || jt.Type == JTokenType.Null)
                {
                    // 如果目标类型是可空类型，返回 null；否则返回默认值 0
                    return objectType.IsNullableType() ? null : (object)0L;
                }

                // 处理字符串类型的值（如 "123"）
                if (jt.Type == JTokenType.String)
                {
                    string strValue = jt.Value<string>().Trim();
                    if (string.IsNullOrEmpty(strValue))
                    {
                        return objectType.IsNullableType() ? null : (object)0L;
                    }
                    if (long.TryParse(strValue, out long result))
                    {
                        return result;
                    }
                    throw new JsonSerializationException($"无法将字符串 '{strValue}' 转换为 long 类型");
                }

                // 处理数字类型的值（如 123）
                if (jt.Type == JTokenType.Integer)
                {
                    return jt.Value<long>();
                }

                // 其他不支持的类型
                throw new JsonSerializationException($"不支持的 JSON 类型 '{jt.Type}' 转换为 long?");
            }
            catch (Exception ex)
            {
                // 包装异常并添加字段信息，方便排查
                throw new JsonSerializationException(
                    $"转换字段时出错: {ex.Message}", ex);
            }
        }

        public override bool CanConvert(Type objectType)
        {
            Type underlyingType = Nullable.GetUnderlyingType(objectType);
            return objectType == typeof(long) || underlyingType == typeof(long);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                // 序列化 null 时直接写 null
                writer.WriteNull();
                return;
            }

            // 确保值是 long 类型
            if (value is long longValue)
            {
                writer.WriteValue(longValue.ToString());
            }
            else
            {
                throw new Exception($"不支持的类型 '{value.GetType()}' 序列化，预期 long 或 long?");
            }
        }
    }

    // 扩展方法：判断类型是否为可空类型
    public static class TypeExtensions
    {
        public static bool IsNullableType(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
    }
}
