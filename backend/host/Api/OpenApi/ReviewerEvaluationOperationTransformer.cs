using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UGTM.Api.OpenApi;

internal sealed class ReviewerEvaluationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.HttpMethod != "POST" || context.Description.RelativePath?.Trim('/') != "api/thesis/reviewer-evaluations")
            return Task.CompletedTask;
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "UUID không rỗng, bắt buộc mọi môi trường. Retry cùng key và ý định nhận status/DTO gốc.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
        });
        // Body metadata is documentation only: routing must not select a 415 endpoint before authentication.
        var body = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() }
        };
        operation.RequestBody = body;
        body.Content["application/json"].Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalPropertiesAllowed = false,
            Required = new HashSet<string> { "assignmentId", "action", "expectedAssignmentVersion" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["assignmentId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["action"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = Names("SaveDraft", "Submit") },
                ["expectedAssignmentVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "4294967295" },
                ["expectedEvaluationVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Minimum = "1", Maximum = "4294967295", Description = "Null/omitted khi tạo lần đầu; Draft phải gửi version hiện tại." },
                ["score"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Minimum = "0", Maximum = "10", Description = "Kiểm tra trước rounding AwayFromZero 2 chữ số; Submit bắt buộc có điểm." },
                ["comment"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Description = "Trim Unicode, tối đa 2.000 rune sau trim; Submit bắt buộc có góp ý. Không nhận NUL/UTF-16 lỗi." },
                ["defenseApproval"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Enum = Names("Approved", "Rejected", null), Description = "Submit bắt buộc có kết luận; cả Approved/Rejected đều khóa phiếu." }
            }
        };
        return Task.CompletedTask;
    }
    internal static List<JsonNode> Names(params string?[] names) => names.Select(name => (JsonNode)JsonValue.Create(name)!).ToList();
}
