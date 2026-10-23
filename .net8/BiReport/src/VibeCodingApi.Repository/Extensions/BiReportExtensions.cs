using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeCodingApi.Repository.BiReport;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Repository.Extensions;

/// <summary>
/// BI 报表模块的注册扩展。
/// </summary>
public static class BiReportExtensions
{
    /// <summary>
    /// 绑定并校验 BI 报表配置（appsettings.json 的 <c>BiQuery</c> 节），注册为单例供各层注入。
    /// <para>
    /// 与 <see cref="SqlSugarExtensions.AddSqlSugar"/> 一致：配置在注册阶段解析，
    /// 白名单为空、表名为空、表名重复、表名含非法字符，都在启动时直接抛异常，
    /// 而不是等某个请求打进来才报错。
    /// </para>
    /// <para>
    /// 这里只管「有哪些表可查」。<b>表与表之间怎么关联不在这里配置</b>——关联条件由前端在
    /// 每次查询请求的 <c>joins</c> 里现场给出，服务端在生成 SQL 时校验两端列存在且类型同族。
    /// </para>
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <param name="configuration">配置源。</param>
    public static IServiceCollection AddBiReport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(BiReportOptions.SectionName).Get<BiReportOptions>();

        if (options is null)
        {
            throw new InvalidOperationException(
                $"未找到配置节 {BiReportOptions.SectionName}，请检查 appsettings.json。");
        }

        if (options.Tables.Count == 0)
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 为空：BI 查询需要显式声明可查询表白名单，" +
                "否则前端查不到任何表。");
        }

        // 表名为空在运行时的表现是「配置里写了却查不到」，这类静默失败很难排查，所以在启动时就拦下
        if (options.Tables.Any(table => string.IsNullOrWhiteSpace(table.Name)))
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中存在 Name 为空的配置项。");
        }

        // 表名最终要拼进 FROM 子句，必须过得了标识符守卫。这里放行的话，
        // 会变成「启动正常、每次查询都报标识符不合法」，不如启动就失败
        var illegalName = options.Tables.FirstOrDefault(table => !BiIdentifier.IsValid(table.Name.Trim()));
        if (illegalName is not null)
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表名 {illegalName.Name} 含不受支持的字符" +
                "（只允许字母、数字、下划线和汉字，且不能以数字开头）。");
        }

        var duplicated = options.Tables
            .GroupBy(table => table.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表名重复：{duplicated.Key}。");
        }

        // 跨库 / 分表配置同样在启动期校验。数据库名、分表前缀、分表日期字段最终都会
        // 参与标识符拼接（或用于发现物理表），配错时宁可启动失败，也不要等刷新元数据才报
        foreach (var table in options.Tables)
        {
            ValidateSourceOptions(table);
        }

        if (options.MaxShardsPerTable < 1)
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.MaxShardsPerTable 必须大于 0，当前为 {options.MaxShardsPerTable}。");
        }

        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// 校验一张表的跨库 / 分表配置。逻辑与表名校验一致：所有会进入 SQL 或参与物理表发现的
    /// 名字都必须能过标识符守卫，且分表日期格式必须是白名单里的固定格式。
    /// </summary>
    private static void ValidateSourceOptions(BiReportTableOption table)
    {
        var name = table.Name.Trim();

        if (!string.IsNullOrWhiteSpace(table.Database) && !BiIdentifier.IsValid(table.Database.Trim()))
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表 {name} 的 Database={table.Database} 含不受支持的字符" +
                "（只允许字母、数字、下划线和汉字，且不能以数字开头）。");
        }

        var hasShardPrefix = !string.IsNullOrWhiteSpace(table.ShardTablePrefix);
        var hasShardDateColumn = !string.IsNullOrWhiteSpace(table.ShardDateColumn);

        if (!hasShardPrefix)
        {
            if (hasShardDateColumn)
            {
                throw new InvalidOperationException(
                    $"{BiReportOptions.SectionName}.Tables 中表 {name} 配置了 ShardDateColumn，" +
                    "但没有配置 ShardTablePrefix；只有分表才需要日期字段。");
            }

            return;
        }

        var prefix = table.ShardTablePrefix!.Trim();
        if (!BiIdentifier.IsValid(prefix))
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表 {name} 的 ShardTablePrefix={table.ShardTablePrefix} " +
                "含不受支持的字符（只允许字母、数字、下划线和汉字，且不能以数字开头）。");
        }

        var dateFormat = string.IsNullOrWhiteSpace(table.ShardNameDateFormat)
            ? BiReportTableOption.DefaultShardNameDateFormat
            : table.ShardNameDateFormat.Trim();

        if (!BiReportTableOption.SupportedShardNameDateFormats.Contains(dateFormat, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表 {name} 的 ShardNameDateFormat={table.ShardNameDateFormat} " +
                $"不受支持，只允许：{string.Join("、", BiReportTableOption.SupportedShardNameDateFormats)}。");
        }

        if (hasShardDateColumn && !BiIdentifier.IsValid(table.ShardDateColumn!.Trim()))
        {
            throw new InvalidOperationException(
                $"{BiReportOptions.SectionName}.Tables 中表 {name} 的 ShardDateColumn={table.ShardDateColumn} " +
                "含不受支持的字符（只允许字母、数字、下划线和汉字，且不能以数字开头）。");
        }
    }
}
