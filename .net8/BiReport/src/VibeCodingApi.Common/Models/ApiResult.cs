namespace VibeCodingApi.Common.Models;

/// <summary>
/// 接口统一返回结构（无数据体），用于删除等只关心成败、没有返回数据的接口。
/// </summary>
public class ApiResult
{
    /// <summary>业务状态码，0 表示成功，非 0 表示失败。</summary>
    public int Code { get; set; }

    /// <summary>提示信息，成功时为 "success"。</summary>
    public string Message { get; set; } = "success";

    /// <summary>构造一个成功结果。</summary>
    public static ApiResult Ok() => new() { Code = 0, Message = "success" };

    /// <summary>构造一个失败结果。</summary>
    /// <param name="message">失败原因。</param>
    /// <param name="code">业务状态码，默认 1（通用业务错误）。</param>
    public static ApiResult Fail(string message, int code = 1) =>
        new() { Code = code, Message = message };
}

/// <summary>
/// 带数据体的接口统一返回结构。所有 Controller 的返回值都用它包一层，
/// 前端只需要判断 <see cref="ApiResult.Code"/> 即可，不必区分「HTTP 失败」和「业务失败」两种解析路径。
/// </summary>
/// <typeparam name="T">业务数据类型。</typeparam>
public class ApiResult<T> : ApiResult
{
    /// <summary>业务数据，失败时为 null。</summary>
    public T? Data { get; set; }

    /// <summary>构造一个成功结果。</summary>
    /// <param name="data">业务数据。</param>
    public static ApiResult<T> Ok(T? data) => new() { Code = 0, Message = "success", Data = data };

    /// <summary>构造一个失败结果（返回类型是泛型版，便于直接作为接口返回值）。</summary>
    /// <param name="message">失败原因。</param>
    /// <param name="code">业务状态码，默认 1（通用业务错误）。</param>
    public static new ApiResult<T> Fail(string message, int code = 1) =>
        new() { Code = code, Message = message, Data = default };
}
