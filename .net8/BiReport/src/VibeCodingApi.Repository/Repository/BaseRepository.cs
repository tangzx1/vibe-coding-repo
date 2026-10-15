using System.Linq.Expressions;
using SqlSugar;
using VibeCodingApi.Common.Models;
using VibeCodingApi.Repository.IRepository;

namespace VibeCodingApi.Repository.Repository;

/// <summary>
/// 基于 SqlSugar 的泛型仓储实现。
/// <para>
/// 注入的是 <see cref="ISqlSugarClient"/>，由 <c>AddSqlSugar</c> 注册成指向根作用域的单例，
/// 也就是配置数组里的第一个连接 —— 业务库 <c>dispatch</c>（ConnId = <c>WMMETAMINE_MYSQL</c>）。
/// </para>
/// <para>
/// ⚠ 若要给<b>日志库</b>（<c>WMMETAMINE_MYSQL_2</c>）里的表写仓储，不能照搬这个基类：
/// 需要注入具体类型 <c>SqlSugarScope</c>，再调
/// <c>GetConnectionScope(SqlSugarConfigIds.Log)</c> 拿到对应作用域，
/// 否则查询会打到业务库上（表现为查到 0 行，而不是报错）。
/// </para>
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
public class BaseRepository<T> : IBaseRepository<T> where T : class, new()
{
    /// <summary>SqlSugar 客户端，子类可直接用来写实体专属的复杂查询。</summary>
    protected readonly ISqlSugarClient Db;

    /// <summary>构造函数。</summary>
    /// <param name="db">SqlSugar 客户端。</param>
    public BaseRepository(ISqlSugarClient db)
    {
        Db = db;
    }

    /// <inheritdoc />
    public virtual async Task<T?> GetByIdAsync(object id)
    {
        return await Db.Queryable<T>().InSingleAsync(id);
    }

    /// <inheritdoc />
    public virtual async Task<List<T>> GetListAsync(
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object>>? orderBy = null,
        bool desc = false)
    {
        return await BuildQuery(where, orderBy, desc).ToListAsync();
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<T>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object>>? orderBy = null,
        bool desc = false)
    {
        // SqlSugar 的分页通过 ref 参数回填总数，这里用它的异步版 RefAsync<int> 承接
        RefAsync<int> total = 0;
        var items = await BuildQuery(where, orderBy, desc).ToPageListAsync(pageIndex, pageSize, total);

        return new PagedResult<T>
        {
            Items = items,
            Total = total.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? where = null)
    {
        var query = Db.Queryable<T>();
        if (where is not null)
        {
            query = query.Where(where);
        }

        return await query.CountAsync();
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> where)
    {
        return await Db.Queryable<T>().AnyAsync(where);
    }

    /// <inheritdoc />
    public virtual async Task<int> InsertReturnIdentityAsync(T entity)
    {
        return await Db.Insertable(entity).ExecuteReturnIdentityAsync();
    }

    /// <inheritdoc />
    public virtual async Task<int> InsertAsync(T entity)
    {
        return await Db.Insertable(entity).ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public virtual async Task<int> UpdateAsync(T entity)
    {
        // 不带 Where 时 SqlSugar 用实体的主键特性（IsPrimaryKey）拼 WHERE，所以实体必须带上主键值
        return await Db.Updateable(entity).ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteByIdAsync(object id)
    {
        return await Db.Deleteable<T>().In(id).ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteByIdsAsync(IEnumerable<object> ids)
    {
        // 先物化再判断：空集合直接返回，避免生成 IN () 这种语法非法的 SQL
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        return await Db.Deleteable<T>().In(idList).ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteAsync(Expression<Func<T, bool>> where)
    {
        return await Db.Deleteable<T>().Where(where).ExecuteCommandAsync();
    }

    /// <summary>
    /// 按条件拼查询：过滤 + 排序。抽出来是为了让列表查询和分页查询共用同一套拼装逻辑。
    /// </summary>
    private ISugarQueryable<T> BuildQuery(
        Expression<Func<T, bool>>? where,
        Expression<Func<T, object>>? orderBy,
        bool desc)
    {
        var query = Db.Queryable<T>();

        if (where is not null)
        {
            query = query.Where(where);
        }

        if (orderBy is not null)
        {
            query = desc ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        return query;
    }
}
