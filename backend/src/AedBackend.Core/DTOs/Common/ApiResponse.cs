namespace AedBackend.Core.DTOs.Common;


/// <summary>
/// 统一API响应格式
/// 契约：{code，message，data}
/// </summary>
/// <param name="Code"></param>
/// <param name="Message"></param>
public sealed record ApiResponse(int Code, string Message)
{
    public static ApiResponse Ok(string message = "success")
    {
        return new ApiResponse(0, message);
    }

    public static ApiResponse Fail(int code, string message)
    {
        return new ApiResponse(code, message);
    }

    public static ApiResponse<T> Ok<T>(T? data, string message = "success")
    {
        return new ApiResponse<T>(0, message, data);
    }

    public static ApiResponse<T> Fail<T>(int code, string message)
    {
        return new ApiResponse<T>(code, message, default);
    }
}

public sealed record ApiResponse<T>(int Code, string Message, T? Data);
