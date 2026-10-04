namespace UGTM.Api
{
    /// <summary>
    /// Shape lỗi trả về cho Client
    /// </summary>
    public sealed class ApiError
    {
        // mã lỗi
        public string Code { get; init; } = String.Empty;
        // mess lỗi
        public string Message { get; init; } = String.Empty;

        /// <summary>
        /// Danh sách lỗi validate
        /// Key = tên field, value[] = danh sách lỗi
        /// </summary>
        public IDictionary<string, string[]>? Error { get; init; } = null;
    }
}
