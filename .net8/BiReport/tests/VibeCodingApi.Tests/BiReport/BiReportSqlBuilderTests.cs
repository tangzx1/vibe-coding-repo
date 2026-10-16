using System.Text.Json;
using VibeCodingApi.Common.Exceptions;
using VibeCodingApi.Model.Dtos;
using VibeCodingApi.Repository.BiReport;
using VibeCodingApi.Repository.Options;

namespace VibeCodingApi.Tests;

/// <summary>
/// <see cref="BiReportSqlBuilder"/> 的单元测试。
/// <para>
/// 不需要数据库、不需要 mock、不需要 <c>WebApplicationFactory</c>：这个类是纯函数，
/// 输入是「查询定义 + 元数据」，输出是一条 SQL 和一串参数。
/// </para>
/// <para>
/// 这是本模块最该固化的部分——动态 SQL 的安全性无法靠代码评审保证，
/// 只能靠「换个输入就必须换个错误」的断言把它钉住。
/// </para>
/// </summary>
public class BiReportSqlBuilderTests
{
    // ---------- 用例数据 ----------

    private static readonly BiReportSchemaDto Schema = new()
    {
        generated_at = new DateTime(2026, 1, 1),
        tables = new List<BiReportTableSchemaDto>
        {
            new()
            {
                table_name = "truck",
                display_name = "矿卡信息",
                columns = new List<BiReportColumnSchemaDto>
                {
                    Column("id", "int"),
                    Column("truck_code", "varchar"),
                    Column("truck_name", "varchar"),
                    Column("truck_fleet_id", "int"),
                    Column("create_date", "datetime")
                }
            },
            new()
            {
                table_name = "truck_lift_record",
                display_name = "矿卡举升记录",
                columns = new List<BiReportColumnSchemaDto>
                {
                    Column("id", "bigint"),
                    Column("truck_code", "varchar"),
                    Column("capacity", "decimal"),
                    Column("create_date", "datetime"),
                    Column("dump_location_name", "varchar")
                }
            }
        }
    };

    private static BiReportColumnSchemaDto Column(string name, string dataType) => new()
    {
        column_name = name,
        display_name = name,
        data_type = dataType,
        is_numeric = BiIdentifier.IsNumericType(dataType),
        is_date = BiIdentifier.IsDateType(dataType),
        is_string = BiIdentifier.IsStringType(dataType)
    };

    /// <summary>断言 SQL 文本时关掉优化器提示，用例才读得清；提示本身有单独的用例覆盖。</summary>
    private static BiReportOptions CreateOptions() => new()
    {
        Enabled = true,
        DefaultPageSize = 20,
        MaxPageSize = 200,
        MaxSelectColumns = 100,
        MaxJoinTables = 8,
        MaxWhereConditions = 30,
        MaxInValues = 1000,
        MaxOrderBy = 5,
        QueryTimeoutMs = 0
    };

    /// <summary>构造一个 JsonElement 值。<c>Clone</c> 让它与 <see cref="JsonDocument"/> 脱钩，可以安全返回。</summary>
    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>两表关联的请求骨架（主表 truck，左连接 truck_lift_record）。</summary>
    private static BiReportQueryDto JoinRequest() => new()
    {
        tables = new List<BiReportTableRefDto>
        {
            new() { table = "truck", alias = "t" },
            new() { table = "truck_lift_record", alias = "r" }
        },
        joins = new List<BiReportJoinDto>
        {
            new()
            {
                join_type = "left",
                left_table = "t",
                left_column = "truck_code",
                right_table = "r",
                right_column = "truck_code"
            }
        },
        select = new List<BiReportSelectDto>
        {
            new() { field = "t.truck_code", alias = "矿卡编码" }
        }
    };

    // ---------- 单表 ----------

