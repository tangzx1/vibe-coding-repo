namespace VibeCodingApi.Repository.Options;

/// <summary>
/// appsettings.json 中 <c>DbConnections</c> 数组的单个连接项。
/// </summary>
public class DbConnectionOption
{
    /// <summary>连接标识，作为 SqlSugar 的 ConfigId（如 WMMETAMINE_MYSQL）。</summary>
    public string ConnId { get; set; } = string.Empty;

    /// <summary>数据库类型，取 SqlSugar <c>DbType</c> 枚举的值（0 = MySql、1 = SqlServer …）。</summary>
    public int DBType { get; set; }

    /// <summary>是否启用；为 false 时不会注册到 SqlSugar。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 命中率（保留字段）。当前引用版本 SqlSugar 5.1.4.221 的
    /// <c>ConnectionConfig</c> / <c>SlaveConnectionConfig</c> 上都<b>没有</b>对应属性
    /// （已用反射逐个核对），所以这个值目前只做配置对齐，不参与任何逻辑。
    /// </summary>
    public int HitRate { get; set; }

    /// <summary>连接字符串。</summary>
    public string Connection { get; set; } = string.Empty;
}
