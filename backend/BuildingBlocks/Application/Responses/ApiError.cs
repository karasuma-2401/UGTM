namespace UGTM.BuildingBlocks.Application.Responses
{
    /// <summary>
    /// Shape lỗi trả về cho Client
    /// </summary>
    public sealed class ApiError
    {
        public string Code { get; init; } = String.Empty;
        public string Message { get; init; } = String.Empty;

        /// <summary>
        /// Key = field name, value[] = Error list
        /// </summary>
        public IDictionary<string, string[]>? Errors { get; init; } = null;

        // Helper quick create simple error
        public static ApiError Create(string code, string message)
            => new() { Code = code, Message = message };

        // Helper create validation error
        public static ApiError Validation(IDictionary<string, string[]> errors, string message = "Invalid data!")
            => new() { Code = "VALIDATION_ERROR", Message = message, Errors = errors };

    }
}