    [Fact]
    public void 单表查询_生成带限定列名与兜底排序的SQL()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "t.truck_code", alias = "矿卡编码" },
                new() { field = "t.truck_name", alias = "矿卡名称" }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        // 没有指定排序时按主表主键兜底：不加 ORDER BY 时 MySQL 不保证行序，翻页可能重复或漏行
        Assert.Equal(
            "SELECT t0.`truck_code` AS `矿卡编码`, t0.`truck_name` AS `矿卡名称` " +
            "FROM `truck` AS t0 ORDER BY t0.`id` ASC LIMIT @p0 OFFSET @p1",
            command.Sql);

        Assert.Equal(new[] { "矿卡编码", "矿卡名称" }, command.Columns);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(20, command.Parameters[0].Value);
        Assert.Equal(0, command.Parameters[1].Value);
        Assert.Equal(1, command.PageIndex);
        Assert.Equal(20, command.PageSize);
    }

    [Fact]
    public void 省略别名时_用列名作为输出名()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.truck_code" } }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Equal(new[] { "truck_code" }, command.Columns);
        Assert.Contains("AS `truck_code`", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 输出别名允许中文与Sql关键字()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "t.truck_code", alias = "矿卡编码" },
                new() { field = "t.truck_name", alias = "order" }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        // 反引号包裹后就是普通标识符，不需要维护一份 SQL 关键字黑名单
        Assert.Contains("AS `矿卡编码`", command.Sql, StringComparison.Ordinal);
        Assert.Contains("AS `order`", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 未显式排序且分组时_按分组字段兜底排序()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "t.truck_code" },
                new() { field = "t.id", aggregate = "count", alias = "数量" }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("GROUP BY t0.`truck_code`", command.Sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY t0.`truck_code` ASC", command.Sql, StringComparison.Ordinal);
    }

    // ---------- 多表关联 ----------

    [Fact]
    public void 两表左连接_生成join并列名可参与排序()
    {
        var request = JoinRequest();
        request.select = new List<BiReportSelectDto>
        {
            new() { field = "t.truck_code", alias = "矿卡编码" },
            new() { field = "r.id", aggregate = "count", alias = "举升次数" },
            new() { field = "r.capacity", aggregate = "sum", alias = "总载重" }
        };
        request.where = new List<BiReportFilterDto>
        {
            new() { field = "r.create_date", op = ">=", value = Json("\"2026-01-01 00:00:00\"") },
            new() { field = "t.truck_fleet_id", op = "in", value = Json("[1, 2]") }
        };
        request.group_by = new List<string> { "t.truck_code" };
        request.order_by = new List<BiReportOrderByDto> { new() { field = "总载重", desc = true } };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Equal(
            "SELECT t0.`truck_code` AS `矿卡编码`, COUNT(t1.`id`) AS `举升次数`, SUM(t1.`capacity`) AS `总载重` " +
            "FROM `truck` AS t0 " +
            "LEFT JOIN `truck_lift_record` AS t1 ON t0.`truck_code` = t1.`truck_code` " +
            "WHERE t1.`create_date` >= @p0 AND t0.`truck_fleet_id` IN (@p1, @p2) " +
            "GROUP BY t0.`truck_code` " +
            "ORDER BY SUM(t1.`capacity`) DESC " +
            "LIMIT @p3 OFFSET @p4",
            command.Sql);

        // 日期被转成 DateTime 参数，整数保持 long；值全部走参数，不拼进 SQL
        Assert.Equal(new DateTime(2026, 1, 1), command.Parameters[0].Value);
        Assert.Equal(1L, command.Parameters[1].Value);
        Assert.Equal(2L, command.Parameters[2].Value);
        Assert.Equal(20, command.Parameters[3].Value);
        Assert.Equal(0, command.Parameters[4].Value);
    }

    [Fact]
    public void 表别名只作引用键_不会出现在SQL里()
    {
        var request = JoinRequest();
        request.tables[0].alias = "用户乱起的别名";
        request.tables[1].alias = "还有一个";

        var command = BiReportSqlBuilder.BuildSelect(
            new BiReportQueryDto
            {
                tables = new List<BiReportTableRefDto>
                {
                    new() { table = "truck", alias = "用户乱起的别名" },
                    new() { table = "truck_lift_record", alias = "还有一个" }
                },
                joins = new List<BiReportJoinDto>
                {
                    new()
                    {
                        join_type = "left",
                        left_table = "用户乱起的别名",
                        left_column = "truck_code",
                        right_table = "还有一个",
                        right_column = "truck_code"
                    }
                },
                select = new List<BiReportSelectDto> { new() { field = "用户乱起的别名.truck_code" } }
            },
            Schema,
            CreateOptions());

        Assert.DoesNotContain("用户乱起的别名", command.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("还有一个", command.Sql, StringComparison.Ordinal);
        Assert.Contains("FROM `truck` AS t0", command.Sql, StringComparison.Ordinal);
        Assert.Contains("JOIN `truck_lift_record` AS t1", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void join顺序被重排_每个ON引用的表都已在前面出现过()
    {
        // 故意把 join 的两端写反：BFS 从主表出发后要把方向纠正过来
        var request = JoinRequest();
        request.joins[0].left_table = "r";
        request.joins[0].right_table = "t";

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains(
            "FROM `truck` AS t0 LEFT JOIN `truck_lift_record` AS t1 ON t0.`truck_code` = t1.`truck_code`",
            command.Sql,
            StringComparison.Ordinal);
    }

    // ---------- 过滤运算符 ----------

    [Fact]
    public void between与isnull的参数个数正确()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto> { new() { field = "r.id" } },
            where = new List<BiReportFilterDto>
            {
                new() { field = "r.capacity", op = "between", value = Json("[0, 200]") },
                new() { field = "r.dump_location_name", op = "is not null" },
                new() { field = "r.truck_code", op = "like", value = Json("\"HS%\"") }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("t0.`capacity` BETWEEN @p0 AND @p1", command.Sql, StringComparison.Ordinal);
        Assert.Contains("t0.`dump_location_name` IS NOT NULL", command.Sql, StringComparison.Ordinal);
        Assert.Contains("t0.`truck_code` LIKE @p2", command.Sql, StringComparison.Ordinal);
        // between 占 2 个、is not null 占 0 个、like 占 1 个，再加上 LIMIT / OFFSET
        Assert.Equal(5, command.Parameters.Count);
        Assert.Equal("HS%", command.Parameters[2].Value);
    }

    [Fact]
    public void 字符串列收到数字时_转成字符串比较()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto> { new() { field = "t.truck_code", op = "=", value = Json("15566") } }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        // 拿数字去比 varchar 会让 MySQL 做隐式转换，索引失效之外语义也可能不同
        Assert.Equal("15566", command.Parameters[0].Value);
    }

    [Fact]
    public void 运算符大小写与空白不敏感()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto>
            {
                new() { field = "t.truck_name", op = "IS   Not   NULL" },
                new() { field = "t.truck_fleet_id", op = "!=", value = Json("3") }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("t0.`truck_name` IS NOT NULL", command.Sql, StringComparison.Ordinal);
        // != 归一成 <>
        Assert.Contains("t0.`truck_fleet_id` <> @p0", command.Sql, StringComparison.Ordinal);
    }

    // ---------- 分页夹紧 ----------

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(50, 50)]
    [InlineData(9999, 200)]
    public void 每页条数被夹紧(int requested, int expected)
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            page_size = requested
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Equal(expected, command.PageSize);
        Assert.Equal(expected, command.Parameters[0].Value);
    }

    [Fact]
    public void 页码小于一时按第一页处理()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            page_index = -3,
            page_size = 10
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Equal(1, command.PageIndex);
        Assert.Equal(0, command.Parameters[1].Value);
    }

    [Fact]
    public void 页码过大时直接报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            page_index = int.MaxValue,
            page_size = 200
        };

        // MySQL 的深翻页要扫掉前面所有行，与其让数据库卡住不如拒绝
        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("页码过大", exception.Message, StringComparison.Ordinal);
    }

    // ---------- 优化器提示 ----------

    [Fact]
    public void 配置了超时_在SELECT后插入优化器提示()
    {
        var options = CreateOptions();
        options.QueryTimeoutMs = 30_000;

        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, options);

        Assert.StartsWith("/*+ MAX_EXECUTION_TIME(30000) */ SELECT", command.Sql, StringComparison.Ordinal);
    }

    // ---------- 统计与导出 ----------

    [Fact]
    public void 无分组统计_直接count()
    {
        var request = JoinRequest();
        request.where = new List<BiReportFilterDto>
        {
            new() { field = "t.truck_fleet_id", op = "=", value = Json("1") }
        };

        var command = BiReportSqlBuilder.BuildCount(request, Schema, CreateOptions());

        Assert.Equal(
            "SELECT COUNT(1) FROM `truck` AS t0 " +
            "LEFT JOIN `truck_lift_record` AS t1 ON t0.`truck_code` = t1.`truck_code` " +
            "WHERE t0.`truck_fleet_id` = @p0",
            command.Sql);
        Assert.Single(command.Parameters);
    }

    [Fact]
    public void 有分组统计_用派生表且内层不带排序与分页()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "r.truck_code" },
                new() { field = "r.id", aggregate = "count", alias = "数量" }
            },
            group_by = new List<string> { "r.truck_code" },
            order_by = new List<BiReportOrderByDto> { new() { field = "数量", desc = true } }
        };

        var command = BiReportSqlBuilder.BuildCount(request, Schema, CreateOptions());

        // 内层带 ORDER BY 纯属浪费，带 LIMIT 会直接改变计数语义；
        // 派生表必须有别名（MySQL 5.7 的硬性要求）
        Assert.Equal(
            "SELECT COUNT(1) FROM (SELECT 1 AS x FROM `truck_lift_record` AS t0 " +
            "GROUP BY t0.`truck_code`) t",
            command.Sql);
        Assert.DoesNotContain("ORDER BY", command.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 导出取数_不分页且上限是多取一行()
    {
        var request = JoinRequest();

        var command = BiReportSqlBuilder.BuildExport(request, Schema, CreateOptions(), maxRows: 20_001);

        Assert.Contains("LIMIT @p0", command.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("OFFSET", command.Sql, StringComparison.Ordinal);
        Assert.Equal(20_001, command.Parameters[0].Value);
    }

    // ---------- 表与列的白名单 ----------

    [Fact]
    public void 表不在白名单时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "mysql_user", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.user" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("不在可查询的表清单中", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 列不属于指定表时报错()
    {
        var request = new BiReportQueryDto
        {
            // truck_lift_record 没有 truck_fleet_id 这一列
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto> { new() { field = "r.truck_fleet_id" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("没有字段 truck_fleet_id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 敏感列不在元数据里_引用即报列不存在()
    {
        // cors_password 在元数据构建阶段就被剔除，生成器看不到它，自然也就查不出来
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.cors_password", alias = "p" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("没有字段 cors_password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 多张表都有同名列时必须写表别名()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto>
            {
                new() { table = "truck", alias = "t" },
                new() { table = "truck_lift_record", alias = "r" }
            },
            joins = JoinRequest().joins,
            select = new List<BiReportSelectDto> { new() { field = "truck_code" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("在多张表中都存在", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 表别名重复时报错()
    {
        var request = new BiReportQueryDto
        {
            // 自关联必须用两个不同的别名，否则两端指向同一行
            tables = new List<BiReportTableRefDto>
            {
                new() { table = "truck", alias = "a" },
                new() { table = "truck", alias = "A" }
            },
            select = new List<BiReportSelectDto> { new() { field = "a.id" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("重复", exception.Message, StringComparison.Ordinal);
    }

    // ---------- 关联连通性 ----------

    [Fact]
    public void 关联条件少于表数减一时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto>
            {
                new() { table = "truck", alias = "t" },
                new() { table = "truck_lift_record", alias = "r" }
            },
            joins = new List<BiReportJoinDto>(),
            select = new List<BiReportSelectDto> { new() { field = "t.id" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("笛卡尔积", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 关联条件多于表数减一时报错()
    {
        var request = JoinRequest();
        request.joins.Add(new BiReportJoinDto
        {
            join_type = "left",
            left_table = "t",
            left_column = "truck_code",
            right_table = "r",
            right_column = "truck_code"
        });

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("冗余或环形关联", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 有一张表没接上时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto>
            {
                new() { table = "truck", alias = "a" },
                new() { table = "truck_lift_record", alias = "b" },
                new() { table = "truck", alias = "c" }
            },
            // 边数是 3-1=2，条数对得上，但 c 与 b 自成一组、主表 a 到不了它们
            joins = new List<BiReportJoinDto>
            {
                new()
                {
                    join_type = "inner", left_table = "b", left_column = "truck_code",
                    right_table = "c", right_column = "truck_code"
                },
                new()
                {
                    join_type = "inner", left_table = "b", left_column = "id",
                    right_table = "c", right_column = "id"
                }
            },
            select = new List<BiReportSelectDto> { new() { field = "a.id" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("没有和主表", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 关联字段类型不同族时报错()
    {
        var request = JoinRequest();
        // truck_lift_record.truck_code 是 varchar，与 capacity(decimal) 不同族
        request.joins[0].right_column = "capacity";

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("类型不匹配", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 不支持join类型时报错()
    {
        var request = JoinRequest();
        request.joins[0].join_type = "right";

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("只支持 inner / left", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 单表却给了关联条件时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            joins = JoinRequest().joins,
            select = new List<BiReportSelectDto> { new() { field = "t.id" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("不需要指定关联条件", exception.Message, StringComparison.Ordinal);
    }

    // ---------- 聚合与分组 ----------

    [Fact]
    public void 未指定分组时_用非聚合列自动补全()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "r.truck_code" },
                new() { field = "r.capacity", aggregate = "avg", alias = "平均载重" }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("AVG(t0.`capacity`)", command.Sql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY t0.`truck_code`", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 非聚合列没进分组时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "r.truck_code" },
                new() { field = "r.capacity", aggregate = "sum", alias = "总载重" }
            },
            // 显式分组漏了 truck_code
            group_by = new List<string> { "r.dump_location_name" }
        };

        // MySQL 5.7 默认开启 ONLY_FULL_GROUP_BY，不提前拦下来会在数据库端报 1055
        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("既没有聚合也没有出现在分组字段中", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 分组字段可以用输出别名()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "r.truck_code", alias = "车辆" },
                new() { field = "r.id", aggregate = "count", alias = "次数" }
            },
            group_by = new List<string> { "车辆" },
            order_by = new List<BiReportOrderByDto> { new() { field = "次数", desc = true } }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("GROUP BY t0.`truck_code`", command.Sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY COUNT(t0.`id`) DESC", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 对字符串列做sum时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.truck_code", aggregate = "sum" } }
        };

        // 在 varchar 上 SUM 是典型的口径错误，要么报错要么算出让人困惑的结果，直接拒掉
        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("不能做 sum 聚合", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void count允许用星号()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "*", aggregate = "count", alias = "总数" } }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("COUNT(1) AS `总数`", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void sum不允许用星号()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "*", aggregate = "sum" } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("只有 count 允许用 * ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void count_distinct生成去重计数()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "r.truck_code", aggregate = "count_distinct", alias = "车辆数" }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        Assert.Contains("COUNT(DISTINCT t0.`truck_code`) AS `车辆数`", command.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 输出别名重复时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "t.truck_code", alias = "编码" },
                new() { field = "t.truck_name", alias = "编码" }
            }
        };

        // 重名会让结果字典里后面的值悄悄覆盖前面的
        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    // ---------- 注入防护 ----------

    [Theory]
    [InlineData("矿卡编码` , (select 1) as b")]
    [InlineData("a` = 1 --")]
    [InlineData("别名 or 1=1")]
    [InlineData("a; drop table truck")]
    [InlineData("a.b")]
    public void 输出别名含非法字符时被拒绝(string alias)
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.truck_code", alias = alias } }
        };

        // 别名是唯一由用户提供、又要拼进 SQL 的片段，靠的正是「字符集里根本没有能闭合反引号的东西」
        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    [Fact]
    public void 运算符里夹带SQL时被拒绝()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto> { new() { field = "t.id", op = "= 1 OR 1=1", value = Json("1") } }
        };

        // 运算符是查表得到的，请求里的原样文本永远不会进 SQL
        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("不支持的运算符", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 过滤值里的引号与注释符_原样作为参数传递()
    {
        const string injected = "x' OR 1=1 -- ";

        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.truck_code" } },
            where = new List<BiReportFilterDto>
            {
                new() { field = "t.truck_code", op = "=", value = Json("\"x' OR 1=1 -- \"") }
            }
        };

        var command = BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions());

        // SQL 文本里只有占位符，注入串整段落在参数值上——数据库会把它当成一个普通的字符串字面量
        Assert.DoesNotContain("OR 1=1", command.Sql, StringComparison.Ordinal);
        Assert.Equal(injected, command.Parameters[0].Value);
    }

    [Fact]
    public void 表名里夹带SQL时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck` --", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } }
        };

        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    [Fact]
    public void 列名里夹带SQL时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto>
            {
                new() { field = "t.id` , (select 1) as x, t.`id" }
            }
        };

        // 列名靠「必须在元数据里」这一条挡住，而不是靠转义
        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    // ---------- 参数形状 ----------

    [Fact]
    public void in的条件值为空数组时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto> { new() { field = "t.id", op = "in", value = Json("[]") } }
        };

        // 不静默短路成 1=0：那会让「参数没传对」表现成「本该有数据却查不到」
        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    [Fact]
    public void between给了三个值时报错()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck_lift_record", alias = "r" } },
            select = new List<BiReportSelectDto> { new() { field = "r.id" } },
            where = new List<BiReportFilterDto>
            {
                new() { field = "r.capacity", op = "between", value = Json("[1, 2, 3]") }
            }
        };

        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));
    }

    [Fact]
    public void isnull不接受比较值()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto>
            {
                new() { field = "t.truck_name", op = "is null", value = Json("1") }
            }
        };

        // 传了值却静默忽略，会变成「我明明填了值」却永远查不出数据的幽灵 bug
        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("不接受比较值", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 比较值为null时提示改用isnull()
    {
        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } },
            where = new List<BiReportFilterDto> { new() { field = "t.truck_name", op = "=", value = Json("null") } }
        };

        var exception = Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, CreateOptions()));

        Assert.Contains("is null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 模块关闭时任何查询都报错()
    {
        var options = CreateOptions();
        options.Enabled = false;

        var request = new BiReportQueryDto
        {
            tables = new List<BiReportTableRefDto> { new() { table = "truck", alias = "t" } },
            select = new List<BiReportSelectDto> { new() { field = "t.id" } }
        };

        Assert.Throws<BusinessException>(
            () => BiReportSqlBuilder.BuildSelect(request, Schema, options));
    }

    // ---------- 标识符守卫 ----------

    [Theory]
    [InlineData("truck_code", "`truck_code`")]
    [InlineData("矿卡编码", "`矿卡编码`")]
    [InlineData("order", "`order`")]
    public void 合法标识符被反引号包裹(string input, string expected)
    {
        Assert.Equal(expected, BiIdentifier.Quote(input));
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a`b")]
    [InlineData("a'b")]
    [InlineData("a\"b")]
    [InlineData("a;b")]
    [InlineData("1abc")]
    [InlineData("a.b")]
    [InlineData("")]
    [InlineData(null)]
    public void 非法标识符被拒绝(string? input)
    {
        Assert.False(BiIdentifier.IsValid(input));
        Assert.Throws<BusinessException>(() => BiIdentifier.Quote(input));
    }

    [Theory]
    [InlineData("varchar", "varchar", true)]
    [InlineData("int", "bigint", true)]
    [InlineData("datetime", "date", true)]
    [InlineData("varchar", "int", false)]
    [InlineData("datetime", "varchar", false)]
    public void join兼容性只认同族类型(string left, string right, bool expected)
    {
        Assert.Equal(expected, BiIdentifier.IsJoinCompatible(left, right));
    }
}
