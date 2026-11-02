using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

namespace UGTM.Api.OpenApi;

internal sealed class ScoreSummaryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath?.Trim('/');
        if (path is null || !path.StartsWith("api/thesis/scores", StringComparison.Ordinal)) return Task.CompletedTask;
        if (context.Description.HttpMethod == "POST") Add("Idempotency-Key", JsonSchemaType.String, true, "uuid", ParameterLocation.Header);
        if (path == "api/thesis/scores")
        {
            Add("batchId", JsonSchemaType.String, true, "uuid"); Add("status", JsonSchemaType.String);
            Add("isStale", JsonSchemaType.Boolean); Add("isReadyForPublication", JsonSchemaType.Boolean);
            Add("pageNumber", JsonSchemaType.Integer, false, "int32"); Add("pageSize", JsonSchemaType.Integer, false, "int32");
        }
        if (context.Description.HttpMethod == "GET") foreach (var code in new[] { "400", "401", "403", "404" })
        { operation.Responses ??= new(); operation.Responses.TryAdd(code, new OpenApiResponse { Description = "Selector, quyền hoặc bảng điểm không hợp lệ." }); }
        return Task.CompletedTask;
        void Add(string name, JsonSchemaType type, bool required = false, string? format = null, ParameterLocation location = ParameterLocation.Query)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter { Name = name, In = location, Required = required, Schema = new OpenApiSchema { Type = type, Format = format } });
        }
    }
}
internal sealed class ScoreSummarySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(ScoreSummaryEndpoints.CalculateRequest) && schema.Properties is { } p)
        {
            schema.AdditionalPropertiesAllowed = false;
            p["expectedSummaryVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Minimum = "1", Maximum = "4294967295", Description = "xmin khi summary tồn tại; null/omit chỉ khi chưa có row." };
            p["expectedActiveConfigVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Minimum = "1", Maximum = "2147483647", Description = "Kỳ vọng version Active, tùy chọn; server luôn xác minh nguồn cấu hình." };
        }
        return Task.CompletedTask;
    }
}
