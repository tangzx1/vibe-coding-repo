using System.Linq.Expressions;
using VibeCodingApi.Common.Models;

namespace VibeCodingApi.Repository.IRepository;

/// <summary>
/// 泛型仓储接口，封装单表的基础增删改查。
/// <para>
/// 放在 Repository 项目内部而不是 Model/Common 里：<c>VibeCodingApi.IService</c> 只引用 Model，
/// 引用不到本项目的类型，所以服务接口（ITruckService）本来也不应该暴露仓储类型。
/// </para>
/// <para>
/// 所有查询都走 SqlSugar 的表达式树，生成的 SQL 由 SqlSugar 按当前 DbType（MySQL）翻译，
/// 分页最终落到 <c>LIMIT</c>，符合目标库 MySQL 5.7 的能力范围。
/// </para>
/// </summary>
/// <typeparam name="T">实体类型，需有公共无参构造函数（SqlSugar 映射要求）。</typeparam>
public interface IBaseRepository<T> where T : class, new()
{
    /// <summary>按主键查询单条，未找到返回 null。</summary>
    /// <param name="id">主键值。</param>
    Task<T?> GetByIdAsync(object id);

    /// <summary>条件查询列表（不分页）。</summary>
    /// <param name="where">过滤条件，为 null 表示不过滤。</param>
    /// <param name="orderBy">排序字段，为 null 表示不排序（继承数据库默认顺序）。</param>
    /// <param name="desc">是否倒序。</param>
    Task<List<T>> GetListAsync(
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object>>? orderBy = null,
        bool desc = false);

    /// <summary>分页查询，同时返回符合条件的总记录数。</summary>
    /// <param name="pageIndex">页码，从 1 开始。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="where">过滤条件，为 null 表示不过滤。</param>
    /// <param name="orderBy">排序字段，为 null 表示不排序。</param>
    /// <param name="desc">是否倒序。</param>
    Task<PagedResult<T>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object>>? orderBy = null,
        bool desc = false);

    /// <summary>统计符合条件的记录数。</summary>
    /// <param name="where">过滤条件，为 null 表示全表。</param>
    Task<int> CountAsync(Expression<Func<T, bool>>? where = null);

    /// <summary>是否存在符合条件的记录。</summary>
    /// <param name="where">过滤条件。</param>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> where);

    /// <summary>
    /// 新增并回填自增主键，返回新主键值。
    /// 仅适用于「主键是自增整数」的表；主键由应用层生成（如雪花 ID）的表请用 <see cref="InsertAsync"/>。
    /// </summary>
    /// <param name="entity">待新增的实体。</param>
    Task<int> InsertReturnIdentityAsync(T entity);

    /// <summary>新增，返回受影响行数。</summary>
    /// <param name="entity">待新增的实体。</param>
    Task<int> InsertAsync(T entity);

    /// <summary>按实体主键更新整行，返回受影响行数。</summary>
    /// <param name="entity">带主键的实体。</param>
    Task<int> UpdateAsync(T entity);

    /// <summary>按主键删除，返回受影响行数。</summary>
    /// <param name="id">主键值。</param>
    Task<int> DeleteByIdAsync(object id);

    /// <summary>按主键批量删除，返回受影响行数。</summary>
    /// <param name="ids">主键集合。</param>
    Task<int> DeleteByIdsAsync(IEnumerable<object> ids);

    /// <summary>按条件删除，返回受影响行数。</summary>
    /// <param name="where">过滤条件。</param>
    Task<int> DeleteAsync(Expression<Func<T, bool>> where);
}
