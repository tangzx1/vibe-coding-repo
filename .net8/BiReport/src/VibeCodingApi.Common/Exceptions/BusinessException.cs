namespace VibeCodingApi.Common.Exceptions;

/// <summary>
/// 业务异常：表示「请求本身合法，但违反业务规则」（如自编码重复）。
/// <para>
/// 由 Service 层抛出，Api 层的异常过滤器统一翻译成 HTTP 400 + <c>ApiResult</c>，
/// 因此 Service 不需要为了回错误信息而把返回值改成元组或结果对象。
/// </para>
/// <para>
/// 与之相对，程序缺陷（空引用、SQL 写错等）应当直接抛其他异常，
/// 过滤器会归为 500，两者的排查路径不同，不要混用。
/// </para>
/// </summary>
public class BusinessException : Exception
{
    /// <summary>业务状态码，随响应体一起返回。</summary>
    public int Code { get; }

    /// <summary>构造函数。</summary>
    /// <param name="message">给调用方看的失败原因。</param>
    /// <param name="code">业务状态码，默认 1（通用业务错误）。</param>
    public BusinessException(string message, int code = 1) : base(message)
    {
        Code = code;
    }
}
