namespace UGTM.BuildingBlocks.Application.Responses
{
    /// <summary>
    /// Generic endpoint
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ApiResponse<T>
    {
        public bool Success { get; private init; }
        public T? Data { get; private init; }
        public ApiError? Error { get; private init; }

        /// <summary>
        /// Trả vè thành công kèm Data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ApiResponse<T> Ok(T data)
            => new() { Success = true, Data = data };


        public static ApiResponse<T> Fail(ApiError error)
            => new() { Success = false, Error = error };
    }

    /// <summary>
    /// Non-generic class cho endpoint void
    /// </summary>
    public static class ApiResponse
    {
        public static ApiResponse<object?> Ok() =>
            new ApiResponse<object?>().WithSuccess();
        public static ApiResponse<object?> Fail(ApiError error) =>
            ApiResponse<object?>.Fail(error);
    }

    file static class ApiResponseExt
    {
        internal static ApiResponse<T> WithSuccess<T>(this ApiResponse<T> _) =>
            ApiResponse<T>.Ok(default!);
    }
}
