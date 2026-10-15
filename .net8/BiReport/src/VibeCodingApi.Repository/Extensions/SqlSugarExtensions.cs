using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Repository.Extensions;

/// <summary>
/// SqlSugar 多库 ConfigId 常量，取值与 appsettings.json 中 <c>DbConnections[].ConnId</c> 一一对应。
/// </summary>
public static class SqlSugarConfigIds
{
    /// <summary>
    /// 业务库 dispatch，对应配置项 <c>ConnId = "WMMETAMINE_MYSQL"</c>。
    /// 它同时是配置数组的第一项，因此根作用域（直接注入 ISqlSugarClient）走的就是这个库。
    /// </summary>
    public const string Default = "WMMETAMINE_MYSQL";

    /// <summary>
    /// 日志库 dispatch_log，对应配置项 <c>ConnId = "WMMETAMINE_MYSQL_2"</c>。
    /// 通过 <c>db.GetConnectionScope(SqlSugarConfigIds.Log)</c> 访问。
    /// </summary>
    public const string Log = "WMMETAMINE_MYSQL_2";
}

/// <summary>
/// SqlSugar 依赖注入注册扩展。
/// </summary>
public static class SqlSugarExtensions
{
    /// <summary>appsettings.json 中连接数组所在的配置节名。</summary>
    public const string ConnectionsSectionName = "DbConnections";

    /// <summary>
    /// 注册 SqlSugar 客户端（单例 SqlSugarScope，内部线程安全）。
    /// 连接来自 appsettings.json 的 <c>DbConnections</c> 数组，只注册 <c>Enabled = true</c> 的项。
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <param name="configuration">配置源。</param>
    public static IServiceCollection AddSqlSugar(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置在注册阶段解析，缺失或不合法时启动即抛异常，避免运行到一半才发现
        var options = configuration.GetSection(ConnectionsSectionName).Get<List<DbConnectionOption>>();

        if (options is null || options.Count == 0)
        {
            throw new InvalidOperationException(
                $"未找到连接配置 {ConnectionsSectionName}[]，请检查 appsettings.json。");
        }

        var enabledOptions = options.Where(x => x.Enabled).ToList();
        if (enabledOptions.Count == 0)
        {
            throw new InvalidOperationException(
                $"{ConnectionsSectionName} 中没有任何 Enabled = true 的连接。");
        }

        // 保持数组顺序：SqlSugar 在没有 ConfigId = "Default" 时，会把第一个连接当作根作用域，
        // 所以 WMMETAMINE_MYSQL 必须排在 WMMETAMINE_MYSQL_2 前面。
        var connectionConfigs = enabledOptions.Select(CreateConnectionConfig).ToList();

        // 同时注册具体类型 SqlSugarScope：多库访问需要它的 GetConnectionScope(configId) 方法，
        // ISqlSugarClient 抽象上没有该方法。两者指向同一实例。
        services.AddSingleton<SqlSugarScope>(_ => new SqlSugarScope(connectionConfigs));
        services.AddSingleton<ISqlSugarClient>(sp => sp.GetRequiredService<SqlSugarScope>());

        return services;
    }

    private static ConnectionConfig CreateConnectionConfig(DbConnectionOption option)
    {
        if (string.IsNullOrWhiteSpace(option.ConnId))
        {
            throw new InvalidOperationException(
                $"{ConnectionsSectionName} 中存在 ConnId 为空的配置项，请检查 appsettings.json。");
        }

        if (string.IsNullOrWhiteSpace(option.Connection))
        {
            throw new InvalidOperationException(
                $"连接 {option.ConnId} 的 Connection 为空，请检查 appsettings.json。");
        }

        if (!Enum.IsDefined(typeof(DbType), option.DBType))
        {
            throw new InvalidOperationException(
                $"连接 {option.ConnId} 的 DBType = {option.DBType} 不是合法的 SqlSugar DbType 值。");
        }

        return new ConnectionConfig
        {
            ConfigId = option.ConnId,
            ConnectionString = option.Connection,
            DbType = (DbType)option.DBType,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        };
    }
}
